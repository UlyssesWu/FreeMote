using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using FreeMote.Psb;
using static FreeMote.Consts;

namespace FreeMote.Plugins.Shells
{
    [Export(typeof(IPsbShell))]
    [ExportMetadata("Name", "FreeMote.Mdf")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "MDF (ZLIB) support.")]
    class MdfShell : IPsbShell
    {
        public string Name => "MDF";

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
            int size = 0;
            if (context != null)
            {
                if (context.TryGetValue(Context_MdfKey, out var mdfKey))
                {
                    int? keyLength = context.TryGetValue(Context_MdfKeyLength, out var kl)
                        ? Convert.ToInt32(kl)
                        : (int?) null;

                    stream = PsbExtension.EncodeMdf(stream, (string) mdfKey, keyLength, true);
                    stream.Position = 0; //A new MemoryStream
                }

                var pos = stream.Position;
                stream.Seek(4, SeekOrigin.Current);
                var bytes = new byte[4];
                stream.Read(bytes, 0, 4);
                if (FastMode)
                {
                    size = BitConverter.ToInt32(bytes, 0);
                }
                stream.Seek(1, SeekOrigin.Current);
                context[Context_PsbZlibFastCompress] = stream.ReadByte() == (byte) 0x9C;
                stream.Position = pos;
            }

            return MPack.MdfDecompressToStream(stream, size) as MemoryStream;
        }

        
        public MemoryStream ToShell(Stream stream, Dictionary<string, object> context = null)
        {
            bool fast = true; //mdf use fast mode by default
            if (context != null && context.TryGetValue(Context_PsbZlibFastCompress, out var fastCompress))
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

                var mms = PsbExtension.EncodeMdf(ms, (string)mdfKey, keyLength, true);
                ms?.Dispose(); //ms disposed
                ms = mms;
            }

            return ms;
        }
    }
}