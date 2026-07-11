// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;

namespace Octockup.Tests
{
    internal class TestCipher : IStreamCipher
    {
        public Task EncryptAsync(
            Stream input,
            Stream output,
            int chunkSize,
            bool leaveInputOpen,
            bool leaveOutputOpen,
            CancellationToken ct) => input.CopyToAsync(output, ct);

        public Task DecryptAsync(
            Stream input,
            Stream output,
            bool leaveInputOpen,
            bool leaveOutputOpen,
            CancellationToken ct) => input.CopyToAsync(output, ct);

        public Task<Stream> EncryptAsync(
            Stream input,
            int chunkSize,
            bool leaveOpen,
            CancellationToken ct) => Task.FromResult(input);

        public Task<Stream> DecryptAsync(
            Stream input,
            bool leaveOpen,
            CancellationToken ct) => Task.FromResult(input);
    }
}
