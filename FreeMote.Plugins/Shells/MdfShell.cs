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

        public List<byte[]> Signatures = new()
            {new byte[] {(byte) 'm', (byte) 'd', (byte) 'f', 0}, new byte[] {(byte) 'm', (byte) 'f', (byte) 'l', 0}};

        public bool IsInShell(Stream stream, Dictionary<string, object> context = null)
        {
            var header = new byte[4];
            var pos = stream.Position;
            stream.Read(header, 0, 4);
            stream.Position = pos;
            foreach (var signature in Signatures)
            {
                if (header.SequenceEqual(signature))
                {
                    if (context != null)
                    {
                        context[Context_PsbShellType] = Name;
                    }

                    return true;
                }
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

            return MdfFile.DecompressToPsbStream(stream, size) as MemoryStream;
        }

        /// <summary>
        /// Decode/encode MDF used in archive PSB. (<paramref name="stream"/> will be disposed)
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="key"></param>
        /// <param name="keyLength"></param>
        /// <param name="keepHeader"></param>
        /// <returns></returns>
        internal MemoryStream EncodeMdf(Stream stream, string key, uint? keyLength, bool keepHeader = true)
        {
            //var bts = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes("1232ab23478cdconfig_info.psb.m"));
            var bts = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(key));
            uint[] seeds = new uint[4];
            seeds[0] = BitConverter.ToUInt32(bts, 0);
            seeds[1] = BitConverter.ToUInt32(bts, 1 * 4);
            seeds[2] = BitConverter.ToUInt32(bts, 2 * 4);
            seeds[3] = BitConverter.ToUInt32(bts, 3 * 4);

            MemoryStream ms = new MemoryStream((int) stream.Length); //MsManager.GetStream("EncodeMdf", (int)stream.Length);
            var gen = new MT19937Generator(seeds);

            using BinaryReader br = new BinaryReader(stream);
            using BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8, true);
            if (keepHeader)
            {
                bw.Write(br.ReadBytes(8));
            }
            //uint count = 0;

            List<byte> keys = new List<byte>();
            if (keyLength != null)
            {
                for (int i = 0; i < keyLength / 4 + 1; i++)
                {
                    keys.AddRange(BitConverter.GetBytes(gen.NextUIntInclusiveMaxValue()));
                }

                keys = keys.GetRange(0, (int)keyLength.Value);
                //keys = keys.Take((int) keyLength.Value).ToList();
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

            var ms = MdfFile.CompressPsbToMdfStream(stream, fast);

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

                var mms = EncodeMdf(ms, (string)context[Context_MdfKey], keyLength);
                ms?.Dispose(); //ms disposed
                ms = mms;
            }

            return ms;
        }

        public byte[] Signature { get; } = null;
    }
}