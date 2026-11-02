using System;
using System.Text;

namespace Evolution.Telemetry
{
    // Everything measured while the player was inside one room.
    // Pure C#: the Unity side (GameTelemetry) feeds it, the GA reads it.
    [Serializable]
    public class RoomTelemetry
    {
        // ---- Identification ----
        public int RunNumber;
        public int RoomIndex;          // 0..4
        public string Map;             // RoomPlan.Describe() in one line, rows separated by '/'
        public float[] Genes;          // the room's 13 attribute genes (0..1)

        // ---- Result ----
        public RoomOutcome Outcome = RoomOutcome.InProgress;
        public float PlannedTime;      // SurviveTime decoded from the genome
        public float TimeSurvived;     // seconds actually played in the room
        public float PlayerMaxHealth;
        public float FinalHealth;

        // ---- Damage taken (indexed by DamageSource) ----
        public float[] DamageTaken = new float[TelemetryEnums.DamageSourceCount];
        public int[] HitsTaken = new int[TelemetryEnums.DamageSourceCount];
        public int[] TrapSteps = new int[TelemetryEnums.DamageSourceCount]; // only ThornTrap / StakesTrap are used
        public float LowHealthThreshold = 0.3f;
        public float TimeBelowLowHealth;

        // ---- Enemies (indexed by EnemyKind) ----
        public int[] EnemiesSpawned = new int[TelemetryEnums.EnemyKindCount];
        public int[] EnemiesKilled = new int[TelemetryEnums.EnemyKindCount];
        public float[] KilledEnemyLifetime = new float[TelemetryEnums.EnemyKindCount]; // sum, see AverageEnemyLifetime
        public int MaxEnemiesAlive;
        public float EnemyAliveSeconds; // integral of "enemies alive" over time, see AverageEnemiesAlive
        public float TimeNearEnemies;   // seconds with at least one enemy inside the near radius

        // ---- Player actions ----
        public int Attacks;
        public int AttackHits;          // one swing that hits two enemies counts twice
        public float DamageDealt;
        public int Dashes;
        public float DistanceTraveled;
        public float TimeStill;

        private int _enemiesAlive;

        public RoomTelemetry(int runNumber, int roomIndex, float plannedTime, float playerMaxHealth)
        {
            RunNumber = runNumber;
            RoomIndex = roomIndex;
            PlannedTime = plannedTime;
            PlayerMaxHealth = playerMaxHealth;
            FinalHealth = playerMaxHealth;
        }

        public bool IsFinished => Outcome != RoomOutcome.InProgress;
        public int EnemiesAlive => _enemiesAlive;

        // ---------------- Recording ----------------

        public void RecordDamage(DamageSource source, float amount)
        {
            if (IsFinished || amount <= 0f) return;
            DamageTaken[(int)source] += amount;
            HitsTaken[(int)source]++;
        }

        public void RecordTrapStep(DamageSource trap)
        {
            if (IsFinished) return;
            TrapSteps[(int)trap]++;
        }

        public void RecordAttack()
        {
            if (!IsFinished) Attacks++;
        }

        public void RecordAttackHit(float damage)
        {
            if (IsFinished) return;
            AttackHits++;
            DamageDealt += damage;
        }

        public void RecordDash()
        {
            if (!IsFinished) Dashes++;
        }

        public void RecordEnemySpawned(EnemyKind kind)
        {
            if (IsFinished) return;
            EnemiesSpawned[(int)kind]++;
            _enemiesAlive++;
            if (_enemiesAlive > MaxEnemiesAlive) MaxEnemiesAlive = _enemiesAlive;
        }

        public void RecordEnemyKilled(EnemyKind kind, float lifetime)
        {
            if (IsFinished) return;
            EnemiesKilled[(int)kind]++;
            KilledEnemyLifetime[(int)kind] += lifetime;
            _enemiesAlive = Math.Max(0, _enemiesAlive - 1);
        }

        // Called every frame while the room is running.
        public void RecordFrame(float deltaTime, float playerHealth, bool enemyNear, float distanceMoved, bool playerStill)
        {
            if (IsFinished || deltaTime <= 0f) return;

            TimeSurvived += deltaTime;
            EnemyAliveSeconds += _enemiesAlive * deltaTime;
            DistanceTraveled += distanceMoved;

            if (PlayerMaxHealth > 0f && playerHealth / PlayerMaxHealth < LowHealthThreshold) TimeBelowLowHealth += deltaTime;
            if (enemyNear) TimeNearEnemies += deltaTime;
            if (playerStill) TimeStill += deltaTime;
        }

        public void Finish(RoomOutcome outcome, float finalHealth)
        {
            if (IsFinished) return;
            Outcome = outcome;
            FinalHealth = Math.Max(0f, finalHealth);
        }

        // ---------------- Derived values (what the fitness will use) ----------------

        // Damage from one source as a fraction of the player's max health (0.25 = a quarter of the bar).
        public float DamageFraction(DamageSource source) =>
            PlayerMaxHealth > 0f ? DamageTaken[(int)source] / PlayerMaxHealth : 0f;

        public float TotalDamage
        {
            get
            {
                var total = 0f;
                foreach (var d in DamageTaken) total += d;
                return total;
            }
        }

