using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;

namespace Octockup.Server.Helpers
{
    public static class ScheduleHelpers
    {
        public static async Task<Schedule?> GetNextScheduleAsync(IQueryable<Schedule> _schedules)
        {
            DateTime now = DateTime.UtcNow;

            var schedules = await _schedules
                .Include(x => x.Backup)
                .ThenInclude(b => b.Source)
                .Include(x => x.Backup)
                .ThenInclude(b => b.Storage)
                .ToListAsync();

            Schedule? best = null;
            DateTime? bestTime = null;

            foreach (var sch in schedules)
            {
                DateTime? nextRun = CalculateNextRun(sch, now);
                if (nextRun == null)
                    continue;

                if (bestTime == null || nextRun < bestTime)
                {
                    best = sch;
                    bestTime = nextRun;
                }
            }

            return best;
        }

        public static DateTime? CalculateNextRun(Schedule s, DateTime now)
        {
            // One-time job (Interval = null)
            if (s.Interval is null)
            {
                // Not started yet → next start
                if (s.FinishedAt is null)
                {
                    return s.StartAt > now ? s.StartAt : now;
                }

                // already executed → no more runs
                return null;
            }

            // Periodic job
            TimeSpan interval = s.Interval.Value;

            // If StartAt is in the future
            if (s.StartAt > now)
            {
                return s.StartAt;
            }

            // If currently running or never finished, cannot determine next run precisely
            if (s.FinishedAt is null)
            {
                // If never started, next run is at StartAt or now
                return s.StartAt > now ? s.StartAt : now;
            }

            // Calculate next run based on when it last finished
            DateTime lastFinished = s.FinishedAt.Value;

            // Next run should be: last finished time + interval
            DateTime nextRun = lastFinished.Add(interval);

            // If the calculated next run is still in the past (e.g., server was down),
            // calculate the nearest future tick from StartAt
            if (nextRun <= now)
            {
                var elapsed = now - s.StartAt;
                if (elapsed.TotalMilliseconds < 0)
                {
                    elapsed = TimeSpan.Zero;
                }

                long k = elapsed.Ticks / interval.Ticks;
                nextRun = s.StartAt.AddTicks(interval.Ticks * (k + 1));
            }

            return nextRun;
        }

        public static string SplitHash(string hash, char pathSeparator)
        {
            // format: aa/bb/ccddeeff...
            return $"{hash[..2]}{pathSeparator}{hash.Substring(2, 2)}{pathSeparator}{hash[4..]}.br.oct";
        }

        public static bool IsPathIgnored(string path, string? fileName, ICollection<string> ignoredPaths)
        {
            foreach (var ignored in ignoredPaths)
            {
                if (string.IsNullOrWhiteSpace(ignored))
                {
                    continue;
                }
                if (path.StartsWith(ignored, StringComparison.OrdinalIgnoreCase) || path.StartsWith(ignored[1..], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                if (fileName != null && fileName.Equals(ignored, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
