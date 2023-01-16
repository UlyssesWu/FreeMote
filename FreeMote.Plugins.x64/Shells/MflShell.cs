﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using FreeMote.FastLz;
using FreeMote.Psb;
using static FreeMote.Consts;

namespace FreeMote.Plugins.Shells
{
    [Export(typeof(IPsbShell))]
    [ExportMetadata("Name", "FreeMote.Mfl")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "MFL (FastLZ) support.")]
    class MflShell : IPsbShell
    {
        public string Name => "MFL";

        public byte[] Signature => new byte[] { (byte)'m', (byte)'f', (byte)'l', 0 };

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
            if (context != null)
            {
                if (context.ContainsKey(Context_MdfKey))
                {
                    uint? keyLength = context.ContainsKey(Context_MdfKeyLength)
                        ? Convert.ToUInt32(context[Context_MdfKeyLength])
                        : (uint?)null;

                    stream = PsbExtension.EncodeMdf(stream, (string)context[Context_MdfKey], keyLength);
                    stream.Position = 0; //A new MemoryStream
                }
            }

            stream.Seek(4, SeekOrigin.Current);
            var bytes = new byte[4];
            stream.Read(bytes, 0, 4);
            var unzippedSize = BitConverter.ToInt32(bytes, 0);

            byte[] input = new byte[stream.Length - 8];
            stream.Read(input, 0, input.Length);

            var output = FastLzNative.Decompress(input, unzippedSize);
            if (output == null)
            {
                throw new InvalidDataException("Fast LZ decompress failed");
            }

            return new MemoryStream(output);
        }


        public MemoryStream ToShell(Stream stream, Dictionary<string, object> context = null)
        {
            byte[] input;
            if (stream is MemoryStream inputMs)
            {
                input = inputMs.ToArray();
            }
            else
            {
                input = new byte[stream.Length];
                stream.Read(input, 0, input.Length);
            }

            var output = FastLzNative.Compress(input);
            var ms = new MemoryStream(output);

            if (context != null && context.ContainsKey(Context_MdfKey))
            {
                uint? keyLength;
                if (context.ContainsKey(Context_MdfKeyLength))
                {
                    keyLength = Convert.ToUInt32(context[Context_MdfKeyLength]);
                }
                else
                {
                    keyLength = (uint?)null;
                }

                var mms = PsbExtension.EncodeMdf(ms, (string)context[Context_MdfKey], keyLength);
                ms?.Dispose(); //ms disposed
                ms = mms;
            }

            return ms;
        }
        
    }
}