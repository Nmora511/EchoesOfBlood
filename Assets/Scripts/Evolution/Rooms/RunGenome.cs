using System;

namespace Evolution.Rooms
{
    // One individual of the room GA: the 5 rooms of a run.
    // The shapes are not part of it: they are chosen before each evolution and passed separately.
    [Serializable]
    public class RunGenome
    {
        public const int RoomCount = RunPlan.RoomCount;

        public RoomGenome[] Rooms = new RoomGenome[RoomCount];

        public static RunGenome CreateRandom(Random rng, float emptyChance = 0.75f)
        {
            var run = new RunGenome();
            for (var i = 0; i < RoomCount; i++) run.Rooms[i] = RoomGenome.CreateRandom(rng, emptyChance);
            return run;
        }

        public RunGenome Clone()
        {
            var copy = new RunGenome();
            for (var i = 0; i < RoomCount; i++) copy.Rooms[i] = Rooms[i].Clone();
            return copy;
        }

        // Repairs every room against the shape it will be played with.
        public void Repair(bool[][] shapes, RepairRules rules, Random rng)
        {
            for (var i = 0; i < RoomCount; i++) RoomRepair.Repair(Rooms[i], shapes[i], rules, rng);
        }

        // Fraction (0..1) of active cells that differ between two rooms, counting only the cells active in shape.
        public static float LayoutDistance(CellType[] a, CellType[] b, bool[] shape)
        {
            var active = 0;
            var different = 0;
            for (var gene = 0; gene < RoomLayout.CellCount; gene++)
            {
                if (!RoomLayout.IsActive(gene, shape)) continue;
                active++;
                if (a[gene] != b[gene]) different++;
            }
            return active > 0 ? (float)different / active : 0f;
        }
    }
}
