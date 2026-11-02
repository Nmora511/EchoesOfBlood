namespace Evolution.Telemetry
{
    // What hurt the player. The order is used as an array index, so only add new values at the end.
    public enum DamageSource
    {
        Unknown,
        GroundEnemy,
        Arrow,
        ThornTrap,
        StakesTrap,
        BossMelee,
        BossArea,
        BossRush
    }

    public enum EnemyKind
    {
        Ground,
        Ranged
    }

    public enum RoomOutcome
    {
        InProgress,
        Survived,
        Died
    }

    public static class TelemetryEnums
    {
        public const int DamageSourceCount = (int)DamageSource.BossRush + 1;
        public const int EnemyKindCount = (int)EnemyKind.Ranged + 1;
    }
}
