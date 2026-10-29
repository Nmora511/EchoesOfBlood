namespace Evolution.Rooms
{
    // What a single grid cell holds. The order MUST match Grid.GridUnit.UnitType,
    // because the Unity side converts between them with a cast.
    public enum CellType
    {
        Empty,
        ThornTrap,
        StakesTrap,
        GroundEnemySpawner,
        RangedEnemySpawner
    }

    public static class CellTypes
    {
        public const int Count = 5;

        public static bool IsTrap(CellType type) => type is CellType.ThornTrap or CellType.StakesTrap;

        public static bool IsSpawner(CellType type) =>
            type is CellType.GroundEnemySpawner or CellType.RangedEnemySpawner;
    }
}
