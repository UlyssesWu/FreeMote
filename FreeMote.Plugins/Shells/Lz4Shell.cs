using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using K4os.Compression.LZ4.Streams;

namespace FreeMote.Plugins.Shells
{
    [Export(typeof(IPsbShell))]
    [ExportMetadata("Name", "FreeMote.Lz4")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "LZ4 support.")]
    class Lz4Shell : IPsbShell
    {
        /// <summary>
        /// LZ4 Frame Header Signature
        /// </summary>
        public const int MAGIC = 0x184D2204;

        public string Name => "LZ4";
        public bool IsInShell(Stream stream, Dictionary<string, object> context = null)
        {
            var header = new byte[4];
            var pos = stream.Position;
            stream.Read(header, 0, 4);
            stream.Position = pos;
            if (BitConverter.ToInt32(header, 0) == MAGIC)
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
            var ms = new MemoryStream();
            using (var decode = LZ4Stream.Decode(stream, leaveOpen:true))
            {
                decode.CopyTo(ms);
            }

            ms.Position = 0;
            return ms;
        }

        public MemoryStream ToShell(Stream stream, Dictionary<string, object> context = null)
        {
            var ms = new MemoryStream();
            using (var encode = LZ4Stream.Encode(ms, leaveOpen:true))
            {
                stream.CopyTo(encode);
            }
            ms.Position = 0;
            return ms;
        }

        public byte[] Signature => BitConverter.GetBytes(MAGIC);
    }
}
