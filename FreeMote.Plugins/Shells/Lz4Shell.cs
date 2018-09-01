using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using LZ4.Frame;
//TODO: switch to CLI solution?

namespace FreeMote.Plugins.Shells
{
    [Export(typeof(IPsbShell))]
    [ExportMetadata("Name", "FreeMote.Lz4")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "LZ4 support via LZ4.Frame.")]
    class Lz4Shell : IPsbShell
    {
        public string Name => "LZ4";
        public bool IsInShell(Stream stream, Dictionary<string, object> context = null)
        {
            var header = new byte[4];
            var pos = stream.Position;
            stream.Read(header, 0, 4);
            stream.Position = pos;
            if (BitConverter.ToInt32(header,0) == LZ4Frame.MAGIC)
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
            return new MemoryStream(LZ4Frame.Decompress(stream));
        }

        public MemoryStream ToShell(Stream stream, Dictionary<string, object> context = null)
        {
            return new MemoryStream(LZ4Frame.Compress(stream));
        }

        public byte[] Signature => BitConverter.GetBytes(LZ4Frame.MAGIC);
    }
}
