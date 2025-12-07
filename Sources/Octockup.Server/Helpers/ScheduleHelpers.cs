using Octockup.Server.Database;
using Octockup.Server.Models.Enums;
using Microsoft.EntityFrameworkCore;

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
                if (sch.Status == ScheduleStatus.Running)
                {
                    return sch;
                }

                DateTime? nextRun = CalculateNextRun(sch, now);
                if (nextRun == null)
                {
                    continue;
                }

                // Only consider schedules due to run now or earlier
                if (nextRun > now)
                {
                    continue;
                }

                if (bestTime == null || nextRun < bestTime)
                {
                    best = sch;
                    bestTime = nextRun;
                }
            }

            return best;
        }

        public static DateTime? CalculateNextRun(Schedule schedule, DateTime now)
        {
            if (schedule.Status == ScheduleStatus.Running)
            {
                // Currently running → interrupted, run now
                return DateTime.UtcNow;
            }

            // One-time job (Interval = null)
            if (schedule.Interval is null)
            {
                // Not started yet → next start
                if (schedule.FinishedAt is null)
                {
                    return schedule.StartAt;
                }

                // already executed → no more runs
                return null;
            }

            // Periodic job
            TimeSpan interval = schedule.Interval.Value;

            // If StartAt is in the future → not started yet
            if (schedule.StartAt > now)
            {
                return schedule.StartAt;
            }

            // If never finished yet → first run = StartAt
            if (schedule.FinishedAt is null)
            {
                return schedule.StartAt;
            }

            // Next run strictly from last finish
            return schedule.FinishedAt.Value.Add(interval);
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
