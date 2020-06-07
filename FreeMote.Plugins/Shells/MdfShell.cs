using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Troschuetz.Random.Generators;
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
                    stream = EncodeMdf(stream, (string) context[Context_MdfKey], keyLength);
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

            return MdfFile.DecompressToPsbStream(stream, size) as MemoryStream;
        }

        internal MemoryStream EncodeMdf(Stream stream, string key, uint? keyLength)
        {
            //var bts = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes("1232ab23478cdconfig_info.psb.m"));
            var bts = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(key));
            uint[] seeds = new uint[4];
            seeds[0] = BitConverter.ToUInt32(bts, 0);
            seeds[1] = BitConverter.ToUInt32(bts, 1 * 4);
            seeds[2] = BitConverter.ToUInt32(bts, 2 * 4);
            seeds[3] = BitConverter.ToUInt32(bts, 3 * 4);

            MemoryStream ms = new MemoryStream((int) stream.Length);
            var gen = new MT19937Generator(seeds);

            BinaryReader br = new BinaryReader(stream);
            BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8, true);
            bw.Write(br.ReadBytes(8));
            //uint count = 0;
            List<byte> keys = new List<byte>();
            if (keyLength != null)
            {
                for (int i = 0; i < keyLength / 4 + 1; i++)
                {
                    keys.AddRange(BitConverter.GetBytes(gen.NextUIntInclusiveMaxValue()));
                }

                keys = keys.Take((int) keyLength.Value).ToList();
            }
            else
            {
                while (keys.Count < br.BaseStream.Length)
                {
                    keys.AddRange(BitConverter.GetBytes(gen.NextUIntInclusiveMaxValue()));
                }
            }

            int currentKey = 0;
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                var current = br.ReadByte();
                if (currentKey >= keys.Count)
                {
                    currentKey = 0;
                }

                current ^= keys[currentKey];
                currentKey++;

                //if (keyLength == null || (count < keyLength.Value))
                //{
                //    current ^= gen.NextUIntInclusiveMaxValue();
                //}
                bw.Write(current);
            }

            return ms;
        }

        public MemoryStream ToShell(Stream stream, Dictionary<string, object> context = null)
        {
            bool fast = true; //mdf use fast mode by default
            if (context != null && context.ContainsKey(Context_PsbZlibFastCompress))
            {
                fast = (bool) context[Context_PsbZlibFastCompress];
            }

            var ms = MdfFile.CompressPsbToMdfStream(stream, fast) as MemoryStream;

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

                ms = EncodeMdf(ms, (string) context[Context_MdfKey], keyLength);
            }

            return ms;
        }

        public byte[] Signature { get; } = {(byte) 'm', (byte) 'd', (byte) 'f', 0};
    }
}