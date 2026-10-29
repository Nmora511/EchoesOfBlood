using System;
using System.Text;

namespace Evolution.Rooms
{
    // One room ready to be played: genome + shape + decoded values.
    [Serializable]
    public class RoomPlan
    {
        public RoomGenome Genome;
        public bool[] Shape;
        public RoomSettings Settings;

        public CellType CellAt(int block, int cell) => Genome.Cells[RoomLayout.GeneIndex(block, cell)];

        // Text map of the room for the Console: # = missing block, . = empty,
        // T = thorn trap, S = stakes trap, G = ground spawner, R = ranged spawner.
        public string Describe()
        {
            var text = new StringBuilder();
            for (var row = 0; row < RoomLayout.GridSide; row++)
            {
                for (var col = 0; col < RoomLayout.GridSide; col++)
                {
                    var gene = RoomLayout.FromRowCol(row, col);
                    text.Append(!RoomLayout.IsActive(gene, Shape) ? '#' : Symbol(Genome.Cells[gene]));
                }
                text.Append('\n');
            }
            return text.ToString();
        }

        private static char Symbol(CellType type)
        {
            switch (type)
            {
                case CellType.ThornTrap: return 'T';
                case CellType.StakesTrap: return 'S';
                case CellType.GroundEnemySpawner: return 'G';
                case CellType.RangedEnemySpawner: return 'R';
                default: return '.';
            }
        }
    }

    // The 5 rooms of a run.
    [Serializable]
    public class RunPlan
    {
        public const int RoomCount = 5;

        public RoomPlan[] Rooms = new RoomPlan[RoomCount];

        // Stage 2 only: random rooms (no evolution yet), repaired so they are playable.
        public static RunPlan CreateRandom(Random rng, RoomAttributeRanges ranges, RepairRules rules, int minBlocks)
        {
            var plan = new RunPlan();
            for (var i = 0; i < RoomCount; i++)
            {
                var shape = RoomShape.Generate(rng, minBlocks);
                var genome = RoomGenome.CreateRandom(rng);
                RoomRepair.Repair(genome, shape, rules, rng);

                plan.Rooms[i] = new RoomPlan
                {
                    Genome = genome,
                    Shape = shape,
                    Settings = ranges.Decode(genome)
                };
            }
            return plan;
        }
    }
}
