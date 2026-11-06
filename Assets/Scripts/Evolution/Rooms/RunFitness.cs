using System;
using System.Collections.Generic;
using Evolution.PlayerModel;

namespace Evolution.Rooms
{
    // Everything the room GA needs for one evolution step (between two runs).
    public class RoomEvolutionContext
    {
        public bool[][] Shapes;                 // the 5 shapes of the NEXT run, chosen before the GA
        public RoomAttributeRanges Ranges;
        public RepairRules Rules;
        public PlayerProfile Profile;
        public EvolutionSettings Settings;

        // Layouts of the last rooms played (most recent last), used by the novelty term.
        public List<CellType[]> RecentLayouts = new List<CellType[]>();
    }

    // Prediction of a whole run, for logging (predicted vs measured difficulty) and debugging.
    [Serializable]
    public class RunPrediction
    {
        public RoomPrediction[] Rooms = new RoomPrediction[RunGenome.RoomCount];
        public float Error;             // sum of |predicted - target| over the 5 rooms
        public float LethalExcess;      // sum of predicted damage above the lethal threshold
        public float Novelty;           // 0..1, mean distance to the recent rooms
        public double Fitness;
    }

    // F = - sum |D_prev,k - delta * T_k|  - LethalPenalty * lethal excess  + NoveltyWeight * novelty
    public class RunFitness
    {
        private readonly RoomEvolutionContext _context;
        private readonly RoomDifficultyModel _model;
        private readonly RoomFitnessSettings _settings;

        public RunFitness(RoomEvolutionContext context)
        {
            _context = context;
            _settings = context.Settings.RoomFitness;
            _model = new RoomDifficultyModel(context.Settings.BaseStats, context.Settings.Model);
        }

        public RoomDifficultyModel Model => _model;

        public double Evaluate(RunGenome run) => Predict(run).Fitness;

        public RunPrediction Predict(RunGenome run)
        {
            var result = new RunPrediction();
            var noveltySum = 0f;

            for (var k = 0; k < RunGenome.RoomCount; k++)
            {
                var roomGenome = run.Rooms[k];
                var roomShape = _context.Shapes[k];
                var roomSettings = _context.Ranges.Decode(roomGenome);

                var roomPrediction = _model.Predict(roomGenome, roomShape, roomSettings, _context.Profile);
                roomPrediction.Target = Target(k);
                result.Rooms[k] = roomPrediction;

                result.Error += Math.Abs(roomPrediction.Total - roomPrediction.Target);
                result.LethalExcess += Math.Max(0f, roomPrediction.Total - _settings.LethalThreshold);
                noveltySum += Novelty(roomGenome, roomShape);
            }

            result.Novelty = noveltySum / RunGenome.RoomCount;
            result.Fitness = -result.Error
                             - _settings.LethalPenalty * result.LethalExcess
                             + _settings.NoveltyWeight * result.Novelty;
            return result;
        }

        public float Target(int room)
        {
            var curve = _settings.TargetCurve;
            var value = curve != null && curve.Length > 0 ? curve[Math.Min(room, curve.Length - 1)] : 0f;
            return _context.Profile.Delta * value;
        }

        // Distance to the MOST similar recent room (0 = identical to one already played, 1 = all cells differ).
        private float Novelty(RoomGenome genome, bool[] shape)
        {
            var layouts = _context.RecentLayouts;
            if (layouts == null || layouts.Count == 0) return 0f;

            var nearest = 1f;
            foreach (var layout in layouts)
            {
                nearest = Math.Min(nearest, RunGenome.LayoutDistance(genome.Cells, layout, shape));
            }
            return nearest;
        }
    }
}
