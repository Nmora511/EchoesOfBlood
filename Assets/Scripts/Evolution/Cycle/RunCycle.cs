using System;
using System.Collections.Generic;
using Evolution.Core;
using Evolution.Persistence;
using Evolution.PlayerModel;
using Evolution.Rooms;
using Evolution.Telemetry;

namespace Evolution.Cycle
{
    // The values from GameBalanceConfig that the cycle needs, so the core does not depend on Unity.
    public class CycleSettings
    {
        public EvolutionSettings Evolution = new EvolutionSettings();
        public RoomAttributeRanges Ranges = new RoomAttributeRanges();
        public RepairRules Rules = new RepairRules();
        public int MinBlocksPerRoom = 5;
    }

    // The whole cycle between two runs:
    //   run N played -> telemetry N -> profile updated -> evolution -> run N+1 chosen.
    // Pure C#, so the RunManager can run it in a Task and the tests can run it without Unity.
    public static class RunCycle
    {
        // First time a participant plays: random repaired population, evolved for 50 (default) generations
        // with the DEFAULT profile, so run 1 is already close to the target curve
        // according to the default weights. Run 1 is still the calibration run: its telemetry is what
        // corrects those default weights.
        // initialEvolution: the convergence of this first evolution (for the Console / logs), may be ignored.
        public static EvolutionState CreateState(string participantId, int seed, CycleSettings settings)
        {
            return CreateState(participantId, seed, settings, out _);
        }

        public static EvolutionState CreateState(string participantId, int seed, CycleSettings settings,
            out RoomEvolutionResult initialEvolution, Action<GenerationStats> reportGeneration = null)
        {
            var rng = new Random(seed);
            var profile = PlayerProfile.CreateDefault(settings.Evolution.Profile, settings.Evolution.BaseStats);
            var context = CreateContext(RoomEvolution.CreateShapes(rng, settings.MinBlocksPerRoom), profile,
                new List<CellType[]>(), settings);

            var randomPopulation = RoomEvolution.CreatePopulation(context, rng);
            initialEvolution = RoomEvolution.Evolve(randomPopulation, context, rng, reportGeneration);
            var best = initialEvolution.Population[0];

            var state = new EvolutionState
            {
                ParticipantId = participantId,
                Seed = seed,
                RunNumber = 1,
                TotalGenerations = settings.Evolution.RoomGa.Generations,
                Profile = profile,
                CurrentPlan = RunPlan.FromGenome(best.Genome, context.Shapes, settings.Ranges),
                CurrentPlanFitness = best.Fitness,
                CurrentPlanGeneration = settings.Evolution.RoomGa.Generations,
                CreatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
            };
            foreach (var individual in initialEvolution.Population) state.Population.Add(individual.Genome);

            state.NextSeed = rng.Next();
            return state;
        }

