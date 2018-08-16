using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;

namespace FreeMote.Plugins
{
    [Export(typeof(IPsbShell))]
    [ExportMetadata("Name", "FreeMote.Mdf")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "MDF support.")]
    class MdfShell : IPsbShell
    {
        public string Name => "MDF";

        public bool IsInShell(Stream stream, Dictionary<string, object> context = null)
        {
            var header = new byte[4];
            var pos = stream.Position;
            stream.Read(header, 0, 4);
            stream.Position = pos;
            if (header.SequenceEqual(Signature))
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
            if (context != null)
            {
                var pos = stream.Position;
                stream.Seek(9, SeekOrigin.Current);
                context[FreeMount.PsbZlibFastCompress] = stream.ReadByte() == (byte)0x9C;
                stream.Position = pos;
            }
            return MdfFile.UncompressToPsbStream(stream) as MemoryStream;
        }

        public MemoryStream ToShell(Stream stream, Dictionary<string, object> context = null)
        {
            bool fast = true; //mdf use fast mode by default
            if (context != null && context.ContainsKey(FreeMount.PsbZlibFastCompress))
            {
                fast = (bool)context[FreeMount.PsbZlibFastCompress];
            }
            return MdfFile.CompressPsbToMdfStream(stream, fast) as MemoryStream;
        }

        public byte[] Signature { get; } = {(byte) 'm', (byte) 'd', (byte) 'f', 0};
    }
}
