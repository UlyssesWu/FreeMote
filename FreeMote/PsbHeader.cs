using System;
using System.IO;
using System.Linq;

namespace FreeMote
{
    /// <summary>
    /// PSB Header
    /// </summary>
    //[StructLayout(LayoutKind.Explicit)]
    public class PsbHeader
    {
        /// <summary>
        /// Max length of header AFAIK. Need to be fixed if there are longer headers exist.
        /// </summary>
        public const int MAX_HEADER_LENGTH = 56;

        //[FieldOffset(0)] //This will cause error on x64 CLR since the next FieldOffset after char[] have to be 8n.
        public char[] Signature = {'P', 'S', 'B', '\0'};

        //[FieldOffset(4)]
        public ushort Version = 3;

        /// <summary>
        /// If 1, the header seems encrypted, which add more difficulty to us
        /// <para>But doesn't really matters since usually it's always encrypted in v3+</para>
        /// </summary>
        //[FieldOffset(6)]
        public ushort HeaderEncrypt = 0;

        /// <summary>
        /// Header Length
        /// <para>Usually same as <see cref="OffsetNames"/></para>
        /// </summary>
        //[FieldOffset(8)]
        public uint HeaderLength;

        /// <summary>
        /// Offset of Names
        /// <para>Usually the beginning of encryption in v2</para>
        /// </summary>
        //[FieldOffset(12)]
        public uint OffsetNames;

        //[FieldOffset(16)]
        public uint OffsetStrings;

        //[FieldOffset(20)]
        public uint OffsetStringsData;

        /// <summary>
        /// ResOffTable
        /// </summary>
        //[FieldOffset(24)]
        public uint OffsetChunkOffsets;

        //[FieldOffset(28)]
        public uint OffsetChunkLengths;

        /// <summary>
        /// Offset of Chunk Data (Image)
        /// </summary>
        //[FieldOffset(32)]
        public uint OffsetChunkData;

        /// <summary>
        /// Entry Offset
        /// </summary>
        //[FieldOffset(36)]
        public uint OffsetEntries;

        /// <summary>
        /// [v3] Adler32 Checksum for header
        /// <para>Not always checked in v3. Sadly, it's always checked from v4, so we have to handle it.</para>
        /// </summary>
        //[FieldOffset(40)]
        public uint Checksum;

        /// <summary>
        /// [v4] Extra chunk offsets
        /// </summary>
        //[FieldOffset(44)]
        public uint OffsetExtraChunkOffsets;

        /// <summary>
        /// [v4] Extra chunk lengths
        /// </summary>
        //[FieldOffset(48)]
        public uint OffsetExtraChunkLengths;

        /// <summary>
        /// [v4] 
        /// <para>If there are no data, same as <see cref="OffsetChunkOffsets"/></para>
        /// </summary>
        //[FieldOffset(52)]
        public uint OffsetExtraChunkData;


        public static PsbHeader Load(BinaryReader br)
        {
            PsbHeader header = new PsbHeader
            {
                Signature = br.ReadChars(4),
                Version = br.ReadUInt16(),
                HeaderEncrypt = br.ReadUInt16(),
                HeaderLength = br.ReadUInt32(),
                OffsetNames = br.ReadUInt32()
            };
            var sig = new string(header.Signature).ToUpperInvariant();
            if (sig.StartsWith("MDF") || sig.StartsWith("MFL"))
            {
                throw new PsbBadFormatException(PsbBadFormatReason.IsMdf, "Maybe a MDF file");
            }
            if (!sig.StartsWith("PSB"))
            {
                throw new PsbBadFormatException(PsbBadFormatReason.Header, "Not a valid PSB file");
            }
            //if (header.HeaderEncrypt != 0) //following header is possibly encrypted
            //{
            //    return header;
            //}
            if (header.HeaderLength < br.BaseStream.Length
                && header.OffsetNames < br.BaseStream.Length
                && (header.HeaderLength == header.OffsetNames || header.HeaderLength == 0))
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
                    header.OffsetExtraChunkOffsets = br.ReadUInt32();
                    header.OffsetExtraChunkLengths = br.ReadUInt32();
                    header.OffsetExtraChunkData = br.ReadUInt32();
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
            var sig = new string(header.Signature).ToUpperInvariant();
            if (sig.StartsWith("MDF"))
            {
                throw new PsbBadFormatException(PsbBadFormatReason.IsMdf, "Maybe a MDF file");
            }
            if (!sig.StartsWith("PSB"))
            {
                throw new PsbBadFormatException(PsbBadFormatReason.Header, "Not a valid PSB file");
            }

            PsbStreamContext context = new PsbStreamContext(key);

            header.HeaderLength = context.ReadUInt32(br);
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
                header.OffsetExtraChunkOffsets = context.ReadUInt32(br);
                header.OffsetExtraChunkLengths = context.ReadUInt32(br);
                header.OffsetExtraChunkData = context.ReadUInt32(br);
            }
            return header;
        }

