using System.IO;
using System.IO.Compression;

namespace FreeMote
{
    public static class ZlibCompress
    {
        public static byte[] Uncompress(Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (DeflateStream deflateStream = new DeflateStream(input, CompressionMode.Decompress))
                {
                    deflateStream.CopyTo(ms);
                }
                return ms.ToArray();
            }
        }

        public static Stream UncompressToStream(Stream input)
        {
            MemoryStream ms = new MemoryStream();
            using (DeflateStream deflateStream = new DeflateStream(input, CompressionMode.Decompress))
            {
                deflateStream.CopyTo(ms);
            }
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public static byte[] Compress(Stream input, bool fast = false)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ms.WriteByte(0x78);
                ms.WriteByte((byte)(fast ? 0x9C : 0xDA));
                using (DeflateStream deflateStream = new DeflateStream(ms, fast ? CompressionLevel.Fastest : CompressionLevel.Optimal))
                {
                    input.CopyTo(deflateStream);
                }
                return ms.ToArray();
            }
        }

    }
}
