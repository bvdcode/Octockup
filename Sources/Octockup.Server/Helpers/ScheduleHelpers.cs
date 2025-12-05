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

        /// <summary>
        /// Checks if a path should be ignored based on ignore patterns.
        /// Patterns starting with '/' match from root (StartsWith).
        /// Patterns without '/' match anywhere in path (Contains).
        /// Supports simple wildcards: '*' matches any characters.
        /// </summary>
        public static bool IsPathIgnored(string path, string? fileName, ICollection<string> ignoredPaths)
        {
            foreach (var pattern in ignoredPaths)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }

                var trimmedPattern = pattern.Trim();

                // Check if filename matches exactly (for patterns like "swap.img")
                if (fileName != null && fileName.Equals(trimmedPattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Pattern starts with '/' → match from root
                if (trimmedPattern.StartsWith('/'))
                {
                    if (MatchesPattern(path, trimmedPattern))
                    {
                        return true;
                    }
                }
                else
                {
                    // Pattern without '/' → match anywhere in path
                    // Split path by '/' and check each segment
                    var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var segment in segments)
                    {
                        if (MatchesPattern("/" + segment, "/" + trimmedPattern))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Matches a path against a pattern with simple wildcard support.
        /// Supports '*' to match any characters.
        /// </summary>
        private static bool MatchesPattern(string path, string pattern)
        {
            // Simple wildcard support: convert pattern to regex-like matching
            if (!pattern.Contains('*'))
            {
                // No wildcards - simple prefix match
                return path.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
            }

            // Split pattern by '*' and check each part exists in order
            var parts = pattern.Split('*', StringSplitOptions.None);
            int currentIndex = 0;

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }

                int foundIndex = path.IndexOf(part, currentIndex, StringComparison.OrdinalIgnoreCase);
                if (foundIndex == -1)
                {
                    return false;
                }

                // First part must match at the start (unless pattern starts with *)
                if (i == 0 && !pattern.StartsWith('*') && foundIndex != currentIndex)
                {
                    return false;
                }

                currentIndex = foundIndex + part.Length;
            }

            // Last part must match at the end (unless pattern ends with *)
            if (parts.Length > 0 && !pattern.EndsWith('*'))
            {
                var lastPart = parts[^1];
                if (!string.IsNullOrEmpty(lastPart) && !path.EndsWith(lastPart, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
