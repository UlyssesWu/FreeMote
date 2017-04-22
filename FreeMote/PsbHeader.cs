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
        /// If 1, the header is also encrypted, 
        /// which add more difficulty to us
        /// </summary>
        public ushort HeaderEncrypt { get; set; } = 0;

        /// <summary>
        /// Offset of encryption bytes
        /// <para>Usually same as <see cref="OffsetNames"/> if encrypted</para>
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
        /// Entry Counts (uint32)
        /// </summary>
        public uint EntryCounts { get; set; }

        /// <summary>
        /// Offset of ???
        /// <para>New in PSB Version 3</para>
        /// </summary>
        public uint OffsetEmote { get; set; }

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
                header.EntryCounts = br.ReadUInt32();
                if (header.Version > 2 || header.OffsetNames >= 44)
                {
                    header.OffsetEmote = br.ReadUInt32();
                }
            }
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
            header.EntryCounts = context.ReadUInt32(br);
            if (header.Version > 2 || header.OffsetNames >= 44)
            {
                header.OffsetEmote = context.ReadUInt32(br);
            }
            return header;
        }
    }
}
