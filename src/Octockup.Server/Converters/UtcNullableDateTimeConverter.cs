using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Octockup.Server.Converters
{
    /// <summary>
    /// Ensures nullable DateTime values are always stored and retrieved as UTC in SQLite.
    /// Throws if attempting to save non-UTC DateTime.
    /// </summary>
    public class UtcNullableDateTimeConverter : ValueConverter<DateTime?, string?>
    {
        public UtcNullableDateTimeConverter() : base(v => ConvertToUtcString(v), v => v != null ? DateTime.Parse(v).ToUniversalTime() : null)
        {

        }

        private static string? ConvertToUtcString(DateTime? value)
        {
            if (!value.HasValue)
            {
                return null;
            }
            if (value.Value.Kind != DateTimeKind.Utc)
            {
                throw new InvalidOperationException($"Attempted to save non-UTC DateTime ({value.Value.Kind}): {value.Value}. All DateTime values must be UTC.");
            }
            return value.Value.ToString("o");
        }
    }
}
