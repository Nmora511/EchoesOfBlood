using System;
using System.Globalization;
using System.IO;
using System.Text;
using Evolution.Persistence;
using Evolution.Telemetry;

namespace Evolution.Cycle
{
    // Validation record of the experiment, inside the participant folder:
    //
    //   runs/run_001.json   the whole chain of one run (plan, prediction, telemetry, profile, evolution, next run)
    //   runs.csv            one line per run
    //   rooms.csv           one line per played room (predicted vs observed)
    //   generations.csv     one line per generation (convergence curve)
    //
    // CSV with ',' as separator and '.' as decimal point (invariant culture), so Python, R and
    // Google Sheets read it directly. In Excel with Portuguese settings: Data > From Text/CSV, delimiter comma.
    // Abandoned runs never get here: only finished runs produce a RunReport.
    public class ValidationLog
    {
        public const string RunsFile = "runs.csv";
        public const string RoomsFile = "rooms.csv";
        public const string GenerationsFile = "generations.csv";

        private static readonly string RunsHeader =
            "participant,run,won,ended_in_room,rooms_played,plan_generation,plan_fitness,plan_target_error," +
            "mean_observed,mean_abs_model_error,delta_before,delta_after," +
            "w_ground,w_arrow,w_thorn,w_stakes,lifetime_ground,lifetime_ranged," +
            "evolution_ms,generation_to,best_fitness_after,mean_fitness_after,diversity_after,next_run,next_target_error";

        private static readonly string RoomsHeader =
            "participant,run,room,outcome,planned_time,time_survived,target,predicted_planned," +
            "predicted_at_played_time,observed,error,target_error," +
            "pred_ground,obs_ground,pred_arrow,obs_arrow,pred_thorn,obs_thorn,pred_stakes,obs_stakes";

        private static readonly string GenerationsHeader =
            "participant,after_run,generation,generation_total,best,mean,worst,diversity,ms";

        private readonly string _folder;

        public ValidationLog(string folder)
        {
            _folder = folder;
        }

        public void Write(RunReport report)
        {
            var runsFolder = Path.Combine(_folder, "runs");
            Directory.CreateDirectory(runsFolder);
            StateStore.WriteAtomic(Path.Combine(runsFolder, $"run_{report.RunNumber:000}.json"), report.ToJson());

            Append(RunsFile, RunsHeader, RunLine(report));

            var rooms = new StringBuilder();
            foreach (var room in report.Rooms) rooms.AppendLine(RoomLine(report, room));
            Append(RoomsFile, RoomsHeader, rooms.ToString().TrimEnd('\r', '\n'));

            var generations = new StringBuilder();
            foreach (var stats in report.Evolution.Generations)
            {
                generations.AppendLine(Csv(report.ParticipantId, report.RunNumber, stats.Generation,
                    report.GenerationFrom + stats.Generation, stats.Best, stats.Mean, stats.Worst, stats.Diversity,
                    stats.ElapsedMs));
            }
            Append(GenerationsFile, GenerationsHeader, generations.ToString().TrimEnd('\r', '\n'));
        }

        private static string RunLine(RunReport r)
        {
            var after = r.ProfileAfter;
            var last = r.Evolution.Generations[r.Evolution.Generations.Count - 1];
            return Csv(r.ParticipantId, r.RunNumber, r.Telemetry.Won, r.Telemetry.EndedInRoom, r.Rooms.Count,
                r.PlayedPlanGeneration, r.PlayedPlanFitness, r.PlayedPrediction.Error,
                r.MeanObserved, r.MeanAbsoluteError, r.ProfileBefore.Delta, after.Delta,
                after.Weight(DamageSource.GroundEnemy), after.Weight(DamageSource.Arrow),
                after.Weight(DamageSource.ThornTrap), after.Weight(DamageSource.StakesTrap),
                after.Lifetime(EnemyKind.Ground), after.Lifetime(EnemyKind.Ranged),
                r.Evolution.ElapsedMs, r.GenerationTo, last.Best, last.Mean, last.Diversity,
                r.NextRunNumber, r.NextPrediction.Error);
        }

        private static string RoomLine(RunReport r, RoomComparison c)
        {
            var p = c.PredictedBySource;
            var o = c.ObservedBySource;
            const int ground = (int)DamageSource.GroundEnemy, arrow = (int)DamageSource.Arrow;
            const int thorn = (int)DamageSource.ThornTrap, stakes = (int)DamageSource.StakesTrap;

            return Csv(r.ParticipantId, r.RunNumber, c.RoomIndex + 1, c.Outcome, c.PlannedTime, c.TimeSurvived,
                c.Target, c.PredictedPlanned, c.PredictedAtPlayedTime, c.Observed, c.Error, c.TargetError,
                p[ground], o[ground], p[arrow], o[arrow], p[thorn], o[thorn], p[stakes], o[stakes]);
        }

        private void Append(string fileName, string header, string lines)
        {
            if (string.IsNullOrEmpty(lines)) return;
            var path = Path.Combine(_folder, fileName);
            var text = new StringBuilder();
            if (!File.Exists(path)) text.AppendLine(header);
            text.AppendLine(lines);
            File.AppendAllText(path, text.ToString(), new UTF8Encoding(false));
        }

        public static string Csv(params object[] values)
        {
            var line = new StringBuilder();
            for (var i = 0; i < values.Length; i++)
            {
                if (i > 0) line.Append(',');
                line.Append(Format(values[i]));
            }
            return line.ToString();
        }

        private static string Format(object value)
        {
            if (value == null) return "";
            if (value is float) return Number((float)value);
            if (value is double) return Number((double)value);
            if (value is bool) return (bool)value ? "1" : "0";
            if (value is int) return ((int)value).ToString(CultureInfo.InvariantCulture);

            var text = value.ToString();
            return text.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
        }

        private static string Number(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) ? "" : value.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
