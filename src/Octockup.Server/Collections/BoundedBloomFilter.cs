// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Octockup.Server.Collections
{
    public class BoundedBloomFilter
    {
        private const int MaximumHashFunctions = 12;
        private readonly byte[] _bits;
        private readonly int _hashFunctionCount;
        private readonly ulong _bitCount;

        public BoundedBloomFilter(int byteCount, long expectedItems)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(byteCount, 1);
            ArgumentOutOfRangeException.ThrowIfNegative(expectedItems);

            _bits = new byte[byteCount];
            _bitCount = checked((ulong)byteCount * 8UL);
            _hashFunctionCount = CalculateHashFunctionCount(_bitCount, expectedItems);
        }

        public int ByteCount => _bits.Length;
        public int HashFunctionCount => _hashFunctionCount;

        public void Add(string value)
        {
            CalculateHashes(value, out ulong firstHash, out ulong secondHash);
            for (int index = 0; index < _hashFunctionCount; index++)
            {
                ulong bitIndex = GetBitIndex(firstHash, secondHash, index);
                int byteIndex = checked((int)(bitIndex / 8UL));
                int bitOffset = (int)(bitIndex % 8UL);
                _bits[byteIndex] |= (byte)(1 << bitOffset);
            }
        }

        public bool MightContain(string value)
        {
            CalculateHashes(value, out ulong firstHash, out ulong secondHash);
            for (int index = 0; index < _hashFunctionCount; index++)
            {
                ulong bitIndex = GetBitIndex(firstHash, secondHash, index);
                int byteIndex = checked((int)(bitIndex / 8UL));
                int bitOffset = (int)(bitIndex % 8UL);
                if ((_bits[byteIndex] & (1 << bitOffset)) == 0)
                {
                    return false;
                }
            }

            return true;
        }

        private ulong GetBitIndex(ulong firstHash, ulong secondHash, int index)
        {
            ulong combinedHash = unchecked(firstHash + ((ulong)index * secondHash));
            return combinedHash % _bitCount;
        }

        private static void CalculateHashes(
            string value,
            out ulong firstHash,
            out ulong secondHash)
        {
            int byteCount = Encoding.UTF8.GetByteCount(value);
            byte[]? rentedBytes = null;
            Span<byte> valueBytes = byteCount <= 256
                ? stackalloc byte[byteCount]
                : (rentedBytes = ArrayPool<byte>.Shared.Rent(byteCount)).AsSpan(0, byteCount);
            try
            {
                Encoding.UTF8.GetBytes(value, valueBytes);
                Span<byte> digest = stackalloc byte[32];
                SHA256.HashData(valueBytes, digest);
                firstHash = BinaryPrimitives.ReadUInt64LittleEndian(digest[..8]);
                secondHash = BinaryPrimitives.ReadUInt64LittleEndian(digest.Slice(8, 8)) | 1UL;
            }
            finally
            {
                if (rentedBytes is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedBytes);
                }
            }
        }

        private static int CalculateHashFunctionCount(ulong bitCount, long expectedItems)
        {
            if (expectedItems == 0)
            {
                return 1;
            }

            double optimal = ((double)bitCount / expectedItems) * Math.Log(2);
            return Math.Clamp((int)Math.Round(optimal), 1, MaximumHashFunctions);
        }
    }
}