        public float TotalDamageFraction => PlayerMaxHealth > 0f ? TotalDamage / PlayerMaxHealth : 0f;
        public float FinalHealthFraction => PlayerMaxHealth > 0f ? FinalHealth / PlayerMaxHealth : 0f;

        // Fraction of the room the player lived through (1 = survived the whole timer).
        public float SurvivalFraction =>
            Outcome == RoomOutcome.Survived ? 1f : PlannedTime > 0f ? Math.Min(1f, TimeSurvived / PlannedTime) : 0f;

        public float AverageEnemiesAlive => TimeSurvived > 0f ? EnemyAliveSeconds / TimeSurvived : 0f;

        public float AverageEnemyLifetime(EnemyKind kind) =>
            EnemiesKilled[(int)kind] > 0 ? KilledEnemyLifetime[(int)kind] / EnemiesKilled[(int)kind] : 0f;

        public float HitRate => Attacks > 0 ? (float)AttackHits / Attacks : 0f;

        public float DamagePerSecond => TimeSurvived > 0f ? TotalDamage / TimeSurvived : 0f;

        // ---------------- Output ----------------

        // Short multi-line text for the Console.
        public string Summary()
        {
            var text = new StringBuilder();
            text.Append($"Room {RoomIndex + 1} (run {RunNumber}): {Outcome}, {TimeSurvived:0.0}/{PlannedTime:0.0}s, ");
            text.Append($"HP {FinalHealth:0.#}/{PlayerMaxHealth:0.#}, damage taken {TotalDamageFraction:P0} of max HP\n");

            text.Append("  Damage by source:");
            for (var i = 0; i < TelemetryEnums.DamageSourceCount; i++)
            {
                if (HitsTaken[i] == 0) continue;
                text.Append($" {(DamageSource)i}={DamageTaken[i] / Math.Max(PlayerMaxHealth, 1e-6f):P0} ({HitsTaken[i]} hits)");
            }
            text.Append('\n');

            text.Append($"  Traps stepped: thorn={TrapSteps[(int)DamageSource.ThornTrap]} stakes={TrapSteps[(int)DamageSource.StakesTrap]}, ");
            text.Append($"low HP time={TimeBelowLowHealth:0.0}s\n");

            text.Append($"  Enemies: ground {EnemiesKilled[0]}/{EnemiesSpawned[0]} killed, ranged {EnemiesKilled[1]}/{EnemiesSpawned[1]} killed, ");
            text.Append($"alive avg={AverageEnemiesAlive:0.0} max={MaxEnemiesAlive}, near time={TimeNearEnemies:0.0}s\n");

            text.Append($"  Player: attacks={Attacks} hits={AttackHits} ({HitRate:P0}), dashes={Dashes}, ");
            text.Append($"moved={DistanceTraveled:0}m, still={TimeStill:0.0}s");
            return text.ToString();
        }

        public string ToJson()
        {
            var json = new JsonBuilder();
            json.BeginObject();
            json.Field("RunNumber", RunNumber);
            json.Field("Room", RoomIndex + 1);
            json.Field("Map", Map);
            json.Field("Genes", Genes);
            json.Field("Outcome", Outcome.ToString());
            json.Field("PlannedTime", PlannedTime);
            json.Field("TimeSurvived", TimeSurvived);
            json.Field("PlayerMaxHealth", PlayerMaxHealth);
            json.Field("FinalHealth", FinalHealth);
            json.Field("TotalDamageFraction", TotalDamageFraction);

            json.BeginObject("DamageBySource");
            for (var i = 0; i < TelemetryEnums.DamageSourceCount; i++)
            {
                if (HitsTaken[i] == 0) continue;
                json.BeginObject(((DamageSource)i).ToString());
                json.Field("Damage", DamageTaken[i]);
                json.Field("Fraction", DamageFraction((DamageSource)i));
                json.Field("Hits", HitsTaken[i]);
                json.EndObject();
            }
            json.EndObject();

            json.Field("ThornTrapSteps", TrapSteps[(int)DamageSource.ThornTrap]);
            json.Field("StakesTrapSteps", TrapSteps[(int)DamageSource.StakesTrap]);
            json.Field("TimeBelowLowHealth", TimeBelowLowHealth);

            for (var k = 0; k < TelemetryEnums.EnemyKindCount; k++)
            {
                var kind = (EnemyKind)k;
                json.BeginObject($"{kind}Enemies");
                json.Field("Spawned", EnemiesSpawned[k]);
                json.Field("Killed", EnemiesKilled[k]);
                json.Field("AverageLifetime", AverageEnemyLifetime(kind));
                json.EndObject();
            }

            json.Field("MaxEnemiesAlive", MaxEnemiesAlive);
            json.Field("AverageEnemiesAlive", AverageEnemiesAlive);
            json.Field("TimeNearEnemies", TimeNearEnemies);
            json.Field("Attacks", Attacks);
            json.Field("AttackHits", AttackHits);
            json.Field("DamageDealt", DamageDealt);
            json.Field("Dashes", Dashes);
            json.Field("DistanceTraveled", DistanceTraveled);
            json.Field("TimeStill", TimeStill);
            json.EndObject();
            return json.ToString();
        }
    }
}
