using System;
using Evolution.Telemetry;

namespace Evolution.PlayerModel
{
    // What the system has learned about this player. Calibrated by ProfileUpdater after every
    // room, and used by the fitness to PREDICT how hard a room it never played will be for them.
    [Serializable]
    public class PlayerProfile
    {
        // Per DamageSource: fraction of the potential damage of that source that actually hits this player
        // (0.2 = one in five enemy attacks / trap chances lands). Lower = the player handles that source well.
        public float[] Weights = new float[TelemetryEnums.DamageSourceCount];

        // Per EnemyKind: seconds an enemy with health multiplier 1 stays alive (how fast the player kills).
        public float[] BaseLifetime = new float[TelemetryEnums.EnemyKindCount];

        // Global difficulty scale applied to the target curve.
        public float Delta = 1f;

        public float PlayerMaxHealth = 100f;
        public int RoomsObserved;
        public int RunsObserved;

        public float Weight(DamageSource source) => Weights[(int)source];
        public float Lifetime(EnemyKind kind) => BaseLifetime[(int)kind];

        public static PlayerProfile CreateDefault(ProfileSettings settings, BaseStats stats)
        {
            var profile = new PlayerProfile { PlayerMaxHealth = stats.PlayerMaxHealth };
            profile.Weights[(int)DamageSource.GroundEnemy] = settings.DefaultEnemyWeight;
            profile.Weights[(int)DamageSource.Arrow] = settings.DefaultArrowWeight;
            profile.Weights[(int)DamageSource.ThornTrap] = settings.DefaultTrapWeight;
            profile.Weights[(int)DamageSource.StakesTrap] = settings.DefaultTrapWeight;
            profile.BaseLifetime[(int)EnemyKind.Ground] = settings.DefaultGroundLifetime;
            profile.BaseLifetime[(int)EnemyKind.Ranged] = settings.DefaultRangedLifetime;
            return profile;
        }

        public PlayerProfile Clone()
        {
            var copy = (PlayerProfile)MemberwiseClone();
            copy.Weights = (float[])Weights.Clone();
            copy.BaseLifetime = (float[])BaseLifetime.Clone();
            return copy;
        }

        public override string ToString() =>
            $"w ground={Weight(DamageSource.GroundEnemy):0.000} arrow={Weight(DamageSource.Arrow):0.000} " +
            $"thorn={Weight(DamageSource.ThornTrap):0.000} stakes={Weight(DamageSource.StakesTrap):0.000}, " +
            $"lifetime ground={Lifetime(EnemyKind.Ground):0.0}s ranged={Lifetime(EnemyKind.Ranged):0.0}s, " +
            $"delta={Delta:0.00} (rooms {RoomsObserved}, runs {RunsObserved})";
    }
}
