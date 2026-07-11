// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Models.Enums;
using Octockup.Server.Services;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;

namespace Octockup.Tests
{
    public class ServerBackupJsonStreamReaderTests
    {
        [Test]
        public async Task ReadAsync_ParsesItemsSplitAcrossSmallPipeSegments()
        {
            string largeValue = new('x', 32_000);
            string json = """
                {
                  "Modules": [{"Id":"11111111-1111-1111-1111-111111111111","Tag":"module","Parameters":{"value":"VALUE"}}],
                  "Backups": [],
                  "Schedules": [],
                  "Snapshots": [],
                  "SnapshotFiles": []
                }
                """.Replace("VALUE", largeValue, StringComparison.Ordinal);
            Pipe pipe = new();
            Task producer = WriteFragmentedAsync(pipe.Writer, Encoding.UTF8.GetBytes(json));
            ServerBackupJsonStreamReader reader = new();
            List<ServerBackupSection> completedSections = [];
            int itemCount = 0;

            await reader.ReadAsync(
                pipe.Reader,
                (jsonEvent, _) =>
                {
                    if (jsonEvent.SectionCompleted)
                    {
                        completedSections.Add(jsonEvent.Section);
                    }
                    else
                    {
                        itemCount++;
                        Assert.That(
                            jsonEvent.Document!.RootElement
                                .GetProperty("Parameters")
                                .GetProperty("value")
                                .GetString(),
                            Has.Length.EqualTo(largeValue.Length));
                    }

                    return Task.CompletedTask;
                },
                CancellationToken.None);
            await producer;

            Assert.Multiple(() =>
            {
                Assert.That(itemCount, Is.EqualTo(1));
                Assert.That(completedSections, Is.EqualTo(new[]
                {
                    ServerBackupSection.Modules,
                    ServerBackupSection.Backups,
                    ServerBackupSection.Schedules,
                    ServerBackupSection.Snapshots,
                    ServerBackupSection.SnapshotFiles
                }));
            });
        }

        [Test]
        public async Task ReadAsync_RejectsOutOfOrderSections()
        {
            const string json = """
                {"Backups":[],"Modules":[],"Schedules":[],"Snapshots":[],"SnapshotFiles":[]}
            """;
            Pipe pipe = new();
            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(json));
            await pipe.Writer.CompleteAsync();
            ServerBackupJsonStreamReader reader = new();

            Assert.ThrowsAsync<JsonException>(async () => await reader.ReadAsync(
                pipe.Reader,
                (_, _) => Task.CompletedTask,
                CancellationToken.None));
        }

        private static async Task WriteFragmentedAsync(
            PipeWriter writer,
            byte[] content)
        {
            try
            {
                foreach (byte[] fragment in content.Chunk(37))
                {
                    await writer.WriteAsync(fragment);
                }
            }
            finally
            {
                await writer.CompleteAsync();
            }
        }
    }
}
