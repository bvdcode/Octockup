// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;

namespace Octockup.Tests
{
    internal class PassThroughCipher : IStreamCipher
    {
        public async Task EncryptAsync(
            Stream input,
            Stream output,
            int chunkSize,
            bool leaveInputOpen,
            bool leaveOutputOpen,
            CancellationToken ct)
        {
            await input.CopyToAsync(output, ct);
        }

        public async Task DecryptAsync(
            Stream input,
            Stream output,
            bool leaveInputOpen,
            bool leaveOutputOpen,
            CancellationToken ct)
        {
            await input.CopyToAsync(output, ct);
        }

        public Task<Stream> EncryptAsync(
            Stream input,
            int chunkSize,
            bool leaveOpen,
            CancellationToken ct)
        {
            return Task.FromResult(input);
        }

        public Task<Stream> DecryptAsync(
            Stream input,
            bool leaveOpen,
            CancellationToken ct)
        {
            return Task.FromResult(input);
        }
    }
}