        /// <summary>
        /// Update Checksum
        /// </summary>
        /// <returns>Current Checksum</returns>
        public uint UpdateChecksum()
        {
            var checkBuffer = BitConverter.GetBytes(HeaderLength)
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
            checkBuffer = BitConverter.GetBytes(OffsetExtraChunkOffsets)
                                           .Concat(BitConverter.GetBytes(OffsetExtraChunkLengths))
                                           .Concat(BitConverter.GetBytes(OffsetExtraChunkData))
                                           .ToArray();
            adler32.Update(checkBuffer);
            Checksum = (uint)adler32.Checksum;
            return Checksum;
        }

        public static uint GetHeaderLength(ushort version)
        {
            if (version < 3)
            {
                return 40u;
            }
            if (version > 3)
            {
                return 56u;
            }
            return 44u;
        }

        /// <summary>
        /// Get Header Length based on Version
        /// </summary>
        /// <returns></returns>
        public uint GetHeaderLength()
        {
            return GetHeaderLength(Version);
        }

        /// <summary>
        /// Change version for Header
        /// </summary>
        /// <param name="version"></param>
        /// <param name="offsetFields">if true, offset all fields</param>
        public void SwitchVersion(ushort version = 3, bool offsetFields = false)
        {
            if (version != 2 && version != 3 && version != 4)
            {
                throw new NotSupportedException("Unsupported version");
            }

            Version = version;
            long headerLen = GetHeaderLength(version);
            if (!offsetFields)
            {
                HeaderLength = (uint) headerLen;
                OffsetNames = HeaderLength;
                UpdateChecksum();
                return;
            }

            long offset = headerLen - HeaderLength;

            if (offset != 0)
            {
                HeaderLength = (uint)headerLen;
                OffsetNames = (uint)(OffsetNames + offset);
                OffsetStrings = (uint)(OffsetStrings + offset);
                OffsetStringsData = (uint)(OffsetStringsData + offset);
                OffsetChunkOffsets = (uint)(OffsetChunkOffsets + offset);
                OffsetChunkLengths = (uint)(OffsetChunkLengths + offset);
                OffsetChunkData = (uint)(OffsetChunkData + offset);
                OffsetEntries = (uint)(OffsetEntries + offset);
                OffsetExtraChunkOffsets = OffsetChunkOffsets - 6;
                OffsetExtraChunkLengths = OffsetChunkOffsets - 3;
                OffsetExtraChunkData = OffsetChunkOffsets;
            }
            UpdateChecksum();
        }

        public byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);
                bw.Write(Signature);
                bw.Write(Version);
                bw.Write(HeaderEncrypt);
                bw.Write(GetHeaderLength());
                bw.Write(OffsetNames);
                bw.Write(OffsetStrings);
                bw.Write(OffsetStringsData);
                bw.Write(OffsetChunkOffsets);
                bw.Write(OffsetChunkLengths);
                bw.Write(OffsetChunkData);
                bw.Write(OffsetEntries);
                if (Version > 2)
                {
                    bw.Write(UpdateChecksum());
                }
                if (Version > 3)
                {
                    bw.Write(OffsetExtraChunkOffsets);
                    bw.Write(OffsetExtraChunkLengths);
                    bw.Write(OffsetExtraChunkData);
                }
                bw.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Similar as <see cref="PsbFile.TestHeaderEncrypted"/> but not based on file.
        /// </summary>
        public bool IsHeaderEncrypted => HeaderLength > (MAX_HEADER_LENGTH + 16) || OffsetNames == 0 || (HeaderLength != OffsetNames && HeaderLength != 0);
    }
}
