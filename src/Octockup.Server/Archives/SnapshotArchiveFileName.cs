// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Text;

namespace Octockup.Server.Archives
{
    public static class SnapshotArchiveFileName
    {
        private const int MaxTagLength = 96;
        private const string InvalidFileNameChars = "<>:\"/\\|?*";

        public static string Create(
            string backupTag,
            DateTime createdAt,
            DateTime? completedAt,
            Guid snapshotId)
        {
            string tag = SanitizeFileNamePart(backupTag);
            if (tag.Length > MaxTagLength)
            {
                tag = tag[..MaxTagLength].Trim('-', '.', '_');
            }

            if (tag.Length == 0)
            {
                tag = "snapshot";
            }

            DateTime timestamp = completedAt ?? createdAt;
            string timestampText = timestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string shortId = snapshotId.ToString("N")[..8];

            return $"{tag}-{timestampText}-{shortId}.zip";
        }

        public static string CreateContentDisposition(string fileName)
        {
            string sanitized = SanitizeFileName(fileName);
            string asciiFallback = CreateAsciiFallback(sanitized);
            string escapedFileName = Uri.EscapeDataString(sanitized);

            return $"attachment; filename=\"{asciiFallback}\"; filename*=UTF-8''{escapedFileName}";
        }

        private static string SanitizeFileName(string value)
        {
            string fileName = SanitizeFileNamePart(value);
            return fileName.Length == 0 ? "snapshot.zip" : fileName;
        }

        private static string SanitizeFileNamePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            bool previousSeparator = false;

            foreach (char character in value.Normalize(NormalizationForm.FormC))
            {
                if (char.IsControl(character) ||
                    char.IsWhiteSpace(character) ||
                    InvalidFileNameChars.Contains(character))
                {
                    AppendSeparator(builder, ref previousSeparator);
                    continue;
                }

                builder.Append(character);
                previousSeparator = character is '-' or '_' or '.';
            }

            return builder.ToString().Trim('-', '.', '_');
        }

        private static string CreateAsciiFallback(string fileName)
        {
            StringBuilder builder = new StringBuilder(fileName.Length);
            bool previousSeparator = false;

            foreach (char character in fileName)
            {
                if (character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-')
                {
                    builder.Append(character);
                    previousSeparator = character is '-' or '_' or '.';
                    continue;
                }

                AppendSeparator(builder, ref previousSeparator);
            }

            string result = builder.ToString().Trim('-', '.', '_');
            if (result.Length == 0)
            {
                return "snapshot.zip";
            }

            return result.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                ? result
                : $"{result}.zip";
        }

        private static void AppendSeparator(StringBuilder builder, ref bool previousSeparator)
        {
            if (builder.Length == 0 || previousSeparator)
            {
                return;
            }

            builder.Append('-');
            previousSeparator = true;
        }
    }
}
