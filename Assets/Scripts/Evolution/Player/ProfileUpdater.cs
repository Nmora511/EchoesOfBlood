using System;
using Evolution.Rooms;
using Evolution.Telemetry;

namespace Evolution.PlayerModel
{
    // Turns telemetry into profile changes. Every value is an exponential moving average
    // (new = old + Alpha * (observed - old)), so recent rooms matter more and the profile follows learning.
    public static class ProfileUpdater
    {
        // Sources the room GA controls.
        public static readonly DamageSource[] RoomSources =
        {
            DamageSource.GroundEnemy, DamageSource.Arrow, DamageSource.ThornTrap, DamageSource.StakesTrap
        };

        // Call once per played room.
        public static void UpdateFromRoom(PlayerProfile profile, RoomPlan played, RoomTelemetry telemetry,
            RoomDifficultyModel model, ProfileSettings settings)
        {
            if (profile == null || played == null || telemetry == null) return;
            if (telemetry.PlayerMaxHealth > 0f) profile.PlayerMaxHealth = telemetry.PlayerMaxHealth;

            // 1. Kill speed: lifetime of the enemies that died, divided by the health multiplier of the room.
            UpdateLifetime(profile, EnemyKind.Ground, played.Settings.Ground.HealthMultiplier, telemetry, settings.Alpha);
            UpdateLifetime(profile, EnemyKind.Ranged, played.Settings.Ranged.HealthMultiplier, telemetry, settings.Alpha);

            // 2. Weights: real damage / exposure, using the time the player actually spent in the room.
            var exposure = model.Exposure(played.Genome, played.Shape, played.Settings, profile, telemetry.TimeSurvived);
            foreach (var source in RoomSources)
            {
                var e = exposure[(int)source];
                if (e < settings.MinExposure) continue; // too little of this source to learn anything

                var observed = Clamp(telemetry.DamageFraction(source) / e, 0f, settings.MaxWeight);
                profile.Weights[(int)source] = Lerp(profile.Weights[(int)source], observed, settings.Alpha);
            }

            profile.RoomsObserved++;
        }

        // Call once at the end of the run. Moves delta: down if the player died in a room,
        // up if all rooms were survived with health to spare.
        public static void UpdateFromRun(PlayerProfile profile, RunTelemetry run, ProfileSettings settings)
        {
            if (profile == null || run == null || run.Rooms.Count == 0) return;

            var died = false;
            var healthSum = 0f;
            foreach (var room in run.Rooms)
            {
                if (room.Outcome == RoomOutcome.Died) died = true;
                healthSum += room.FinalHealthFraction;
            }

            if (died)
            {
                profile.Delta *= 1f - settings.DeltaStep;
            }
            else if (run.Rooms.Count >= RunPlan.RoomCount && healthSum / run.Rooms.Count >= settings.ComfortMargin)
            {
                profile.Delta *= 1f + settings.DeltaStep;
            }

            profile.Delta = Clamp(profile.Delta, settings.DeltaMin, settings.DeltaMax);
            profile.RunsObserved++;
        }

        private static void UpdateLifetime(PlayerProfile profile, EnemyKind kind, float healthMultiplier,
            RoomTelemetry telemetry, float alpha)
        {
            // Only enemies that died have a lifetime. Enemies still alive at the end are ignored
            if (telemetry.EnemiesKilled[(int)kind] == 0 || healthMultiplier <= 0f) return;

            var observed = telemetry.AverageEnemyLifetime(kind) / healthMultiplier;
            profile.BaseLifetime[(int)kind] = Lerp(profile.BaseLifetime[(int)kind], observed, alpha);
        }

        private static float Lerp(float from, float to, float t) => from + (to - from) * t;
        private static float Clamp(float v, float min, float max) => Math.Max(min, Math.Min(max, v));
    }
}
