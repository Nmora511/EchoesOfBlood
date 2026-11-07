using System;
using Evolution.PlayerModel;
using Evolution.Telemetry;

namespace Evolution.Rooms
{
    // Prediction of one room: exposure and predicted damage per source, in fractions of the player's max health.
    [Serializable]
    public class RoomPrediction
    {
        public float[] Exposure = new float[TelemetryEnums.DamageSourceCount];
        public float[] Damage = new float[TelemetryEnums.DamageSourceCount];
        public float Total;     // predicted health fraction lost in the room (D_prev)
        public float Target;    // delta * TargetCurve[room], filled by RunFitness
    }

    // The player model of the GA: how much damage a room should do to THIS player.
    //
    // Exposure of a source = the damage it could do if every attack / trap chance landed,
    // as a fraction of the player's max health. It depends only on the genome (and the profile's kill speed).
    // Predicted damage = profile weight of the source * exposure. The weights are calibrated from telemetry.
    public class RoomDifficultyModel
    {
        private readonly BaseStats _stats;
        private readonly DifficultyModelSettings _settings;

        public RoomDifficultyModel(BaseStats stats, DifficultyModelSettings settings)
        {
            _stats = stats;
            _settings = settings;
        }

        public RoomPrediction Predict(RoomGenome genome, bool[] shape, RoomSettings settings, PlayerProfile profile)
        {
            var prediction = new RoomPrediction
            {
                Exposure = Exposure(genome, shape, settings, profile, settings.SurviveTime)
            };

            for (var i = 0; i < prediction.Exposure.Length; i++)
            {
                prediction.Damage[i] = profile.Weights[i] * prediction.Exposure[i];
                prediction.Total += prediction.Damage[i];
            }
            return prediction;
        }

        // duration = seconds in the room: SurviveTime when predicting, TimeSurvived when calibrating.
        public float[] Exposure(RoomGenome genome, bool[] shape, RoomSettings settings, PlayerProfile profile, float duration)
        {
            var exposure = new float[TelemetryEnums.DamageSourceCount];
            var maxHealth = profile.PlayerMaxHealth > 0f ? profile.PlayerMaxHealth : _stats.PlayerMaxHealth;

            CountCells(genome, shape, out var groundSpawners, out var rangedSpawners,
                out var thornNear, out var thornFar, out var stakesNear, out var stakesFar);

            // Enemies only exist after the first spawn delay.
            var enemyTime = Math.Max(0f, duration - _stats.FirstSpawnDelay);

            var groundAlive = AliveEnemies(groundSpawners, settings.Ground, profile.Lifetime(EnemyKind.Ground));
            var rangedAlive = AliveEnemies(rangedSpawners, settings.Ranged, profile.Lifetime(EnemyKind.Ranged));
            
            //Crowd limits the melee enemies: they compete for the few places around the player.
            var effectiveGroundEnemies = groundAlive * CrowdFactor(groundAlive);

            exposure[(int)DamageSource.GroundEnemy] =
                effectiveGroundEnemies * Dps(settings.Ground, _stats.GroundDamage) * enemyTime / maxHealth;
            exposure[(int)DamageSource.Arrow] =
                rangedAlive * Dps(settings.Ranged, _stats.ArrowDamage) * enemyTime / maxHealth;

            // Traps: one "chance" per trap per minute, traps near the safe path count fully.
            var minutes = Math.Max(0f, duration) / 60f;
            exposure[(int)DamageSource.ThornTrap] = _stats.ThornDamage * settings.ThornDamageMultiplier / maxHealth
                                                    * (thornNear + _settings.TrapFarWeight * thornFar) * minutes;
            exposure[(int)DamageSource.StakesTrap] = _stats.StakesDamage * settings.StakesDamageMultiplier / maxHealth
                                                     * (stakesNear + _settings.TrapFarWeight * stakesFar) * minutes;
            return exposure;
        }

        // Average enemies alive from n spawners, by Little's law: arrivals per second (1 / SpawnInterval)
        // times how long each one lives (BaseLifetime * HealthMultiplier), capped by MaxAlive per spawner.
        private static float AliveEnemies(int spawners, SpawnerSettings spawner, float baseLifetime)
        {
            if (spawners == 0 || spawner.SpawnInterval <= 0f) return 0f;
            var lifetime = baseLifetime * spawner.HealthMultiplier;
            return spawners * Math.Min(spawner.MaxAlive, lifetime / spawner.SpawnInterval);
        }

        // Damage per second of one enemy if every attack landed.
        private static float Dps(SpawnerSettings spawner, float baseDamage) =>
            spawner.AttackCooldown > 0f ? baseDamage * spawner.DamageMultiplier / spawner.AttackCooldown : 0f;

        // Fraction of the alive ground enemies that effectively attack (occupancy problem): there are C attack
        // slots around the player and each enemy takes one at random. A slot stays empty with probability
        // (1 - 1/C)^n, so the expected number of occupied slots is C * (1 - (1 - 1/C)^n).
        // The fraction is capped at 1 because n is an average and can be below 1.
        private float CrowdFactor(float alive)
        {
            var capacity = Math.Max(1f, _settings.CrowdCapacity);
            if (alive <= 0f) return 1f;
            var effective = capacity * (1.0 - Math.Pow(1.0 - 1.0 / capacity, alive));
            return (float)Math.Min(1.0, effective / alive);
        }

        private static void CountCells(RoomGenome genome, bool[] shape, out int ground, out int ranged,
            out int thornNear, out int thornFar, out int stakesNear, out int stakesFar)
        {
            ground = ranged = thornNear = thornFar = stakesNear = stakesFar = 0;
            var nearPath = NearSafePath(genome, shape);

            for (var gene = 0; gene < RoomLayout.CellCount; gene++)
            {
                if (!RoomLayout.IsActive(gene, shape)) continue;
                switch (genome.Cells[gene])
                {
                    case CellType.GroundEnemySpawner: ground++; break;
                    case CellType.RangedEnemySpawner: ranged++; break;
                    case CellType.ThornTrap:
                        if (nearPath[gene]) thornNear++; else thornFar++;
                        break;
                    case CellType.StakesTrap:
                        if (nearPath[gene]) stakesNear++; else stakesFar++;
                        break;
                }
            }
        }

        // Cells at most 1 cell away (diagonals included) from the trap-free path between the doors.
        private static bool[] NearSafePath(RoomGenome genome, bool[] shape)
        {
            var near = new bool[RoomLayout.CellCount];
            var path = RoomRepair.FindSafePath(genome, shape);
            if (path == null)
            {
                // Should not happen after repair. Treat every trap as near (worst case).
                for (var i = 0; i < near.Length; i++) near[i] = true;
                return near;
            }

            foreach (var gene in path)
            {
                RoomLayout.ToRowCol(gene, out var row, out var col);
                for (var r = row - 1; r <= row + 1; r++)
                for (var c = col - 1; c <= col + 1; c++)
                {
                    if (r < 0 || r >= RoomLayout.GridSide || c < 0 || c >= RoomLayout.GridSide) continue;
                    near[RoomLayout.FromRowCol(r, c)] = true;
                }
            }
            return near;
        }
    }
}
