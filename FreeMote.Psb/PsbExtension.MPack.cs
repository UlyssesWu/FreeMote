using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MersenneTwister;
using MersenneTwister.MT;

namespace FreeMote.Psb
{
    /// <summary>
    /// A repeating-key candidate supplied to an MPack shell-specific decoder.
    /// </summary>
    public sealed class MPackKeyLengthCandidate
    {
        private readonly byte[] _cipher;
        private readonly byte[] _key;

        internal MPackKeyLengthCandidate(byte[] cipher, byte[] key, int keyLength)
        {
            _cipher = cipher;
            _key = key;
            KeyLength = keyLength;
        }

        public int KeyLength { get; }
        public int PayloadLength => _cipher.Length;

        public byte GetDecryptedByte(int index)
        {
            if (index < 0 || index >= _cipher.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return (byte) (_cipher[index] ^ _key[index % KeyLength]);
        }

        public Stream OpenDecryptedStream(int offset = 0, int length = -1)
        {
            if (offset < 0 || offset > _cipher.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (length < 0)
            {
                length = _cipher.Length - offset;
            }

            if (length < 0 || length > _cipher.Length - offset)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            return new RepeatingXorReadStream(_cipher, _key, KeyLength, offset, length);
        }

        private sealed class RepeatingXorReadStream : Stream
        {
            private readonly byte[] _cipher;
            private readonly byte[] _key;
            private readonly int _keyLength;
            private readonly int _sourceOffset;
            private readonly int _length;
            private int _position;

            public RepeatingXorReadStream(byte[] cipher, byte[] key, int keyLength, int sourceOffset, int length)
            {
                _cipher = cipher;
                _key = key;
                _keyLength = keyLength;
                _sourceOffset = sourceOffset;
                _length = length;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                if (offset < 0 || count < 0 || offset > buffer.Length - count)
                {
                    throw new ArgumentOutOfRangeException();
                }

                var readCount = Math.Min(count, _length - _position);
                for (var i = 0; i < readCount; i++)
                {
                    var sourceIndex = _sourceOffset + _position + i;
                    buffer[offset + i] = (byte) (_cipher[sourceIndex] ^ _key[sourceIndex % _keyLength]);
                }

                _position += readCount;
                return readCount;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _length;
            public override long Position
            {
                get => _position;
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Reusable, length-limited output buffer for shell-specific candidate decompression.
    /// </summary>
    public sealed class MPackOutputBuffer : Stream
    {
        private readonly byte[] _buffer;

        public MPackOutputBuffer(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _buffer = new byte[capacity];
        }

        public int Written { get; private set; }
        public bool HasPsbSignature => Written >= 4 && _buffer[0] == (byte) 'P' &&
                                       _buffer[1] == (byte) 'S' && _buffer[2] == (byte) 'B' && _buffer[3] == 0;

        public void Reset()
        {
            Written = 0;
        }

        public MemoryStream AsMemoryStream()
        {
            return new MemoryStream(_buffer, 0, Written, false, true);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || count < 0 || offset > buffer.Length - count)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (count > _buffer.Length - Written)
            {
                throw new InvalidDataException("Decompressed MPack data exceeds the length declared in its header.");
            }

            Buffer.BlockCopy(buffer, offset, _buffer, Written, count);
            Written += count;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => Written;
        public override long Position
        {
            get => Written;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    public static partial class PsbExtension
    {
        /// <summary>
        /// Enumerates repeating MT key lengths and delegates shell-specific decompression to <paramref name="tryDecode"/>.
        /// </summary>
        /// <param name="encryptedPayload">Encrypted MPack payload, excluding the shell header.</param>
        /// <param name="key">Complete MPack seed (key + file name).</param>
        /// <param name="tryDecode">Returns a decompressed PSB stream for a viable candidate, or null otherwise.</param>
        /// <param name="keyLength">The shortest candidate that decompresses to a structurally valid PSB.</param>
        /// <param name="maxKeyLength">Maximum candidate, or the payload length when less than or equal to zero.</param>
        public static MemoryStream DecodeMPackWithInferredKeyLength(Stream encryptedPayload, string key,
            Func<MPackKeyLengthCandidate, MemoryStream> tryDecode, out int keyLength, int maxKeyLength = 0)
        {
            if (encryptedPayload == null)
            {
                throw new ArgumentNullException(nameof(encryptedPayload));
            }

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (tryDecode == null)
            {
                throw new ArgumentNullException(nameof(tryDecode));
            }

            var cipher = ReadRemainingBytes(encryptedPayload);
            var candidateLimit = maxKeyLength <= 0 ? cipher.Length : Math.Min(maxKeyLength, cipher.Length);
            if (candidateLimit <= 0)
            {
                throw new InvalidDataException("MPack payload is empty.");
            }

            var keyBytes = GenerateMpackKeyBytes(key, candidateLimit);
            for (var candidateLength = 1; candidateLength <= candidateLimit; candidateLength++)
            {
                var candidate = new MPackKeyLengthCandidate(cipher, keyBytes, candidateLength);
                var decoded = tryDecode(candidate);
                if (decoded == null)
                {
                    continue;
                }

                if (HasValidPsbStructure(decoded))
                {
                    keyLength = candidateLength;
                    decoded.Position = 0;
                    return decoded;
                }

                decoded.Dispose();
            }

            throw new InvalidDataException(
                $"Unable to infer MPack key length in range 1..{candidateLimit} for the specified key.");
        }

        private static MTRandom<mt19937ar_t> CreateMdfRandom(string key)
        {
            byte[] hash;
            using (var md5 = MD5.Create())
            {
                hash = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
            }

            var seeds = new[]
            {
                BitConverter.ToUInt32(hash, 0),
                BitConverter.ToUInt32(hash, 4),
                BitConverter.ToUInt32(hash, 8),
                BitConverter.ToUInt32(hash, 12)
            };
            return new MTRandom<mt19937ar_t>(seeds);
        }

        private static byte[] GenerateMpackKeyBytes(string key, int length)
        {
            var result = new byte[length];
            var random = CreateMdfRandom(key);
            Span<byte> word = stackalloc byte[4];
            var offset = 0;
            while (offset < length)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(word, random.GenerateUInt32());
                var copyLength = Math.Min(word.Length, length - offset);
                word.Slice(0, copyLength).CopyTo(result.AsSpan(offset, copyLength));
                offset += copyLength;
            }

            return result;
        }

        private static byte[] ReadRemainingBytes(Stream stream)
        {
            var originalPosition = stream.CanSeek ? stream.Position : 0;
            try
            {
                using (var copy = new MemoryStream())
                {
                    stream.CopyTo(copy);
                    return copy.ToArray();
                }
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = originalPosition;
                }
            }
        }

        private static bool HasValidPsbStructure(MemoryStream decoded)
        {
            try
            {
                decoded.Position = 0;
                _ = new PSB(decoded, false);
                decoded.Position = 0;
                return true;
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                return false;
            }
        }
    }
}
