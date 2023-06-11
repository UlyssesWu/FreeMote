using FreeMote.Psb;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using XMemCompress;
using static FreeMote.Consts;

namespace FreeMote.Plugins.Shells
{
    [Export(typeof(IPsbShell))]
    [ExportMetadata("Name", "FreeMote.Mxb")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "MXB (XMemCompress) support.")]
    internal class MxbShell : IPsbShell
    {
        public string Name => "MXB";
        public byte[] Signature { get; } = { (byte) 'm', (byte) 'x', (byte) 'b', 0 };

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
            if (context != null)
            {
                if (context.TryGetValue(Context_MdfKey, out var mdfKey))
                {
                    uint? keyLength = context.TryGetValue(Context_MdfKeyLength, out var kl)
                        ? Convert.ToUInt32(kl)
                        : (uint?) null;

                    stream = PsbExtension.EncodeMdf(stream, (string) mdfKey, keyLength, true);
                    stream.Position = 0; //A new MemoryStream
                }
            }

            stream.Seek(4, SeekOrigin.Current);
            var bytes = new byte[4];
            _ = stream.Read(bytes, 0, 4);
            var unzippedSize = BitConverter.ToInt32(bytes, 0);

            var ms = new MemoryStream(unzippedSize);
            XCompressFile.DecompressStream(stream, ms);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public MemoryStream ToShell(Stream stream, Dictionary<string, object> context = null)
        {
            var unzipLength = (int)stream.Length;
            var ms = XCompressFile.CompressStream(stream);
            if (context != null && context.TryGetValue(Context_MdfKey, out var mdfKey))
            {
                uint? keyLength;
                if (context.TryGetValue(Context_MdfKeyLength, out var kl))
                {
                    keyLength = Convert.ToUInt32(kl);
                }
                else
                {
                    keyLength = (uint?) null;
                }

                var mms = PsbExtension.EncodeMdf(ms, (string) mdfKey, keyLength, false);
                ms?.Dispose(); //ms disposed
                ms = mms;
            }

            ms.Seek(0, SeekOrigin.Begin);
            var shellMs = new MemoryStream((int) (ms.Length + 8));
            shellMs.Write(Signature, 0, 4);
            shellMs.Write(BitConverter.GetBytes(unzipLength), 0, 4);
            ms.CopyTo(shellMs);
            ms.Dispose();
            shellMs.Seek(0, SeekOrigin.Begin);
            return shellMs;
        }
    }
}
