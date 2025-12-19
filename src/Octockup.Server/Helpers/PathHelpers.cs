// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

namespace Octockup.Server.Helpers
{
    public class PathHelpers
    {
        public static string GetPath(string fileName)
        {
            // /app/data/fileName
            string folder = Path.Combine(AppContext.BaseDirectory, "data");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, fileName);
        }
    }
}
