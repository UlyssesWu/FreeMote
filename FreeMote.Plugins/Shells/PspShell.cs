// LZSS decompression by morkt (https://github.com/morkt/GARbro) LICENSE: MIT

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;

namespace FreeMote.Plugins.Shells
{
    [Export(typeof(IPsbShell))]
    [ExportMetadata("Name", "FreeMote.Psp")]
    [ExportMetadata("Author", "Ulysses & morkt")]
    [ExportMetadata("Comment", "PSP (LZSS) unpack support.")]
    class PspShell : IPsbShell
    {
        public string Name => "PSP";

        //40 C0 A6 01 FF | 50 53 42 (P S B)
        public static byte[] MAGIC => new[] {(byte) 'P', (byte) 'S', (byte) 'B'};

        public bool IsInShell(Stream stream, Dictionary<string, object> context = null)
        {
            var header = new byte[3];
            var pos = stream.Position;
            stream.Seek(5, SeekOrigin.Current);
            stream.Read(header, 0, 3);
            stream.Position = pos;
            if (header.SequenceEqual(MAGIC))
            {
                return true;
            }

            return false;
        }

        public MemoryStream ToPsb(Stream stream, Dictionary<string, object> context = null)
        {
            MemoryStream ms = null;
            using (BinaryReader br = new BinaryReader(stream))
            {
                int unpackedSize = br.ReadInt32();
                ms = new MemoryStream(unpackedSize);
                using (BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8, true))
                {
                    //var output = new byte[unpackedSize];
                    //int dst = 0;
                    var frame = new byte[0x1000];
                    int framePos = 1;
                    while (ms.Length < unpackedSize)
                    {
                        int ctl = br.ReadByte();
                        for (int bit = 1; ms.Length < unpackedSize && bit != 0x100; bit <<= 1)
                        {
                            if (0 != (ctl & bit))
                            {
                                byte b = br.ReadByte();
                                bw.Write(frame[framePos++ & 0xFFF] = b);
                            }
                            else
                            {
                                int hi = br.ReadByte();
                                int lo = br.ReadByte();
                                int offset = hi << 4 | lo >> 4;
                                for (int count = 2 + (lo & 0xF); count != 0; --count)
                                {
                                    byte v = frame[offset++ & 0xFFF];
                                    bw.Write(frame[framePos++ & 0xFFF] = v);
                                }
                            }
                        }
                    }
                }
            }

            ms.Position = 0;
            return ms;
        }

        public MemoryStream ToShell(Stream stream, Dictionary<string, object> context = null)
        {
            //TODO:
            Console.WriteLine("PSP compression is not supported.");
            return null;
            //throw new NotImplementedException("PSP compression is not supported.");
        }

        public byte[] Signature => null;
    }
}