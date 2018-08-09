using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;

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
            if (header[0] == MdfFile.Signature[0] && header[1] == MdfFile.Signature[1] && header[2] == MdfFile.Signature[2] && header[3] == 0)
            {
                if (context != null)
                {
                    context[FreeMount.PsbShellType] = Name;
                }
                return true;
            }

            return false;
        }

        public Stream ToPsb(Stream stream, Dictionary<string, object> context = null)
        {
            return MdfFile.UncompressToPsbStream(stream);
        }

        public Stream ToShell(Stream stream, Dictionary<string, object> context = null)
        {
            return MdfFile.CompressPsbToMdfStream(stream);
        }
    }
}
