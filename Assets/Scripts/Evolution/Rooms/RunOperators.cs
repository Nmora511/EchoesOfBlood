using System;
using System.Collections.Generic;

namespace Evolution.Rooms
{
    // Crossover and mutation of RunGenome. Mutate also repairs, so every individual
    // the GA evaluates is playable. Only active cells are mutated: dormant genes are kept.
    public class RunOperators
    {
        private readonly bool[][] _shapes;
        private readonly RepairRules _rules;
        private readonly RoomGaSettings _settings;

        public RunOperators(bool[][] shapes, RepairRules rules, RoomGaSettings settings)
        {
            _shapes = shapes;
            _rules = rules;
            _settings = settings;
        }

        // For each of the 5 rooms: copy the whole room from one parent, or (BlockCrossoverRate)
        // mix them: each 3x3 block from one parent and the attributes blended.
        public RunGenome Crossover(RunGenome mother, RunGenome father, Random rng)
        {
            var child = new RunGenome();
            for (var room = 0; room < RunGenome.RoomCount; room++)
            {
                var a = mother.Rooms[room];
                var b = father.Rooms[room];

                if (rng.NextDouble() >= _settings.BlockCrossoverRate)
                {
                    child.Rooms[room] = (rng.Next(2) == 0 ? a : b).Clone();
                    continue;
                }

                var mixed = new RoomGenome();
                for (var block = 0; block < RoomLayout.BlockCount; block++)
                {
                    var source = rng.Next(2) == 0 ? a : b;
                    for (var cell = 0; cell < RoomLayout.CellsPerBlock; cell++)
                    {
                        var gene = RoomLayout.GeneIndex(block, cell);
                        mixed.Cells[gene] = source.Cells[gene];
                    }
                }

                // Arithmetic crossover: a random weighted average, the same weight for the whole room.
                var weight = (float)rng.NextDouble();
                for (var i = 0; i < mixed.Attributes.Length; i++)
                {
                    mixed.Attributes[i] = weight * a.Attributes[i] + (1f - weight) * b.Attributes[i];
                }

                child.Rooms[room] = mixed;
            }

            // Blocks from different parents can break the rules (path, spawner count), so repair now.
            child.Repair(_shapes, _rules, rng);
            return child;
        }

        public void Mutate(RunGenome run, Random rng)
        {
            for (var room = 0; room < RunGenome.RoomCount; room++)
            {
                var genome = run.Rooms[room];
                var active = ActiveCells(_shapes[room]);

                // 1. Change the type of a cell.
                foreach (var gene in active)
                {
                    if (rng.NextDouble() < _settings.CellMutationRate)
                    {
                        genome.Cells[gene] = RandomOtherType(genome.Cells[gene], rng);
                    }
                }

                // 2. Swap two cells (moves something without changing how many there are).
                if (active.Count > 1 && rng.NextDouble() < _settings.SwapMutationRate)
                {
                    var i = active[rng.Next(active.Count)];
                    var j = active[rng.Next(active.Count)];
                    (genome.Cells[i], genome.Cells[j]) = (genome.Cells[j], genome.Cells[i]);
                }

                // 3. Gaussian noise on the attributes, kept inside [0, 1].
                for (var i = 0; i < genome.Attributes.Length; i++)
                {
                    if (rng.NextDouble() >= _settings.AttributeMutationRate) continue;
                    var value = genome.Attributes[i] + _settings.AttributeSigma * Gaussian(rng);
                    genome.Attributes[i] = Math.Max(0f, Math.Min(1f, value));
                }

                RoomRepair.Repair(genome, _shapes[room], _rules, rng);
            }
        }

        private static List<int> ActiveCells(bool[] shape)
        {
            var cells = new List<int>(RoomLayout.CellCount);
            for (var gene = 0; gene < RoomLayout.CellCount; gene++)
            {
                if (RoomLayout.IsActive(gene, shape)) cells.Add(gene);
            }
            return cells;
        }

        private static CellType RandomOtherType(CellType current, Random rng)
        {
            // Picks one of the other CellTypes.Count - 1 types with equal chance.
            var offset = rng.Next(1, CellTypes.Count);
            return (CellType)(((int)current + offset) % CellTypes.Count);
        }

        // Standard normal sample.
        private static float Gaussian(Random rng)
        {
            var u1 = 1.0 - rng.NextDouble();
            var u2 = rng.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }
    }
}
