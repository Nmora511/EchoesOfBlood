using System;

namespace Evolution.Rooms
{
    // Real values for one kind of enemy spawner, decoded from the genome.
    [Serializable]
    public struct SpawnerSettings
    {
        public float HealthMultiplier;   // multiplies the prefab's HealthPoints
        public float DamageMultiplier;   // multiplies the prefab's AttackDamage
        public int MaxAlive;             // enemies alive at the same time, per spawner
        public float SpawnInterval;      // seconds between spawns
        public float AttackCooldown;     // seconds, replaces the prefab's AttackCooldown
    }

    // Everything the scene needs to apply the attribute genes of one room.
    [Serializable]
    public class RoomSettings
    {
        public SpawnerSettings Ground;
        public SpawnerSettings Ranged;
        public float ThornDamageMultiplier;
        public float StakesDamageMultiplier;
        public float SurviveTime;
    }

    [Serializable]
    public struct FloatRange
    {
        public float Min;
        public float Max;

        public FloatRange(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public float Lerp(float t) => Min + (Max - Min) * Math.Max(0f, Math.Min(1f, t));
    }

    // Maps each attribute gene from [0, 1] to a real value. Editable in the Inspector through GameBalanceConfig.
    [Serializable]
    public class RoomAttributeRanges
    {
        public FloatRange EnemyHealthMultiplier = new FloatRange(0.6f, 2f);
        public FloatRange EnemyDamageMultiplier = new FloatRange(0.6f, 2f);
        public FloatRange GroundMaxAlive = new FloatRange(1f, 3f);
        public FloatRange RangedMaxAlive = new FloatRange(1f, 3f);
        public FloatRange SpawnInterval = new FloatRange(1f, 8f);
        public FloatRange GroundAttackCooldown = new FloatRange(1.5f, 4f);
        public FloatRange RangedAttackCooldown = new FloatRange(1.5f, 5f);
        public FloatRange TrapDamageMultiplier = new FloatRange(0.5f, 2f);
        public FloatRange SurviveTime = new FloatRange(45f, 120f);

        public RoomSettings Decode(RoomGenome genome)
        {
            return new RoomSettings
            {
                Ground = new SpawnerSettings
                {
                    HealthMultiplier = EnemyHealthMultiplier.Lerp(genome[RoomAttribute.GroundHealth]),
                    DamageMultiplier = EnemyDamageMultiplier.Lerp(genome[RoomAttribute.GroundDamage]),
                    MaxAlive = (int)Math.Round(GroundMaxAlive.Lerp(genome[RoomAttribute.GroundMaxAlive])),
                    SpawnInterval = SpawnInterval.Lerp(genome[RoomAttribute.GroundSpawnInterval]),
                    AttackCooldown = GroundAttackCooldown.Lerp(genome[RoomAttribute.GroundAttackCooldown])
                },
                Ranged = new SpawnerSettings
                {
                    HealthMultiplier = EnemyHealthMultiplier.Lerp(genome[RoomAttribute.RangedHealth]),
                    DamageMultiplier = EnemyDamageMultiplier.Lerp(genome[RoomAttribute.RangedDamage]),
                    MaxAlive = (int)Math.Round(RangedMaxAlive.Lerp(genome[RoomAttribute.RangedMaxAlive])),
                    SpawnInterval = SpawnInterval.Lerp(genome[RoomAttribute.RangedSpawnInterval]),
                    AttackCooldown = RangedAttackCooldown.Lerp(genome[RoomAttribute.RangedAttackCooldown])
                },
                ThornDamageMultiplier = TrapDamageMultiplier.Lerp(genome[RoomAttribute.ThornDamage]),
                StakesDamageMultiplier = TrapDamageMultiplier.Lerp(genome[RoomAttribute.StakesDamage]),
                SurviveTime = SurviveTime.Lerp(genome[RoomAttribute.SurviveTime])
            };
        }
    }
}
