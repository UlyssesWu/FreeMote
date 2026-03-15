using System.IO;
using System.IO.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace FreeMote
{
    public static class ZlibCompress
    {
        public static byte[] Decompress(Stream input)
        {
            using var ms = new MemoryStream();
            using (DeflateStream deflateStream = new DeflateStream(input, CompressionMode.Decompress))
            {
                deflateStream.CopyTo(ms);
            }

            //input.Dispose();
            return ms.ToArray();
        }

        public static void Decompress(Stream input, byte[] output)
        {
            using var ms = new MemoryStream(output);
            using DeflateStream deflateStream = new DeflateStream(input, CompressionMode.Decompress);
            deflateStream.CopyTo(ms);
            return;
        }

        /// <summary>
        /// [RequireUsing]
        /// </summary>
        /// <param name="input"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static Stream DecompressToStream(Stream input, int size = 0)
        {
            //MemoryStream ms = size <= 0
            //    ? Consts.MsManager.GetStream()
            //    : Consts.MsManager.GetStream("DecompressToStream", size);
            //var ms = Consts.MsManager.GetStream();
            var ms = size <= 0 ? new MemoryStream() : new MemoryStream(size);
            using (DeflateStream deflateStream = new DeflateStream(input, CompressionMode.Decompress))
            {
                deflateStream.CopyTo(ms);
            }

            //input.Dispose();
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }
        
        public static byte[] Compress(Stream input, bool fast = false)
        {
            using var ms = new MemoryStream();
            ms.WriteByte(0x78);
            ms.WriteByte((byte) (fast ? 0x9C : 0xDA));
            if (UseSharpZipLib(fast))
            {
                var deflaterLevel = GetDeflaterLevel(fast);
                var deflater = new Deflater(deflaterLevel, true);
                using (var deflateStream = new DeflaterOutputStream(ms, deflater))
                {
                    deflateStream.IsStreamOwner = false;
                    input.CopyTo(deflateStream);
                    deflateStream.Finish();
                }
            }
            else
            {
                using (DeflateStream deflateStream = new DeflateStream(ms, GetSystemCompressionLevel(fast), true))
                {
                    input.CopyTo(deflateStream);
                }
            }

            //input.Dispose();
            return ms.GetBuffer();
        }

        public static void CompressToBinaryWriter(BinaryWriter bw, Stream input, bool fast = false)
        {
            using var output = CompressToStream(input, fast);
            output.CopyTo(bw.BaseStream);
        }

        /// <summary>
        /// Will NOT dispose input stream
        /// </summary>
        /// <param name="input"></param>
        /// <param name="fast"></param>
        /// <returns></returns>
        public static MemoryStream CompressToStream(Stream input, bool fast = false)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x78);
            ms.WriteByte((byte) (fast ? 0x9C : 0xDA));
            if (UseSharpZipLib(fast))
            {
                var deflaterLevel = GetDeflaterLevel(fast);
                var deflater = new Deflater(deflaterLevel, true);
                using (var deflateStream = new DeflaterOutputStream(ms, deflater))
                {
                    deflateStream.IsStreamOwner = false;
                    input.CopyTo(deflateStream);
                    deflateStream.Finish();
                }
            }
            else
            {
                using (DeflateStream deflateStream = new DeflateStream(ms, GetSystemCompressionLevel(fast), true))
                {
                    input.CopyTo(deflateStream);
                }
            }

            //input.Dispose(); //DO NOT dispose
            ms.Position = 0;
            return ms;
        }

        private static int GetDeflaterLevel(bool fast)
        {
            if (Consts.ForceCompressionLevel != null)
            {
                switch (Consts.ForceCompressionLevel.Value)
                {
                    case CompressionLevel.NoCompression:
                        return Deflater.NO_COMPRESSION;
                    case CompressionLevel.Fastest:
                        return Deflater.BEST_SPEED;
                    case CompressionLevel.Optimal:
                        return Deflater.BEST_COMPRESSION;
                    default:
                        return fast ? Deflater.BEST_SPEED : Deflater.BEST_COMPRESSION;
                }
            }

            // MDF body in real games usually uses max deflate level (0xDA).
            return fast ? Deflater.BEST_SPEED : Deflater.BEST_COMPRESSION;
        }

        private static CompressionLevel GetSystemCompressionLevel(bool fast)
        {
            if (Consts.ForceCompressionLevel != null)
            {
                return Consts.ForceCompressionLevel.Value;
            }

            return fast ? CompressionLevel.Fastest : CompressionLevel.Optimal;
        }

        private static bool UseSharpZipLib(bool fast)
        {
            if (fast)
            {
                return false;
            }

            // Only use SharpZipLib in optimize mode, so normal mode keeps .NET Deflate speed.
            return Consts.OptimizeMode;
        }
    }
}