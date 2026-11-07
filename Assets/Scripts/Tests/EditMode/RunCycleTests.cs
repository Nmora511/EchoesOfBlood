using System;
using System.IO;
using Evolution.Cycle;
using Evolution.Persistence;
using Evolution.PlayerModel;
using Evolution.Rooms;
using Evolution.Telemetry;
using NUnit.Framework;

namespace Evolution.Tests
{
    // EditMode tests of stage 5: the cycle between runs, the save file and the validation log.
    public class RunCycleTests
    {
        private string _folder;

        [SetUp]
        public void CreateFolder()
        {
            _folder = Path.Combine(Path.GetTempPath(), "EchoesOfBloodTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void DeleteFolder()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private static CycleSettings Settings()
        {
            var settings = new CycleSettings();
            settings.Evolution.RoomGa.Generations = 20; // faster tests
            return settings;
        }

        // Fake telemetry of the plan: the player survives the first rooms losing `damage` of their health to
        // ground enemies, and dies in room `diesIn` (0-based, -1 = survives all 5).
        private static RunTelemetry Play(EvolutionState state, float damage, int diesIn = -1)
        {
            var run = new RunTelemetry(state.RunNumber, state.Seed);
            for (var k = 0; k < RunPlan.RoomCount; k++)
            {
                var room = state.CurrentPlan.Rooms[k];
                var dies = k == diesIn;
                var time = dies ? room.Settings.SurviveTime * 0.5f : room.Settings.SurviveTime;
                var lost = dies ? 100f : damage * 100f;

                var telemetry = new RoomTelemetry(state.RunNumber, k, room.Settings.SurviveTime, 100f);
                telemetry.RecordFrame(time, 100f, false, 0f, false);
                telemetry.RecordDamage(DamageSource.GroundEnemy, lost);
                telemetry.Finish(dies ? RoomOutcome.Died : RoomOutcome.Survived, 100f - lost);
                run.AddRoom(telemetry);

                if (dies) break;
            }
            run.Finish(diesIn < 0, diesIn < 0 ? RunPlan.RoomCount : diesIn);
            return run;
        }

        private static void AssertSamePlan(RunPlan a, RunPlan b)
        {
            for (var k = 0; k < RunPlan.RoomCount; k++)
            {
                Assert.That(b.Rooms[k].Shape, Is.EqualTo(a.Rooms[k].Shape), $"shape of room {k + 1}");
                Assert.That(b.Rooms[k].Genome.Cells, Is.EqualTo(a.Rooms[k].Genome.Cells), $"cells of room {k + 1}");
                Assert.That(b.Rooms[k].Genome.Attributes, Is.EqualTo(a.Rooms[k].Genome.Attributes), $"genes of room {k + 1}");
            }
        }

        [Test]
        public void State_SurvivesSavingAndLoading_Exactly()
        {
            var settings = Settings();
            var state = RunCycle.CreateState("P01", 7, settings);
            RunCycle.CompleteRun(state, Play(state, 0.3f), settings);

            var json = StateSerializer.ToJson(state);
            var loaded = StateSerializer.FromJson(json, settings.Ranges);

            Assert.That(StateSerializer.ToJson(loaded), Is.EqualTo(json));
            Assert.That(loaded.RunNumber, Is.EqualTo(2));
            Assert.That(loaded.Population.Count, Is.EqualTo(settings.Evolution.RoomGa.PopulationSize));
            Assert.That(loaded.Profile.Weights, Is.EqualTo(state.Profile.Weights));
            Assert.That(loaded.RecentLayouts.Count, Is.EqualTo(5));
            AssertSamePlan(state.CurrentPlan, loaded.CurrentPlan);
        }

        [Test]
        public void ClosingAndReopeningTheGame_GivesTheSameNextRun()
        {
            var settings = Settings();

            // Without closing the game.
            var direct = RunCycle.CreateState("P01", 11, settings);
            RunCycle.CompleteRun(direct, Play(direct, 0.3f), settings);
            RunCycle.CompleteRun(direct, Play(direct, 0.5f, 2), settings);

            // Saving and loading from disk between the two runs.
            var first = RunCycle.CreateState("P01", 11, settings);
            RunCycle.CompleteRun(first, Play(first, 0.3f), settings);
            StateStore.Save(_folder, first);
            var reopened = StateStore.Load(_folder, settings.Ranges, out var warning);
            Assert.That(warning, Is.Null);
            RunCycle.CompleteRun(reopened, Play(reopened, 0.5f, 2), settings);

            Assert.That(reopened.RunNumber, Is.EqualTo(3));
            Assert.That(reopened.NextSeed, Is.EqualTo(direct.NextSeed));
            AssertSamePlan(direct.CurrentPlan, reopened.CurrentPlan);
        }

        [Test]
        public void CompleteRun_LearnsAndPlansAPlayableNextRun()
        {
            var settings = Settings();
            var state = RunCycle.CreateState("P01", 3, settings);
            var playedPlan = state.CurrentPlan;

            // The player died in room 3 (index 2).
            var report = RunCycle.CompleteRun(state, Play(state, 0.4f, 2), settings);

            Assert.That(report.RunNumber, Is.EqualTo(1));
            Assert.That(report.NextRunNumber, Is.EqualTo(2));
            Assert.That(report.Rooms.Count, Is.EqualTo(3), "only the played rooms are compared");
            Assert.That(report.Rooms[2].Observed, Is.EqualTo(1f).Within(1e-4));
            Assert.That(report.ProfileAfter.Delta, Is.LessThan(report.ProfileBefore.Delta), "died -> delta goes down");
            Assert.That(state.Profile.RoomsObserved, Is.EqualTo(3));
            Assert.That(state.RecentLayouts.Count, Is.EqualTo(3), "only the played rooms enter the novelty archive");
            Assert.That(state.TotalGenerations, Is.EqualTo(2 * settings.Evolution.RoomGa.Generations),
                "creation + one run, each with Generations generations");
            Assert.That(state.CurrentPlanGeneration, Is.EqualTo(state.TotalGenerations));
            Assert.That(report.Evolution.Generations.Count, Is.EqualTo(settings.Evolution.RoomGa.Generations + 1));
            Assert.That(state.CurrentPlan, Is.Not.SameAs(playedPlan));

            for (var k = 0; k < RunPlan.RoomCount; k++)
            {
                var room = state.CurrentPlan.Rooms[k];
                Assert.That(RoomRepair.FindSafePath(room.Genome, room.Shape), Is.Not.Null, $"room {k + 1} is not playable");
            }
        }

        [Test]
        public void FirstRun_IsEvolvedWithTheDefaultProfile()
        {
            var settings = Settings();
            var state = RunCycle.CreateState("P01", 21, settings, out var evolution);

            Assert.That(state.RunNumber, Is.EqualTo(1));
            Assert.That(state.Profile.RoomsObserved, Is.EqualTo(0), "nobody played yet");
            Assert.That(state.CurrentPlanGeneration, Is.EqualTo(settings.Evolution.RoomGa.Generations));
            Assert.That(evolution.Generations[^1].Best,
                Is.GreaterThan(evolution.Generations[0].Best), "the evolution improved the random population");
            Assert.That(state.CurrentPlanFitness, Is.EqualTo(evolution.Population[0].Fitness));
        }

        [Test]
        public void PredictionAtPlayedTime_IsProportionalToTheTimePlayed()
        {
            var settings = Settings();
            var state = RunCycle.CreateState("P01", 5, settings);
            var report = RunCycle.CompleteRun(state, Play(state, 0.2f, 0), settings);

            // Died at half of the timer of room 1: the enemies' share of the prediction only counts the
            // time after the first spawn, so it is at most half of the full prediction.
            var room = report.Rooms[0];
            Assert.That(room.TimeSurvived, Is.LessThan(room.PlannedTime));
            Assert.That(room.PredictedAtPlayedTime, Is.LessThanOrEqualTo(room.PredictedPlanned * 0.5f + 1e-4f));
        }

        [Test]
        public void ChangingThePopulationSize_InTheInspector_IsHandled()
        {
            var settings = Settings();
            var state = RunCycle.CreateState("P01", 9, settings);

            settings.Evolution.RoomGa.PopulationSize = 12;
            RunCycle.CompleteRun(state, Play(state, 0.3f), settings);
            Assert.That(state.Population.Count, Is.EqualTo(12));

            settings.Evolution.RoomGa.PopulationSize = 40;
            RunCycle.CompleteRun(state, Play(state, 0.3f), settings);
            Assert.That(state.Population.Count, Is.EqualTo(40));
        }

        [Test]
        public void CorruptedSaveFile_FallsBackToTheBackup()
        {
            var settings = Settings();
            var state = RunCycle.CreateState("P01", 13, settings);
            StateStore.Save(_folder, state);                    // run 1
            RunCycle.CompleteRun(state, Play(state, 0.3f), settings);
            StateStore.Save(_folder, state);                    // run 2, run 1 goes to the .bak

            File.WriteAllText(StateStore.PathIn(_folder), "{ \"Version\": 1, \"Prof"); // half written file

            var loaded = StateStore.Load(_folder, settings.Ranges, out var warning);
            Assert.That(warning, Is.Not.Null);
            Assert.That(loaded.RunNumber, Is.EqualTo(1));
            Assert.That(File.Exists(StateStore.PathIn(_folder) + ".tmp"), Is.False);
        }

        [Test]
        public void ValidationLog_WritesOneLinePerRunRoomAndGeneration()
        {
            var settings = Settings();
            var state = RunCycle.CreateState("P01", 17, settings);
            var log = new ValidationLog(_folder);

            log.Write(RunCycle.CompleteRun(state, Play(state, 0.3f), settings));        // 5 rooms
            log.Write(RunCycle.CompleteRun(state, Play(state, 0.3f, 1), settings));     // 2 rooms

            var runs = File.ReadAllLines(Path.Combine(_folder, ValidationLog.RunsFile));
            var rooms = File.ReadAllLines(Path.Combine(_folder, ValidationLog.RoomsFile));
            var generations = File.ReadAllLines(Path.Combine(_folder, ValidationLog.GenerationsFile));

            Assert.That(runs.Length, Is.EqualTo(1 + 2), "header + 2 runs");
            Assert.That(rooms.Length, Is.EqualTo(1 + 5 + 2), "header + 7 played rooms");
            Assert.That(generations.Length, Is.EqualTo(1 + 2 * (settings.Evolution.RoomGa.Generations + 1)));

            // Same number of columns in every line, so the decimal separator never became a comma.
            var columns = runs[0].Split(',').Length;
            foreach (var line in runs) Assert.That(line.Split(',').Length, Is.EqualTo(columns), line);
            columns = rooms[0].Split(',').Length;
            foreach (var line in rooms) Assert.That(line.Split(',').Length, Is.EqualTo(columns), line);

            // The per-run JSON is valid and has the whole chain.
            var json = JsonNode.Parse(File.ReadAllText(Path.Combine(_folder, "runs", "run_002.json")));
            Assert.That(json["RunNumber"].AsInt(), Is.EqualTo(2));
            Assert.That(json["Plan"]["Rooms"].Count, Is.EqualTo(5));
            Assert.That(json["Plan"]["Rooms"][2]["Played"].AsBool(true), Is.False);
            Assert.That(json["PredictedVsObserved"].Count, Is.EqualTo(2));
            Assert.That(json["Telemetry"]["Rooms"].Count, Is.EqualTo(2));
            Assert.That(json["Next"]["RunNumber"].AsInt(), Is.EqualTo(3));
        }

        private static RoomPlan Room(RoomAttributeRanges ranges, int groundSpawners, int rangedSpawners)
        {
            var genome = new RoomGenome();
            for (var i = 0; i < genome.Attributes.Length; i++) genome.Attributes[i] = 0.5f;
            for (var i = 0; i < groundSpawners; i++) genome.Cells[RoomLayout.FromRowCol(0, 2 + i)] = CellType.GroundEnemySpawner;
            for (var i = 0; i < rangedSpawners; i++) genome.Cells[RoomLayout.FromRowCol(8, 2 + i)] = CellType.RangedEnemySpawner;
            return new RoomPlan { Genome = genome, Shape = RoomShape.Full(), Settings = ranges.Decode(genome) };
        }

        private static RoomTelemetry Survived(RoomPlan plan, float groundDamage, float arrowDamage)
        {
            var telemetry = new RoomTelemetry(1, 0, plan.Settings.SurviveTime, 100f);
            telemetry.RecordFrame(plan.Settings.SurviveTime, 100f, false, 0f, false);
            telemetry.RecordDamage(DamageSource.GroundEnemy, groundDamage);
            telemetry.RecordDamage(DamageSource.Arrow, arrowDamage);
            telemetry.Finish(RoomOutcome.Survived, 100f - groundDamage - arrowDamage);
            return telemetry;
        }

        [Test]
        public void GroundEnemies_DoNotReduceTheArrowExposure()
        {
            var settings = new EvolutionSettings();
            var ranges = new RoomAttributeRanges();
            var model = new RoomDifficultyModel(settings.BaseStats, settings.Model);
            var profile = PlayerProfile.CreateDefault(settings.Profile, settings.BaseStats);

            var archersOnly = Room(ranges, 0, 2);
            var crowded = Room(ranges, 5, 2);
            var arrow = (int)DamageSource.Arrow;

            var alone = model.Predict(archersOnly.Genome, archersOnly.Shape, archersOnly.Settings, profile).Exposure[arrow];
            var withGround = model.Predict(crowded.Genome, crowded.Shape, crowded.Settings, profile).Exposure[arrow];
            Assert.That(alone, Is.GreaterThan(0f));
            Assert.That(withGround, Is.EqualTo(alone).Within(1e-5));

            // The ground enemies still saturate among themselves: 5 spawners are less than 5x one spawner.
            var one = Room(ranges, 1, 0);
            var ground = (int)DamageSource.GroundEnemy;
            var eOne = model.Predict(one.Genome, one.Shape, one.Settings, profile).Exposure[ground];
            var eFive = model.Predict(crowded.Genome, crowded.Shape, crowded.Settings, profile).Exposure[ground];
            Assert.That(eFive, Is.GreaterThan(eOne));
            Assert.That(eFive, Is.LessThan(5f * eOne));
        }

        [Test]
        public void Calibration_PoolsTheRooms_AndDoesNotDependOnTheirOrder()
        {
            var settings = new EvolutionSettings
            {
                Profile =
                {
                    RunAlpha = 1f
                }
            };
            var ranges = new RoomAttributeRanges();
            var model = new RoomDifficultyModel(settings.BaseStats, settings.Model);

            var small = Room(ranges, 1, 1);
            var big = Room(ranges, 4, 4);
            var smallTelemetry = Survived(small, 10f, 20f);
            var bigTelemetry = Survived(big, 30f, 5f);

            var forward = PlayerProfile.CreateDefault(settings.Profile, settings.BaseStats);
            ProfileUpdater.UpdateFromRooms(forward, new[] { small, big }, new[] { smallTelemetry, bigTelemetry }, model, settings.Profile);
            var backward = PlayerProfile.CreateDefault(settings.Profile, settings.BaseStats);
            ProfileUpdater.UpdateFromRooms(backward, new[] { big, small }, new[] { bigTelemetry, smallTelemetry }, model, settings.Profile);

            Assert.That(backward.Weights, Is.EqualTo(forward.Weights));
            Assert.That(forward.RoomsObserved, Is.EqualTo(2));

            // With RunAlpha = 1 the weight is exactly sum(damage) / sum(exposure).
            var reference = PlayerProfile.CreateDefault(settings.Profile, settings.BaseStats);
            var arrow = (int)DamageSource.Arrow;
            var exposure = model.Exposure(small.Genome, small.Shape, small.Settings, reference, smallTelemetry.TimeSurvived)[arrow]
                           + model.Exposure(big.Genome, big.Shape, big.Settings, reference, bigTelemetry.TimeSurvived)[arrow];
            Assert.That(forward.Weights[arrow], Is.EqualTo(0.25f / exposure).Within(1e-5));
        }
        
        [Test]
        public void JsonNode_ReadsWhatJsonBuilderWrites()
        {
            var json = new JsonBuilder { ExactNumbers = true };
            json.BeginObject();
            json.Field("Text", "a \"quoted\" \\ text\nwith a new line");
            json.Field("Float", 0.1f);
            json.Field("Double", Math.PI);
            json.Field("Negative", -3);
            json.Field("Flag", true);
            json.Field("Values", new[] { 1.5f, -2.25f, 1e-7f });
            json.BeginArray("Empty");
            json.EndArray();
            json.EndObject();

            var node = JsonNode.Parse(json.ToString());
            Assert.That(node["Text"].AsString(), Is.EqualTo("a \"quoted\" \\ text\nwith a new line"));
            Assert.That(node["Float"].AsFloat(), Is.EqualTo(0.1f));
            Assert.That(node["Double"].AsDouble(), Is.EqualTo(Math.PI));
            Assert.That(node["Negative"].AsInt(), Is.EqualTo(-3));
            Assert.That(node["Flag"].AsBool(), Is.True);
            Assert.That(node["Values"].AsFloatArray(), Is.EqualTo(new[] { 1.5f, -2.25f, 1e-7f }));
            Assert.That(node["Empty"].Count, Is.EqualTo(0));
            Assert.That(node["DoesNotExist"].AsInt(42), Is.EqualTo(42));
        }
        
        [Test]
        public void Alpha_StartsHigh_AndFallsToRunAlpha()
        {
            var settings = new ProfileSettings { RunAlpha = 0.4f, FirstRunAlpha = 0.7f, AlphaWarmupRuns = 2 };
            Assert.That(ProfileUpdater.AlphaForRun(0, settings), Is.EqualTo(0.7f).Within(1e-6));
            Assert.That(ProfileUpdater.AlphaForRun(1, settings), Is.EqualTo(0.55f).Within(1e-6));
            Assert.That(ProfileUpdater.AlphaForRun(2, settings), Is.EqualTo(0.4f).Within(1e-6));
            Assert.That(ProfileUpdater.AlphaForRun(10, settings), Is.EqualTo(0.4f).Within(1e-6));

            // Never below RunAlpha (the tests that use RunAlpha = 1 stay exact).
            settings.RunAlpha = 1f;
            Assert.That(ProfileUpdater.AlphaForRun(0, settings), Is.EqualTo(1f));

            settings.RunAlpha = 0.4f;
            settings.AlphaWarmupRuns = 0;
            Assert.That(ProfileUpdater.AlphaForRun(0, settings), Is.EqualTo(0.4f));
        }
    }
}