        // Call once when a run ends and at least one room was played.
        // CHANGES the given state: after it, the state holds the updated profile, the new population
        // and the plan of the next run. Pass a copy if the original must stay untouched.
        // reportGeneration is called once per generation (from the thread that runs this method).
        public static RunReport CompleteRun(EvolutionState state, RunTelemetry run, CycleSettings settings,
            Action<GenerationStats> reportGeneration = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (run == null || run.Rooms.Count == 0)
                throw new ArgumentException("The run has no room telemetry, nothing to learn from.", nameof(run));

            var evolution = settings.Evolution;
            var played = state.CurrentPlan;
            var report = new RunReport
            {
                ParticipantId = state.ParticipantId,
                RunNumber = state.RunNumber,
                Seed = state.Seed,
                PlayedPlan = played,
                PlayedPlanFitness = state.CurrentPlanFitness,
                PlayedPlanGeneration = state.CurrentPlanGeneration,
                Telemetry = run,
                ProfileBefore = state.Profile.Clone()
            };

            // 1. What the model predicted for the run that was played, with the profile it was chosen with.
            //    (The profile and the novelty archive only change below, so this is the same prediction
            //    that made the GA choose this run.)
            var playedGenome = ToGenome(played);
            var playedContext = CreateContext(Shapes(played), report.ProfileBefore, state.RecentLayouts, settings);
            var playedFitness = new RunFitness(playedContext);
            report.PlayedPrediction = playedFitness.Predict(playedGenome);

            // 2. Predicted vs measured, room by room. "At played time" uses the seconds the player really
            //    stayed in the room, so a room where the player died early is compared fairly.
            var model = playedFitness.Model;
            foreach (var telemetry in run.Rooms)
            {
                var k = telemetry.RoomIndex;
                if (k < 0 || k >= RunPlan.RoomCount) continue;
                report.Rooms.Add(Compare(played.Rooms[k], telemetry, report.PlayedPrediction.Rooms[k], model,
                    report.ProfileBefore));
            }

            // 3. Calibration: the played rooms pooled together (once per run), then delta.
            var playedPlans = new List<RoomPlan>();
            var playedTelemetry = new List<RoomTelemetry>();
            foreach (var telemetry in run.Rooms)
            {
                var k = telemetry.RoomIndex;
                if (k < 0 || k >= RunPlan.RoomCount) continue;
                playedPlans.Add(played.Rooms[k]);
                playedTelemetry.Add(telemetry);
            }
            ProfileUpdater.UpdateFromRooms(state.Profile, playedPlans, playedTelemetry, model, evolution.Profile);
            ProfileUpdater.UpdateFromRun(state.Profile, run, evolution.Profile);
            report.ProfileAfter = state.Profile.Clone();

            // 4. Novelty archive: only the rooms the player actually saw.
            foreach (var telemetry in run.Rooms)
            {
                var k = telemetry.RoomIndex;
                if (k < 0 || k >= RunPlan.RoomCount) continue;
                state.RecentLayouts.Add((CellType[])played.Rooms[k].Genome.Cells.Clone());
            }
            while (state.RecentLayouts.Count > evolution.RoomFitness.RecentLayoutsKept) state.RecentLayouts.RemoveAt(0);

            // 5. Evolution: the saved generation is the initial population, against the shapes of the next run.
            var rng = new Random(state.NextSeed);
            var nextShapes = RoomEvolution.CreateShapes(rng, settings.MinBlocksPerRoom);
            var context = CreateContext(nextShapes, state.Profile, state.RecentLayouts, settings);
            FitPopulationSize(state.Population, evolution.RoomGa.PopulationSize, rng);

            report.GenerationFrom = state.TotalGenerations;
            report.Evolution = RoomEvolution.Evolve(state.Population, context, rng, reportGeneration);
            report.GenerationTo = state.TotalGenerations + evolution.RoomGa.Generations;

            // 6. The best individual becomes the next run. The whole last generation is kept.
            var best = report.Evolution.Population[0];
            state.Population = new List<RunGenome>(report.Evolution.Population.Count);
            foreach (var individual in report.Evolution.Population) state.Population.Add(individual.Genome);

            state.TotalGenerations = report.GenerationTo;
            state.RunNumber++;
            state.CurrentPlan = RunPlan.FromGenome(best.Genome, nextShapes, settings.Ranges);
            state.CurrentPlanFitness = best.Fitness;
            state.CurrentPlanGeneration = state.TotalGenerations;
            state.NextSeed = rng.Next();

            report.NextRunNumber = state.RunNumber;
            report.NextPlan = state.CurrentPlan;
            report.NextPrediction = report.Evolution.BestPrediction;
            report.State = state;
            return report;
        }

        private static RoomComparison Compare(RoomPlan room, RoomTelemetry telemetry, RoomPrediction planned,
            RoomDifficultyModel model, PlayerProfile profile)
        {
            var comparison = new RoomComparison
            {
                RoomIndex = telemetry.RoomIndex,
                Outcome = telemetry.Outcome,
                PlannedTime = telemetry.PlannedTime,
                TimeSurvived = telemetry.TimeSurvived,
                Target = planned.Target,
                PredictedPlanned = planned.Total,
                Observed = telemetry.TotalDamageFraction
            };

            var exposure = model.Exposure(room.Genome, room.Shape, room.Settings, profile, telemetry.TimeSurvived);
            foreach (var source in ProfileUpdater.RoomSources)
            {
                var i = (int)source;
                comparison.PredictedBySource[i] = profile.Weights[i] * exposure[i];
                comparison.ObservedBySource[i] = telemetry.DamageFraction(source);
                comparison.PredictedAtPlayedTime += comparison.PredictedBySource[i];
            }
            return comparison;
        }

        // The Inspector value may have changed since the population was saved.
        private static void FitPopulationSize(List<RunGenome> population, int size, Random rng)
        {
            size = Math.Max(1, size);
            if (population.Count > size) population.RemoveRange(size, population.Count - size); // drops the worst
            while (population.Count < size) population.Add(RunGenome.CreateRandom(rng)); // Evolve repairs it
        }

        private static RoomEvolutionContext CreateContext(bool[][] shapes, PlayerProfile profile,
            List<CellType[]> recentLayouts, CycleSettings settings)
        {
            return new RoomEvolutionContext
            {
                Shapes = shapes,
                Ranges = settings.Ranges,
                Rules = settings.Rules,
                Profile = profile,
                Settings = settings.Evolution,
                RecentLayouts = new List<CellType[]>(recentLayouts)
            };
        }

        private static RunGenome ToGenome(RunPlan plan)
        {
            var genome = new RunGenome();
            for (var i = 0; i < RunGenome.RoomCount; i++) genome.Rooms[i] = plan.Rooms[i].Genome.Clone();
            return genome;
        }

        private static bool[][] Shapes(RunPlan plan)
        {
            var shapes = new bool[RunPlan.RoomCount][];
            for (var i = 0; i < shapes.Length; i++) shapes[i] = plan.Rooms[i].Shape;
            return shapes;
        }
    }
}
