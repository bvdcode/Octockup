// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace Octockup.Server.Helpers
{
    public static class SnapshotFileCursorCodec
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public static string Encode(string path)
        {
            return WebEncoders.Base64UrlEncode(Utf8.GetBytes(path));
        }

        public static string Decode(string cursor)
        {
            try
            {
                string path = Utf8.GetString(WebEncoders.Base64UrlDecode(cursor));
                if (string.IsNullOrEmpty(path))
                {
                    throw new FormatException("Snapshot file cursor is empty.");
                }

                return path;
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                throw new FormatException("Snapshot file cursor is invalid.", ex);
            }
        }
    }
}
