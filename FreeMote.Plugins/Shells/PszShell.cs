using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Text;

namespace FreeMote.Plugins.Shells
{
    [Export(typeof(IPsbShell))]
    [ExportMetadata("Name", "FreeMote.Psz")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "PSZ support.")]
    class PszShell : IPsbShell
    {
        public string Name => "PSZ";

        public bool IsInShell(Stream stream, Dictionary<string, object> context = null)
        {
            var header = new byte[4];
            var pos = stream.Position;
            stream.Read(header, 0, 4);
            stream.Position = pos;
            if (header[0] == Name[0] && header[1] == Name[1] && header[2] == Name[2] && header[3] == 0)
            {
                if (context != null)
                {
                    context[FreeMount.PsbShellType] = Name;
                }
                return true;
            }

            return false;
        }

        public MemoryStream ToPsb(Stream stream, Dictionary<string, object> context = null)
        {
            using (var br = new BinaryReader(stream))
            {
                br.ReadBytes(4); //PSZ
                var zippedLen = br.ReadInt32();
                var oriLen = br.ReadInt32();
                br.ReadInt32(); //0
                br.ReadByte(); //0x78
                var config = br.ReadByte(); //0x9C: fast; 0xDA: compact
                if (context != null)
                {
                    context[FreeMount.PsbZlibFastCompress] = config == (byte)0x9C;
                }

                return ZlibCompress.UncompressToStream(stream) as MemoryStream;
            }
        }

        public MemoryStream ToShell(Stream stream, Dictionary<string, object> context = null)
        {
            bool fast = false;
            if (context != null && context.ContainsKey(FreeMount.PsbZlibFastCompress))
            {
                fast = (bool)context[FreeMount.PsbZlibFastCompress];
            }

            var oriLen = (int)stream.Length;
            var pos = stream.Position;
            var compressedStream = ZlibCompress.CompressToStream(stream, fast);
            MemoryStream ms = new MemoryStream(16 + (int)compressedStream.Length);
            using (var bw = new BinaryWriter(ms, Encoding.UTF8, true))
            {
                stream.Position = pos;
                Adler32 checksumer = new Adler32();
                checksumer.Update(stream);
                var checksum = (uint)checksumer.Checksum;

                bw.Write(Signature);
                bw.Write((int)compressedStream.Length + 4);
                bw.Write(oriLen);
                bw.Write((int)0);
                compressedStream.CopyTo(ms);
                bw.WriteBE(checksum);
                compressedStream.Dispose();
            }

            ms.Position = 0;
            return ms;
        }

        public byte[] Signature { get; } = { (byte)'P', (byte)'S', (byte)'Z', 0 };
    }
}
