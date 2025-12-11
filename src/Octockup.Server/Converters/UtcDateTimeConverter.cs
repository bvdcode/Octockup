using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Octockup.Server.Converters
{
    /// <summary>
    /// Ensures DateTime values are always stored and retrieved as UTC in SQLite.
    /// Throws if attempting to save non-UTC DateTime.
    /// </summary>
    public class UtcDateTimeConverter : ValueConverter<DateTime, string>
    {
        public UtcDateTimeConverter() : base(v => ConvertToUtcString(v), v => DateTime.Parse(v).ToUniversalTime())
        {

        }

        private static string ConvertToUtcString(DateTime value)
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                throw new InvalidOperationException($"Attempted to save non-UTC DateTime ({value.Kind}): {value}. All DateTime values must be UTC.");
            }
            return value.ToString("o");
        }
    }
}
