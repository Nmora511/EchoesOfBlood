using System;

namespace Evolution.Rooms
{
    // Attributes shared by every element of the same kind inside one room.
    // Each gene is a number in [0, 1] that RoomAttributeRanges turns into a real value.
    public enum RoomAttribute
    {
        GroundHealth,
        GroundDamage,
        GroundMaxAlive,
        GroundSpawnInterval,
        GroundAttackCooldown,
        RangedHealth,
        RangedDamage,
        RangedMaxAlive,
        RangedSpawnInterval,
        RangedAttackCooldown,
        ThornDamage,
        StakesDamage,
        SurviveTime
    }

    // Genome of ONE room: 81 cell genes + one gene per RoomAttribute.
    // The room shape (which blocks exist) is NOT part of the genome: it is chosen outside the GA.
    [Serializable]
    public class RoomGenome
    {
        public static readonly int AttributeCount = Enum.GetValues(typeof(RoomAttribute)).Length;

        public CellType[] Cells = new CellType[RoomLayout.CellCount];
        public float[] Attributes = new float[AttributeCount];

        public float this[RoomAttribute attribute]
        {
            get => Attributes[(int)attribute];
            set => Attributes[(int)attribute] = value;
        }

        public static RoomGenome CreateRandom(Random rng, float emptyChance = 0.75f)
        {
            var genome = new RoomGenome();
            for (var i = 0; i < genome.Cells.Length; i++)
            {
                // Mostly empty cells, otherwise any non-empty type with equal chance.
                genome.Cells[i] = rng.NextDouble() < emptyChance
                    ? CellType.Empty
                    : (CellType)rng.Next(1, CellTypes.Count);
            }

            for (var i = 0; i < genome.Attributes.Length; i++)
            {
                genome.Attributes[i] = (float)rng.NextDouble();
            }

            return genome;
        }

        public RoomGenome Clone()
        {
            return new RoomGenome
            {
                Cells = (CellType[])Cells.Clone(),
                Attributes = (float[])Attributes.Clone()
            };
        }
    }
}
