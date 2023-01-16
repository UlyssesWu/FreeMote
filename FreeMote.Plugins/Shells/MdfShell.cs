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
            stream.Read(header, 0, 4);
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
                if (context.ContainsKey(Context_MdfKey))
                {
                    uint? keyLength = context.ContainsKey(Context_MdfKeyLength)
                        ? Convert.ToUInt32(context[Context_MdfKeyLength])
                        : (uint?) null;

                    stream = PsbExtension.EncodeMdf(stream, (string) context[Context_MdfKey], keyLength);
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

            return MPack.MdfDecompressToPsbStream(stream, size) as MemoryStream;
        }

        
        public MemoryStream ToShell(Stream stream, Dictionary<string, object> context = null)
        {
            bool fast = true; //mdf use fast mode by default
            if (context != null && context.ContainsKey(Context_PsbZlibFastCompress))
            {
                fast = (bool) context[Context_PsbZlibFastCompress];
            }

            var ms = MPack.CompressPsbToMdfStream(stream, fast);

            if (context != null && context.ContainsKey(Context_MdfKey))
            {
                uint? keyLength;
                if (context.ContainsKey(Context_MdfKeyLength))
                {
                    keyLength = Convert.ToUInt32(context[Context_MdfKeyLength]);
                }
                else
                {
                    keyLength = (uint?) null;
                }

                var mms = PsbExtension.EncodeMdf(ms, (string)context[Context_MdfKey], keyLength);
                ms?.Dispose(); //ms disposed
                ms = mms;
            }

            return ms;
        }
    }
}