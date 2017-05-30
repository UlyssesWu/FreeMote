using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FreeMote
{
    internal class PsbHeader
    {
        public char[] Signature { get; set; } = new char[4];
        public ushort Version { get; set; } = 2;

        /// <summary>
        /// If 1, the header seems encrypted, which add more difficulty to us
        /// <para>But doesn't really matters since usually it's always encrypted in v3+</para>
        /// </summary>
        public ushort HeaderEncrypt { get; set; } = 0;

        /// <summary>
        /// Offset of encryption bytes
        /// <para>Usually same as <see cref="OffsetNames"/> if encrypted, but it's still 0x2C in v4 while it should be 0x38</para>
        /// </summary>
        public uint OffsetEncrypt { get; set; } = 0;

        /// <summary>
        /// Header Length
        /// <para>Usually the beginning of encryption</para>
        /// </summary>
        public uint OffsetNames { get; set; }

        public uint OffsetStrings { get; set; }

        public uint OffsetStringsData { get; set; }

        /// <summary>
        /// ResOffTable
        /// </summary>
        public uint OffsetChunkOffsets { get; set; }
        public uint OffsetChunkLengths { get; set; }

        /// <summary>
        /// Offset of Chunk Data (Image)
        /// </summary>
        public uint OffsetChunkData { get; set; }

        /// <summary>
        /// Entry Offset
        /// </summary>
        public uint OffsetEntries { get; set; }

        /// <summary>
        /// [New in v3] Adler32 Checksum for header
        /// <para>Not always checked in v3. Sadly, it's always checked from v4, so we have to handle it.</para>
        /// </summary>
        public uint Checksum { get; set; }

        /// <summary>
        /// [New in v4] Usually an empty array (3 bytes)
        /// <para><see cref="OffsetResourceOffsets"/> - 6</para>
        /// </summary>
        public uint OffsetUnknown1 { get; set; }
        /// <summary>
        /// [New in v4] Usually an empty array (3 bytes)
        /// <para><see cref="OffsetResourceOffsets"/> - 3</para>
        /// </summary>
        public uint OffsetUnknown2 { get; set; }
        /// <summary>
        /// [New in v4] Usually same as <see cref="OffsetChunkOffsets"/>
        /// </summary>
        public uint OffsetResourceOffsets { get; set; }

        public static PsbHeader Load(BinaryReader br)
        {
            PsbHeader header = new PsbHeader
            {
                Signature = br.ReadChars(4),
                Version = br.ReadUInt16(),
                HeaderEncrypt = br.ReadUInt16(),
                OffsetEncrypt = br.ReadUInt32(),
                OffsetNames = br.ReadUInt32()
            };
            //if (header.HeaderEncrypt != 0) //following header is possibly encrypted
            //{
            //    return header;
            //}
            if (header.OffsetEncrypt < br.BaseStream.Length 
                && header.OffsetNames < br.BaseStream.Length 
                && (header.OffsetEncrypt == header.OffsetNames || header.OffsetEncrypt == 0))
            {
                header.OffsetStrings = br.ReadUInt32();
                header.OffsetStringsData = br.ReadUInt32();
                header.OffsetChunkOffsets = br.ReadUInt32();
                header.OffsetChunkLengths = br.ReadUInt32();
                header.OffsetChunkData = br.ReadUInt32();
                header.OffsetEntries = br.ReadUInt32();
                if (header.Version > 2)
                {
                    header.Checksum = br.ReadUInt32();
                }
                if (header.Version > 3)
                {
                    header.OffsetUnknown1 = br.ReadUInt32();
                    header.OffsetUnknown2 = br.ReadUInt32();
                    header.OffsetResourceOffsets = br.ReadUInt32();
                }
            }
            //else
            //{
            //    throw new NotSupportedException("Header seems encrypted.");
            //}
            return header;
        }
        
        public static PsbHeader Load(BinaryReader br, uint key)
        {
            PsbHeader header = new PsbHeader
            {
                Signature = br.ReadChars(4),
                Version = br.ReadUInt16(),
                HeaderEncrypt = br.ReadUInt16()
            };
            PsbStreamContext context = new PsbStreamContext(key);

            header.OffsetEncrypt = context.ReadUInt32(br);
            header.OffsetNames = context.ReadUInt32(br);
            header.OffsetStrings = context.ReadUInt32(br);
            header.OffsetStringsData = context.ReadUInt32(br);
            header.OffsetChunkOffsets = context.ReadUInt32(br);
            header.OffsetChunkLengths = context.ReadUInt32(br);
            header.OffsetChunkData = context.ReadUInt32(br);
            header.OffsetEntries = context.ReadUInt32(br);
            if (header.Version > 2)
            {
                header.Checksum = context.ReadUInt32(br);
            }
            if (header.Version > 3)
            {
                header.OffsetUnknown1 = context.ReadUInt32(br);
                header.OffsetUnknown2 = context.ReadUInt32(br);
                header.OffsetResourceOffsets = context.ReadUInt32(br);
            }
            return header;
        }

        /// <summary>
        /// Update Checksum
        /// </summary>
        /// <returns>Current Checksum</returns>
        public uint UpdateChecksum()
        {
            var checkBuffer = BitConverter.GetBytes(OffsetEncrypt)
                        .Concat(BitConverter.GetBytes(OffsetNames))
                        .Concat(BitConverter.GetBytes(OffsetStrings))
                        .Concat(BitConverter.GetBytes(OffsetStringsData))
                        .Concat(BitConverter.GetBytes(OffsetChunkOffsets))
                        .Concat(BitConverter.GetBytes(OffsetChunkLengths))
                        .Concat(BitConverter.GetBytes(OffsetChunkData))
                        .Concat(BitConverter.GetBytes(OffsetEntries))
                        .ToArray();
            Adler32 adler32 = new Adler32();
            adler32.Update(checkBuffer);
            if (Version < 4)
            {
                Checksum = (uint)adler32.Checksum;
                return Checksum;
            }
            checkBuffer = BitConverter.GetBytes(OffsetUnknown1)
                                           .Concat(BitConverter.GetBytes(OffsetUnknown2))
                                           .Concat(BitConverter.GetBytes(OffsetResourceOffsets))
                                           .ToArray();
            adler32.Update(checkBuffer);
            Checksum = (uint)adler32.Checksum;
            return Checksum;
        }
    }
}
