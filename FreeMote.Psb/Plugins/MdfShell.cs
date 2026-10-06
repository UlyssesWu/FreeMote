using FreeMote.Psb;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using static FreeMote.Consts;

// ReSharper disable once CheckNamespace
namespace FreeMote.Plugins
{
    [Export(typeof(IPsbShell))]
    [ExportMetadata("Name", "FreeMote.Mdf")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "MDF (ZLIB) support.")]
    class MdfShell : IPsbShell, IPsbShellKeyLengthInferer
    {
        public const string ShellName = "MDF";
        public string Name => ShellName;

        public byte[] Signature => new byte[] {(byte) 'm', (byte) 'd', (byte) 'f', 0};

        public bool IsInShell(Stream stream, Dictionary<string, object> context = null)
        {
            var header = new byte[4];
            var pos = stream.Position;
            _ = stream.Read(header, 0, 4);
            stream.Position = pos;
            if (header.SequenceEqual(Signature))
            {
                if (context != null)
                {
                    context[Context_PsbShellType] = Name;
                }

                return true;
            }

            return false;
        }

        public MemoryStream ToPsb(Stream stream, Dictionary<string, object> context = null)
        {
            var decrypted = Decrypt(stream, context);
            try
            {
                var result = (MemoryStream) MPack.MdfDecompressToStream(decrypted);
                SetCompressionContext(decrypted, context);
                return result;
            }
            finally
            {
                if (decrypted != stream)
                {
                    decrypted.Dispose();
                }
            }
        }

        public static void ToPsb(Stream stream, byte[] outBuffer, Dictionary<string, object> context = null)
        {
            var decrypted = Decrypt(stream, context);
            try
            {
                MPack.MdfDecompress(decrypted, outBuffer);
                SetCompressionContext(decrypted, context);
            }
            finally
            {
                if (decrypted != stream)
                {
                    decrypted.Dispose();
                }
            }
        }

        private static Stream Decrypt(Stream stream, Dictionary<string, object> context)
        {
            stream.Position = 0;
            if (context != null && context.TryGetValue(Context_MdfKey, out var key))
            {
                int? length = context.TryGetValue(Context_MdfKeyLength, out var kl) ? Convert.ToInt32(kl) : (int?) null;
                var decrypted = PsbExtension.EncodeMdf(stream, (string) key, length, true);
                decrypted.Position = 0;
                return decrypted;
            }
            return stream;
        }

        private static void SetCompressionContext(Stream stream, Dictionary<string, object> context)
        {
            if (context == null)
            {
                return;
            }
            var pos = stream.Position;
            stream.Position = 9;
            context[Context_PsbZlibFastCompress] = stream.ReadByte() == 0x9C;
            stream.Position = pos;
        }

        public MemoryStream ToPsbWithInferredKeyLength(Stream stream, string key, out int keyLength,
            Dictionary<string, object> context = null)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var originalPosition = stream.CanSeek ? stream.Position : 0;
            try
            {
                var header = new byte[8];
                if (stream.Read(header, 0, header.Length) != header.Length ||
                    !header.Take(4).SequenceEqual(Signature))
                {
                    throw new InvalidDataException("Invalid MDF header.");
                }

                var expectedLength = BitConverter.ToInt32(header, 4);
                if (expectedLength < 4)
                {
                    throw new InvalidDataException("Invalid MDF decompressed length.");
                }

                var output = new byte[expectedLength];
                var fastCompression = false;
                var result = PsbExtension.DecodeMPackWithInferredKeyLength(stream, key, candidate =>
                {
                    if (candidate.PayloadLength < 7)
                    {
                        return null;
                    }

                    var cmf = candidate.GetDecryptedByte(0);
                    var flg = candidate.GetDecryptedByte(1);
                    if ((cmf & 0x0F) != 8 || (cmf >> 4) > 7 || (flg & 0x20) != 0 ||
                        ((cmf << 8) + flg) % 31 != 0)
                    {
                        return null;
                    }

                    try
                    {
                        using var decrypted = candidate.OpenDecryptedStream();
                        ZlibCompress.DecompressZlib(decrypted, output, expectedLength);
                    }
                    catch (Exception e) when (e is InvalidDataException || e is IOException)
                    {
                        return null;
                    }

                    if (output[0] != 'P' || output[1] != 'S' || output[2] != 'B' || output[3] != 0)
                    {
                        return null;
                    }

                    fastCompression = flg == 0x9C;
                    return new MemoryStream(output, false);
                }, out keyLength);

                if (context != null)
                {
                    context[Context_PsbZlibFastCompress] = fastCompression;
                }

                return result;
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = originalPosition;
                }
            }
        }
        
        public MemoryStream ToShell(Stream stream, Dictionary<string, object> context = null)
        {
            bool fast = true; //mdf use fast mode by default
            if (context != null && context.TryGetValue(Context_PsbShellCompression, out var compression))
            {
                if (compression is string value)
                {
                    if (value.Equals("fast", StringComparison.OrdinalIgnoreCase))
                    {
                        fast = true;
                    }
                    else if (value.Equals("best", StringComparison.OrdinalIgnoreCase))
                    {
                        fast = false;
                    }
                }
            }
            else if (context != null && context.TryGetValue(Context_PsbZlibFastCompress, out var fastCompress))
            {
                fast = (bool) fastCompress;
            }

            var ms = MPack.CompressPsbToMdfStream(stream, fast); //this will prepend MDF header

            if (context != null && context.TryGetValue(Context_MdfKey, out var mdfKey))
            {
                int? keyLength;
                if (context.TryGetValue(Context_MdfKeyLength, out var kl))
                {
                    keyLength = Convert.ToInt32(kl);
                }
                else
                {
                    keyLength = (int?) null;
                }

                //TODO: inplace encode ms
                var mms = PsbExtension.EncodeMdf(ms, (string)mdfKey, keyLength, true);
                ms?.Dispose(); //ms disposed
                ms = mms;
            }

            return ms;
        }
    }
}
