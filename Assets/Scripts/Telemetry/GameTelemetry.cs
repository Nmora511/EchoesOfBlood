using System.Collections.Generic;
using Enemies;
using Evolution.Rooms;
using Evolution.Telemetry;
using UnityEngine;
using PlayerClass = Player.Player;

// Unity side of the telemetry. Gameplay scripts report what happened here, and the data
// goes into the current RoomTelemetry (pure C#, in the Evolution assembly).
// Events that arrive while no room is being recorded (boss room, after the timer ended) are ignored.
namespace Telemetry
{
    public static class GameTelemetry
    {
        public static RoomTelemetry CurrentRoom { get; private set; }
        public static bool IsRecording => CurrentRoom is { IsFinished: false };

        // Spawn time of each enemy still alive, used for the lifetime and for "enemy near the player".
        private static readonly Dictionary<Enemy, float> AliveEnemies = new Dictionary<Enemy, float>();
        private static readonly List<Enemy> DeadKeys = new List<Enemy>();

        private static float _nearRadius = 4f;
        private static float _stillSpeed = 0.5f;
        private static Vector3 _lastPlayerPosition;
        private static bool _hasLastPosition;
        private static bool _warnedUnknownSource;

        // Statics survive between Plays when Domain Reload is off, so they are cleared here.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CurrentRoom = null;
            AliveEnemies.Clear();
            _hasLastPosition = false;
            _warnedUnknownSource = false;
        }

        // ---------------- Room lifecycle (called by RoomManager) ----------------

        public static void BeginRoom(int runNumber, int roomIndex, RoomPlan plan, float plannedTime,
            float playerMaxHealth, GameBalanceConfig config)
        {
            AliveEnemies.Clear();
            _hasLastPosition = false;

            if (config != null)
            {
                _nearRadius = config.TelemetryNearRadius;
                _stillSpeed = config.StillSpeed;
            }

            CurrentRoom = new RoomTelemetry(runNumber, roomIndex, plannedTime, playerMaxHealth)
            {
                LowHealthThreshold = config != null ? config.LowHealthThreshold : 0.3f,
                Map = plan?.Describe().TrimEnd('\n').Replace('\n', '/'),
                Genes = (float[])plan?.Genome.Attributes.Clone()
            };
        }

        // Closes the room and returns its data. Later events are ignored.
        public static RoomTelemetry EndRoom(RoomOutcome outcome)
        {
            if (CurrentRoom == null) return null;

            var player = PlayerClass.PlayerInstance;
            CurrentRoom.Finish(outcome, player != null ? player.HealthPoints : 0f);
            AliveEnemies.Clear();
            return CurrentRoom;
        }

        // Called every frame by RoomManager while the timer is running.
        public static void Tick(float deltaTime)
        {
            if (!IsRecording) return;

            var player = PlayerClass.PlayerInstance;
            if (player == null) return;

            var position = player.transform.position;
            position.y = 0f;

            var moved = _hasLastPosition ? Vector3.Distance(position, _lastPlayerPosition) : 0f;
            _lastPlayerPosition = position;
            _hasLastPosition = true;

            var isStill = moved < _stillSpeed * deltaTime;
            CurrentRoom.RecordFrame(deltaTime, player.HealthPoints, IsAnyEnemyNear(position), moved, isStill);
        }

        // ---------------- Player ----------------

        public static void PlayerDamaged(DamageSource source, float amount)
        {
            if (!IsRecording) return;

            if (source == DamageSource.Unknown && !_warnedUnknownSource)
            {
                _warnedUnknownSource = true;
                Debug.LogWarning("[Telemetry] The player took damage from a hitbox with Source = Unknown. " +
                                 "Set the Source field of that AttackHitbox / DamageHitbox.");
            }

            CurrentRoom.RecordDamage(source, amount);
        }

        public static void PlayerAttacked()
        {
            if (IsRecording) CurrentRoom.RecordAttack();
        }

        public static void PlayerHitEnemy(float damage)
        {
            if (IsRecording) CurrentRoom.RecordAttackHit(damage);
        }

        public static void PlayerDashed()
        {
            if (IsRecording) CurrentRoom.RecordDash();
        }

        public static void TrapStepped(DamageSource trap)
        {
            if (IsRecording) CurrentRoom.RecordTrapStep(trap);
        }

        // ---------------- Enemies (called by EnemySpawner) ----------------

        public static void EnemySpawned(Enemy enemy)
        {
            if (!IsRecording || enemy == null) return;
            AliveEnemies[enemy] = Time.time;
            CurrentRoom.RecordEnemySpawned(KindOf(enemy));
        }

        public static void EnemyDied(Enemy enemy)
        {
            if (!IsRecording || enemy == null) return;
            if (!AliveEnemies.Remove(enemy, out var spawnTime)) return;

            CurrentRoom.RecordEnemyKilled(KindOf(enemy), Time.time - spawnTime);
        }

        private static EnemyKind KindOf(Enemy enemy) => enemy is RangedEnemy ? EnemyKind.Ranged : EnemyKind.Ground;

        private static bool IsAnyEnemyNear(Vector3 playerPosition)
        {
            var isNear = false;
            var nearSqr = _nearRadius * _nearRadius;

            foreach (var enemy in AliveEnemies.Keys)
            {
                // Destroyed without dying (should not happen, but a destroyed key would stay forever).
                if (enemy == null)
                {
                    DeadKeys.Add(enemy);
                    continue;
                }

                var offset = enemy.transform.position - playerPosition;
                offset.y = 0f;
                if (offset.sqrMagnitude <= nearSqr) isNear = true;
            }

            foreach (var key in DeadKeys) AliveEnemies.Remove(key);
            DeadKeys.Clear();
            return isNear;
        }
    }
}
