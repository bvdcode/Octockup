// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.WebUtilities;
using System.Globalization;
using System.Text;

namespace Octockup.Server.Helpers
{
    public static class SnapshotCursorCodec
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public static string Encode(DateTime? completedAt, Guid id)
        {
            string value = completedAt.HasValue
                ? $"1.{completedAt.Value.Ticks.ToString(CultureInfo.InvariantCulture)}.{id:N}"
                : $"0.0.{id:N}";
            return WebEncoders.Base64UrlEncode(Utf8.GetBytes(value));
        }

        public static (DateTime? CompletedAt, Guid Id) Decode(string cursor)
        {
            try
            {
                string value = Utf8.GetString(WebEncoders.Base64UrlDecode(cursor));
                string[] parts = value.Split('.');
                if (parts.Length != 3 ||
                    !Guid.TryParseExact(parts[2], "N", out Guid id))
                {
                    throw new FormatException("Snapshot cursor has an invalid shape.");
                }

                if (parts[0] == "0" && parts[1] == "0")
                {
                    return (null, id);
                }

                if (parts[0] != "1" ||
                    !long.TryParse(
                        parts[1],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out long ticks))
                {
                    throw new FormatException("Snapshot cursor has an invalid timestamp.");
                }

                return (new DateTime(ticks, DateTimeKind.Utc), id);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                throw new FormatException("Snapshot cursor is invalid.", ex);
            }
        }
    }
}
