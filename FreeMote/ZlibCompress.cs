using System.IO;
using System.IO.Compression;

namespace FreeMote
{
    public static class ZlibCompress
    {
        public static byte[] Decompress(Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (DeflateStream deflateStream = new DeflateStream(input, CompressionMode.Decompress))
                {
                    deflateStream.CopyTo(ms);
                }

                input.Dispose();
                return ms.ToArray();
            }
        }

        public static Stream DecompressToStream(Stream input, int size = 0)
        {
            MemoryStream ms = size <= 0 ? new MemoryStream() : new MemoryStream(size);
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
            using (MemoryStream ms = new MemoryStream())
            {
                ms.WriteByte(0x78);
                ms.WriteByte((byte) (fast ? 0x9C : 0xDA));
                using (DeflateStream deflateStream =
                    new DeflateStream(ms, fast ? CompressionLevel.Fastest : CompressionLevel.Optimal))
                {
                    input.CopyTo(deflateStream);
                }

                input.Dispose();
                return ms.ToArray();
            }
        }

        public static Stream CompressToStream(Stream input, bool fast = false)
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