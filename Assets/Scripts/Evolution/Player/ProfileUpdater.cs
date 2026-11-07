using System;
using System.Collections.Generic;
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

        // Call once per run with all the rooms played.
        public static void UpdateFromRooms(PlayerProfile profile, IList<RoomPlan> plans, IList<RoomTelemetry> telemetries,
            RoomDifficultyModel model, ProfileSettings settings)
        {
            if (profile == null || plans == null || telemetries == null) return;
            var count = Math.Min(plans.Count, telemetries.Count);
            if (count == 0) return;

            for (var i = 0; i < count; i++)
            {
                if (telemetries[i].PlayerMaxHealth > 0f) profile.PlayerMaxHealth = telemetries[i].PlayerMaxHealth;
            }

            var alpha = AlphaForRun(profile.RunsObserved, settings);
            
            // 1. Kill speed: lifetime of the enemies that died, divided by the health multiplier of the room.
            UpdateLifetime(profile, EnemyKind.Ground, plans, telemetries, count, alpha);
            UpdateLifetime(profile, EnemyKind.Ranged, plans, telemetries, count, alpha);

            // 2. Weights: real damage / exposure, using the time the player actually spent in the room.
            var damage = new float[TelemetryEnums.DamageSourceCount];
            var exposure = new float[TelemetryEnums.DamageSourceCount];
            for (var i = 0; i < count; i++)
            {
                var plan = plans[i];
                var telemetry = telemetries[i];
                var roomExposure = model.Exposure(plan.Genome, plan.Shape, plan.Settings, profile, telemetry.TimeSurvived);
                foreach (var source in RoomSources)
                {
                    exposure[(int)source] += roomExposure[(int)source];
                    damage[(int)source] += telemetry.DamageFraction(source);
                }
            }
            
            foreach (var source in RoomSources)
            {
                var e = exposure[(int)source];
                if (e < settings.MinExposure) continue; // too little of this source in the run to learn anything

                var observed = Clamp(damage[(int)source] / e, 0f, settings.MaxWeight);
                profile.Weights[(int)source] = Lerp(profile.Weights[(int)source], observed, alpha);
            }
            profile.RoomsObserved += count;
            }

        // A run with a single room (used by the tests).
        public static void UpdateFromRoom(PlayerProfile profile, RoomPlan played, RoomTelemetry telemetry,
            RoomDifficultyModel model, ProfileSettings settings)
        {
            if (played == null || telemetry == null) return;
            UpdateFromRooms(profile, new[] { played }, new[] { telemetry }, model, settings);
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

        private static void UpdateLifetime(PlayerProfile profile, EnemyKind kind, IList<RoomPlan> plans,
            IList<RoomTelemetry> telemetries, int count, float alpha)
        {
            // Only enemies that died have a lifetime. Enemies still alive at the end are ignored
            var killed = 0;
            var normalizedLifetime = 0f;
            for (var i = 0; i < count; i++)
            {
                var spawner = kind == EnemyKind.Ground ? plans[i].Settings.Ground : plans[i].Settings.Ranged;
                var kills = telemetries[i].EnemiesKilled[(int)kind];
                if (kills == 0 || spawner.HealthMultiplier <= 0f) continue;

                killed += kills;
                normalizedLifetime += telemetries[i].KilledEnemyLifetime[(int)kind] / spawner.HealthMultiplier;
            }
            if (killed == 0) return;

            profile.BaseLifetime[(int)kind] = Lerp(profile.BaseLifetime[(int)kind], normalizedLifetime / killed, alpha);
        }

        public static float AlphaForRun(int runsObserved, ProfileSettings settings)
        {
            if (settings.AlphaWarmupRuns <= 0 || runsObserved >= settings.AlphaWarmupRuns) return settings.RunAlpha;
            var t = Math.Max(0, runsObserved) / (float)settings.AlphaWarmupRuns;
            var warm = settings.FirstRunAlpha + (settings.RunAlpha - settings.FirstRunAlpha) * t;
            return Math.Max(settings.RunAlpha, warm);
        }

        private static float Lerp(float from, float to, float t) => from + (to - from) * t;
        private static float Clamp(float v, float min, float max) => Math.Max(min, Math.Min(max, v));
    }
}
