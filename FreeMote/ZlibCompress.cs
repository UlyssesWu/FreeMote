using System;
using System.IO;
using System.IO.Compression;
using Microsoft.IO;

namespace FreeMote
{
    public static class ZlibCompress
    {
        public static byte[] Decompress(Stream input)
        {
            using (var ms = Consts.MsManager.GetStream())
            {
                using (DeflateStream deflateStream = new DeflateStream(input, CompressionMode.Decompress))
                {
                    deflateStream.CopyTo(ms);
                }

                input.Dispose();
                return ms.ToArray();
            }
        }

        /// <summary>
        /// [RequireUsing]
        /// </summary>
        /// <param name="input"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static Stream DecompressToStream(Stream input, int size = 0)
        {
            MemoryStream ms = size <= 0
                ? Consts.MsManager.GetStream()
                : Consts.MsManager.GetStream("DecompressToStream", size);
            //var ms = Consts.MsManager.GetStream();
            using (DeflateStream deflateStream = new DeflateStream(input, CompressionMode.Decompress))
            {
                deflateStream.CopyTo(ms);
            }

            input.Dispose();
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public static byte[] Compress(Stream input, bool fast = false)
        {
            using var ms = new MemoryStream();
            ms.WriteByte(0x78);
            ms.WriteByte((byte) (fast ? 0x9C : 0xDA));
            using (DeflateStream deflateStream =
                new DeflateStream(ms, fast ? CompressionLevel.Fastest : CompressionLevel.Optimal))
            {
                input.CopyTo(deflateStream);
            }

            input.Dispose();
            return ms.GetBuffer();
        }

        public static void CompressToBinaryWriter(BinaryWriter bw, Stream input, bool fast = false)
        {
            using var output = CompressToStream(input, fast);
            output.CopyTo(bw.BaseStream);
        }

        public static MemoryStream CompressToStream(Stream input, bool fast = false)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x78);
            ms.WriteByte((byte) (fast ? 0x9C : 0xDA));
            using (DeflateStream deflateStream =
                new DeflateStream(ms, fast ? CompressionLevel.Fastest : CompressionLevel.Optimal, true))
            {
                input.CopyTo(deflateStream);
            }

            input.Dispose();
            ms.Position = 0;
            return ms;
        }
    }
}