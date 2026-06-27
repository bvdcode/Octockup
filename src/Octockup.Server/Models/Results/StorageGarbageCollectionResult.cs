// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Results
{
    public class StorageGarbageCollectionResult
    {
        public Guid StorageId { get; set; }
        public int UploadedHashesScanned { get; set; }
        public int ReferencedChunks { get; set; }
        public int OrphanChunks { get; set; }
        public int DeletedObjects { get; set; }
        public int MissingObjects { get; set; }
        public int FailedDeletes { get; set; }
        public long FreedStoredSize { get; set; }
    }
}
