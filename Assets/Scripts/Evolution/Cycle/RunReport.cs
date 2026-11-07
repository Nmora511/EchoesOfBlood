using System;
using System.Collections.Generic;
using Evolution.Persistence;
using Evolution.PlayerModel;
using Evolution.Rooms;
using Evolution.Telemetry;

namespace Evolution.Cycle
{
    // Predicted vs measured for one played room, in fractions of the player's max health.
    [Serializable]
    public class RoomComparison
    {
        public int RoomIndex;
        public RoomOutcome Outcome;
        public float PlannedTime;
        public float TimeSurvived;

        public float Target;                  // delta * TargetCurve[room], what the GA aimed for
        public float PredictedPlanned;        // model prediction for the full SurviveTime (what the fitness used)
        public float PredictedAtPlayedTime;   // same model, but for the seconds actually played
        public float Observed;                // health fraction really lost

        public float[] PredictedBySource = new float[TelemetryEnums.DamageSourceCount]; // at played time
        public float[] ObservedBySource = new float[TelemetryEnums.DamageSourceCount];

        // Model error: positive = the room hurt more than the model expected.
        public float Error => Observed - PredictedAtPlayedTime;

        // How far the room was from what the GA wanted (positive = harder than the target).
        public float TargetError => Observed - Target;
    }

    // Everything about one finished run: the chain run N -> telemetry N -> evolution -> run N+1.
    // ValidationLog writes it to disk.
    public class RunReport
    {
        public string ParticipantId;
        public int RunNumber;
        public int Seed;

        // The run that was played.
        public RunPlan PlayedPlan;
        public double PlayedPlanFitness;
        public int PlayedPlanGeneration;
        public RunPrediction PlayedPrediction;
        public RunTelemetry Telemetry;
        public List<RoomComparison> Rooms = new List<RoomComparison>();

        // The player model before and after learning from this run.
        public PlayerProfile ProfileBefore;
        public PlayerProfile ProfileAfter;

        // The evolution done after the run.
        public RoomEvolutionResult Evolution;
        public int GenerationFrom;
        public int GenerationTo;

        // The run chosen to be played next.
        public int NextRunNumber;
        public RunPlan NextPlan;
        public RunPrediction NextPrediction;

        // The state after the cycle (what gets saved).
        public EvolutionState State;

        // Filled by the caller if writing the log failed (the state is saved anyway).
        public string LogError;

        public float MeanAbsoluteError
        {
            get
            {
                if (Rooms.Count == 0) return 0f;
                var sum = 0f;
                foreach (var room in Rooms) sum += Math.Abs(room.Error);
                return sum / Rooms.Count;
            }
        }

        public float MeanObserved
        {
            get
            {
                if (Rooms.Count == 0) return 0f;
                var sum = 0f;
                foreach (var room in Rooms) sum += room.Observed;
                return sum / Rooms.Count;
            }
        }

        public string Summary() =>
            $"Run {RunNumber} -> run {NextRunNumber}: generations {GenerationFrom}->{GenerationTo} in " +
            $"{Evolution.ElapsedMs:0} ms, best fitness {Evolution.Population[0].Fitness:0.000}, " +
            $"delta {ProfileBefore.Delta:0.00}->{ProfileAfter.Delta:0.00}, " +
            $"model error {MeanAbsoluteError:0.000} (mean |observed - predicted| in {Rooms.Count} rooms)";

        // ---------------- JSON of the whole chain (one file per run) ----------------

