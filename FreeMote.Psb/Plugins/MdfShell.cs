using FreeMote.Psb;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using static FreeMote.Consts;

// ReSharper disable once CheckNamespace
namespace FreeMote.Plugins
{
    [Export(typeof(IPsbShell))]
    [ExportMetadata("Name", "FreeMote.Mdf")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "MDF (ZLIB) support.")]
    class MdfShell : IPsbShell
    {
        public const string ShellName = "MDF";
        public string Name => ShellName;

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
            bool encoded = false;
            if (context != null)
            {
                if (context.TryGetValue(Context_MdfKey, out var mdfKey))
                {
                    int? keyLength = context.TryGetValue(Context_MdfKeyLength, out var kl)
                        ? Convert.ToInt32(kl)
                        : (int?) null;

                    stream = PsbExtension.EncodeMdf(stream, (string) mdfKey, keyLength, true);
                    stream.Position = 0; //A new MemoryStream
                    encoded = true;
                    //File.WriteAllBytes("test.mdf", ((MemoryStream) stream).ToArray());
                    //stream.Position = 0;
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

            var result = MPack.MdfDecompressToStream(stream, size) as MemoryStream;
            if (encoded)
            {
                stream.Dispose();
            }

            return result;
        }

        public static void ToPsb(Stream stream, byte[] outBuffer, Dictionary<string, object> context = null)
        {
            bool encoded = false;
            Stream toBeDecompressedStream = stream;
            if (context != null)
            {
                if (context.TryGetValue(Context_MdfKey, out var mdfKey))
                {
                    int? keyLength = context.TryGetValue(Context_MdfKeyLength, out var kl)
                        ? Convert.ToInt32(kl)
                        : (int?) null;

                    toBeDecompressedStream = PsbExtension.EncodeMdf(stream, (string) mdfKey, keyLength, true);
                    encoded = true;
                }

                var pos = toBeDecompressedStream.Position;
                toBeDecompressedStream.Seek(4, SeekOrigin.Current);
                var bytes = new byte[4];
                toBeDecompressedStream.Read(bytes, 0, 4);
                toBeDecompressedStream.Seek(1, SeekOrigin.Current);
                context[Context_PsbZlibFastCompress] = toBeDecompressedStream.ReadByte() == (byte) 0x9C;
                toBeDecompressedStream.Position = pos;
            }

            toBeDecompressedStream.Seek(10, SeekOrigin.Begin);
            ZlibCompress.Decompress(toBeDecompressedStream, outBuffer);
            if (encoded)
            {
                toBeDecompressedStream.Dispose();
            }
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

                //TODO: inplace encode ms
                var mms = PsbExtension.EncodeMdf(ms, (string)mdfKey, keyLength, true);
                ms?.Dispose(); //ms disposed
                ms = mms;
            }

            return ms;
        }
    }
}