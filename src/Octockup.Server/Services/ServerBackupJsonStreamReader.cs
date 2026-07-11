// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Buffers;
using System.IO.Pipelines;
using System.Text.Json;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Services
{
    public class ServerBackupJsonStreamReader
    {
        private const int EventBatchSize = 100;
        private const int ExpectedSectionCount = 5;

        public async Task ReadAsync(
            PipeReader pipeReader,
            Func<ServerBackupJsonEvent, CancellationToken, Task> handleEventAsync,
            CancellationToken cancellationToken)
        {
            JsonReaderState readerState = default;
            ServerBackupJsonParserState parserState = new();
            Exception? error = null;
            try
            {
                while (true)
                {
                    ReadResult readResult = await pipeReader
                        .ReadAsync(cancellationToken)
                        .ConfigureAwait(false);
                    ReadOnlySequence<byte> buffer = readResult.Buffer;
                    List<ServerBackupJsonEvent> events = new(EventBatchSize);
                    ServerBackupParseResult parseResult;
                    try
                    {
                        parseResult = ParseBuffer(
                            buffer,
                            readResult.IsCompleted,
                            readerState,
                            parserState,
                            events);
                    }
                    catch
                    {
                        DisposeEvents(events);
                        throw;
                    }

                    SequencePosition consumed = buffer.GetPosition(
                        parseResult.ConsumedBytes);
                    SequencePosition examined = parseResult.NeedsMoreData
                        ? buffer.End
                        : consumed;
                    pipeReader.AdvanceTo(consumed, examined);
                    readerState = parseResult.ReaderState;

                    try
                    {
                        foreach (ServerBackupJsonEvent jsonEvent in events)
                        {
                            await handleEventAsync(
                                jsonEvent,
                                cancellationToken).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        DisposeEvents(events);
                    }

                    if (readResult.IsCompleted)
                    {
                        if (parseResult.NeedsMoreData)
                        {
                            throw new JsonException(
                                "Server backup JSON ended inside a value.");
                        }

                        if (parseResult.ConsumedBytes == buffer.Length)
                        {
                            break;
                        }
                    }
                }

                if (!parserState.RootCompleted ||
                    parserState.CurrentSection != ServerBackupSection.None ||
                    parserState.NextSectionIndex != ExpectedSectionCount)
                {
                    throw new JsonException(
                        "Server backup JSON does not contain every required section.");
                }
            }
            catch (Exception ex)
            {
                error = ex;
                throw;
            }
            finally
            {
                await pipeReader.CompleteAsync(error).ConfigureAwait(false);
            }
        }

        private static ServerBackupParseResult ParseBuffer(
            ReadOnlySequence<byte> buffer,
            bool isFinalBlock,
            JsonReaderState readerState,
            ServerBackupJsonParserState parserState,
            ICollection<ServerBackupJsonEvent> events)
        {
            Utf8JsonReader reader = new(buffer, isFinalBlock, readerState);
            while (events.Count < EventBatchSize)
            {
                JsonReaderState stateBeforeToken = reader.CurrentState;
                long bytesBeforeToken = reader.BytesConsumed;
                if (!reader.Read())
                {
                    return new ServerBackupParseResult(
                        reader.BytesConsumed,
                        reader.CurrentState,
                        !isFinalBlock);
                }

                if (parserState.CurrentSection != ServerBackupSection.None &&
                    reader.TokenType == JsonTokenType.StartObject &&
                    reader.CurrentDepth == 2)
                {
                    Utf8JsonReader valueReader = reader;
                    if (!JsonDocument.TryParseValue(
                        ref valueReader,
                        out JsonDocument? document))
                    {
                        if (isFinalBlock)
                        {
                            throw new JsonException(
                                "Server backup JSON ended inside an array item.");
                        }

                        return new ServerBackupParseResult(
                            bytesBeforeToken,
                            stateBeforeToken,
                            true);
                    }

                    reader = valueReader;
                    events.Add(new ServerBackupJsonEvent(
                        parserState.CurrentSection,
                        document,
                        false));
                    continue;
                }

                ProcessStructuralToken(reader, parserState, events);
            }

            return new ServerBackupParseResult(
                reader.BytesConsumed,
                reader.CurrentState,
                false);
        }

        private static void ProcessStructuralToken(
            Utf8JsonReader reader,
            ServerBackupJsonParserState parserState,
            ICollection<ServerBackupJsonEvent> events)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject when
                    reader.CurrentDepth == 0 &&
                    !parserState.RootStarted:
                    parserState.RootStarted = true;
                    return;
                case JsonTokenType.PropertyName when
                    reader.CurrentDepth == 1 &&
                    parserState.CurrentSection == ServerBackupSection.None:
                    parserState.PendingProperty = reader.GetString();
                    return;
                case JsonTokenType.StartArray when
                    reader.CurrentDepth == 1 &&
                    parserState.PendingProperty is not null:
                    StartSection(parserState);
                    return;
                case JsonTokenType.EndArray when
                    reader.CurrentDepth == 1 &&
                    parserState.CurrentSection != ServerBackupSection.None:
                    ServerBackupSection completedSection = parserState.CurrentSection;
                    parserState.CurrentSection = ServerBackupSection.None;
                    parserState.NextSectionIndex++;
                    events.Add(new ServerBackupJsonEvent(
                        completedSection,
                        null,
                        true));
                    return;
                case JsonTokenType.EndObject when
                    reader.CurrentDepth == 0 &&
                    parserState.RootStarted &&
                    parserState.CurrentSection == ServerBackupSection.None:
                    parserState.RootCompleted = true;
                    return;
                default:
                    throw new JsonException(
                        $"Unexpected token {reader.TokenType} at depth {reader.CurrentDepth} in server backup JSON.");
            }
        }

        private static void StartSection(ServerBackupJsonParserState parserState)
        {
            ServerBackupSection section = ParseSection(parserState.PendingProperty!);
            if ((int)section != parserState.NextSectionIndex)
            {
                throw new JsonException(
                    $"Server backup section {section} is out of order.");
            }

            parserState.CurrentSection = section;
            parserState.PendingProperty = null;
        }

        private static ServerBackupSection ParseSection(string propertyName)
        {
            if (propertyName.Equals("Modules", StringComparison.OrdinalIgnoreCase))
            {
                return ServerBackupSection.Modules;
            }
            if (propertyName.Equals("Backups", StringComparison.OrdinalIgnoreCase))
            {
                return ServerBackupSection.Backups;
            }
            if (propertyName.Equals("Schedules", StringComparison.OrdinalIgnoreCase))
            {
                return ServerBackupSection.Schedules;
            }
            if (propertyName.Equals("Snapshots", StringComparison.OrdinalIgnoreCase))
            {
                return ServerBackupSection.Snapshots;
            }
            if (propertyName.Equals("SnapshotFiles", StringComparison.OrdinalIgnoreCase))
            {
                return ServerBackupSection.SnapshotFiles;
            }

            throw new JsonException(
                $"Unknown server backup section '{propertyName}'.");
        }

        private static void DisposeEvents(IEnumerable<ServerBackupJsonEvent> events)
        {
            foreach (ServerBackupJsonEvent jsonEvent in events)
            {
                jsonEvent.Dispose();
            }
        }
    }
}