        public string ToJson()
        {
            var json = new JsonBuilder();
            json.BeginObject();
            json.Field("ParticipantId", ParticipantId);
            json.Field("RunNumber", RunNumber);
            json.Field("Seed", Seed);
            json.Field("Won", Telemetry.Won);
            json.Field("EndedInRoom", Telemetry.EndedInRoom);

            json.BeginObject("Plan");
            json.Field("Generation", PlayedPlanGeneration);
            json.Field("Fitness", PlayedPlanFitness);
            json.BeginArray("Rooms");
            for (var k = 0; k < RunPlan.RoomCount; k++)
            {
                var room = PlayedPlan.Rooms[k];
                json.BeginObject();
                json.Field("Room", k + 1);
                json.Field("Played", WasPlayed(k));
                WriteRoomPlan(json, room);
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();

            json.BeginObject("Prediction");
            json.Field("Fitness", PlayedPrediction.Fitness);
            json.Field("TargetError", PlayedPrediction.Error);
            json.Field("LethalExcess", PlayedPrediction.LethalExcess);
            json.Field("Novelty", PlayedPrediction.Novelty);
            json.BeginArray("Rooms");
            for (var k = 0; k < RunPlan.RoomCount; k++)
            {
                json.BeginObject();
                json.Field("Room", k + 1);
                WriteRoomPrediction(json, PlayedPrediction.Rooms[k]);
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();

            json.BeginArray("PredictedVsObserved");
            foreach (var room in Rooms)
            {
                json.BeginObject();
                json.Field("Room", room.RoomIndex + 1);
                json.Field("Outcome", room.Outcome.ToString());
                json.Field("PlannedTime", room.PlannedTime);
                json.Field("TimeSurvived", room.TimeSurvived);
                json.Field("Target", room.Target);
                json.Field("PredictedPlanned", room.PredictedPlanned);
                json.Field("PredictedAtPlayedTime", room.PredictedAtPlayedTime);
                json.Field("Observed", room.Observed);
                json.Field("Error", room.Error);
                json.BeginObject("BySource");
                foreach (var source in ProfileUpdater.RoomSources)
                {
                    json.BeginObject(source.ToString());
                    json.Field("Predicted", room.PredictedBySource[(int)source]);
                    json.Field("Observed", room.ObservedBySource[(int)source]);
                    json.EndObject();
                }
                json.EndObject();
                json.EndObject();
            }
            json.EndArray();

            json.Field("MeanAbsoluteError", MeanAbsoluteError);

            json.RawField("Telemetry", Telemetry.ToJson());
            StateSerializer.WriteProfile(json, "ProfileBefore", ProfileBefore);
            StateSerializer.WriteProfile(json, "ProfileAfter", ProfileAfter);

            json.BeginObject("Evolution");
            json.Field("GenerationFrom", GenerationFrom);
            json.Field("GenerationTo", GenerationTo);
            json.Field("ElapsedMs", Evolution.ElapsedMs);
            json.Field("PopulationSize", Evolution.Population.Count);
            json.BeginArray("Convergence");
            foreach (var stats in Evolution.Generations)
            {
                json.BeginObject();
                json.Field("Generation", GenerationFrom + stats.Generation);
                json.Field("Best", stats.Best);
                json.Field("Mean", stats.Mean);
                json.Field("Worst", stats.Worst);
                json.Field("Diversity", stats.Diversity);
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();

            json.BeginObject("Next");
            json.Field("RunNumber", NextRunNumber);
            json.Field("Fitness", NextPrediction.Fitness);
            json.Field("TargetError", NextPrediction.Error);
            json.BeginArray("Rooms");
            for (var k = 0; k < RunPlan.RoomCount; k++)
            {
                json.BeginObject();
                json.Field("Room", k + 1);
                WriteRoomPlan(json, NextPlan.Rooms[k]);
                WriteRoomPrediction(json, NextPrediction.Rooms[k]);
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();
            json.EndObject();

            return json.ToString();
        }

        private bool WasPlayed(int roomIndex)
        {
            foreach (var room in Rooms)
            {
                if (room.RoomIndex == roomIndex) return true;
            }
            return false;
        }

        private static void WriteRoomPlan(JsonBuilder json, RoomPlan room)
        {
            json.Field("Shape", RoomShape.Describe(room.Shape));
            json.Field("Map", room.Describe().TrimEnd('\n').Replace('\n', '/'));
            json.Field("Cells", StateSerializer.CellsToText(room.Genome.Cells));
            json.Field("Genes", room.Genome.Attributes);

            var s = room.Settings;
            json.BeginObject("Decoded");
            WriteSpawner(json, "Ground", s.Ground);
            WriteSpawner(json, "Ranged", s.Ranged);
            json.Field("ThornDamageMultiplier", s.ThornDamageMultiplier);
            json.Field("StakesDamageMultiplier", s.StakesDamageMultiplier);
            json.Field("SurviveTime", s.SurviveTime);
            json.EndObject();
        }

        private static void WriteSpawner(JsonBuilder json, string name, SpawnerSettings spawner)
        {
            json.BeginObject(name);
            json.Field("HealthMultiplier", spawner.HealthMultiplier);
            json.Field("DamageMultiplier", spawner.DamageMultiplier);
            json.Field("MaxAlive", spawner.MaxAlive);
            json.Field("SpawnInterval", spawner.SpawnInterval);
            json.Field("AttackCooldown", spawner.AttackCooldown);
            json.EndObject();
        }

        private static void WriteRoomPrediction(JsonBuilder json, RoomPrediction prediction)
        {
            json.Field("Target", prediction.Target);
            json.Field("Predicted", prediction.Total);
            json.BeginObject("PredictedBySource");
            foreach (var source in ProfileUpdater.RoomSources)
            {
                json.Field(source.ToString(), prediction.Damage[(int)source]);
            }
            json.EndObject();
        }
    }
}
