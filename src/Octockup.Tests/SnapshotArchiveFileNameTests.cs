// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Archives;

namespace Octockup.Tests
{
    public class SnapshotArchiveFileNameTests
    {
        [Test]
        public void Create_UsesBackupTagCompletedAtAndSnapshotId()
        {
            Guid snapshotId = Guid.Parse("019eccec-f54d-77ba-833d-0755e65b0543");
            DateTime createdAt = new DateTime(2026, 6, 16, 11, 20, 0, DateTimeKind.Utc);
            DateTime completedAt = new DateTime(2026, 6, 16, 12, 34, 56, DateTimeKind.Utc);

            string result = SnapshotArchiveFileName.Create("Prod / Rust: Main", createdAt, completedAt, snapshotId);

            Assert.That(result, Is.EqualTo("Prod-Rust-Main-20260616-123456-019eccec.zip"));
        }

        [Test]
        public void Create_WhenTagIsEmpty_UsesSnapshotPrefix()
        {
            Guid snapshotId = Guid.Parse("019eccec-f54d-77ba-833d-0755e65b0543");
            DateTime createdAt = new DateTime(2026, 6, 16, 11, 20, 0, DateTimeKind.Utc);

            string result = SnapshotArchiveFileName.Create(" / ", createdAt, null, snapshotId);

            Assert.That(result, Is.EqualTo("snapshot-20260616-112000-019eccec.zip"));
        }

        [Test]
        public void CreateContentDisposition_IncludesAsciiFallbackAndUtf8FileName()
        {
            string result = SnapshotArchiveFileName.CreateContentDisposition(
                "\u041c\u043e\u0439-\u0441\u043d\u0430\u043f\u0448\u043e\u0442-20260616-123456-019eccec.zip");

            Assert.That(result, Does.Contain("attachment; filename=\"20260616-123456-019eccec.zip\""));
            Assert.That(result, Does.Contain("filename*=UTF-8''%D0%9C%D0%BE%D0%B9"));
        }
    }
}
