using System;
using System.IO;
using System.Linq;

namespace FreeMote
{
    /// <summary>
    /// AstcHeader - 16 bytes
    /// </summary>
    public class AstcHeader
    {
        public static readonly byte[] Magic = {0x13, 0xAB, 0xA1, 0x5C};
        public const int Length = 16;

        public byte[] Data { get; }

        public AstcHeader()
        {
            Data = new byte[16];
        }

        public AstcHeader(byte[] data)
        {
            Data = data;
        }

        public Span<byte> Signature
        {
            get => Data.AsSpan(0, 4);
            set => value.CopyTo(Data);
        }

        public byte BlockX
        {
            get => Data[4];
            set => Data[4] = value;
        }

        public byte BlockY
        {
            get => Data[5];
            set => Data[5] = value;
        }

        public byte BlockZ
        {
            get => Data[6];
            set => Data[6] = value;
        }

        public Span<byte> DimX
        {
            get => Data.AsSpan().Slice(7, 3);
            set => value.CopyTo(Data.AsSpan(7, 3));
        }

        public Span<byte> DimY {
            get => Data.AsSpan().Slice(10, 3);
            set => value.CopyTo(Data.AsSpan(10, 3));
        }
        public Span<byte> DimZ {
            get => Data.AsSpan().Slice(13, 3);
            set => value.CopyTo(Data.AsSpan(13, 3));
        }

        public int Width
        {
            get => DimX[0] << 16 | DimX[1] << 8 | DimX[2];
            set => BitConverter.GetBytes(value).AsSpan(0, 3).CopyTo(Data.AsSpan(7, 3));
        }

        public int Height
        {
            get => DimY[0] << 16 | DimY[1] << 8 | DimY[2];
            set => BitConverter.GetBytes(value).AsSpan(0, 3).CopyTo(Data.AsSpan(10, 3));
        }

        public int Depth
        {
            get => DimZ[0] << 16 | DimZ[1] << 8 | DimZ[2];
            set => BitConverter.GetBytes(value).AsSpan(0, 3).CopyTo(Data.AsSpan(13, 3));
        }
    }

    public static class AstcFile
    {
        public static AstcHeader ParseAstcHeader(Stream stream)
        {
            var pos = stream.Position;
            if (stream.Length - stream.Position < 16)
            {
                return null;
            }

            byte[] bytes = new byte[16];
            stream.Read(bytes, 0, 16);
            stream.Position = pos;

            if (bytes[0] == AstcHeader.Magic[0] && bytes[1] == AstcHeader.Magic[1] && bytes[2] == AstcHeader.Magic[2] && bytes[3] == AstcHeader.Magic[3])
            {
                return new AstcHeader(bytes);
            }

            return null;
        }

        public static AstcHeader ParseAstcHeader(BinaryReader br)
        {
            var stream = br.BaseStream;
            var pos = stream.Position;
            if (stream.Length - stream.Position < 16)
            {
                return null;
            }

            var bytes = br.ReadBytes(16);
            stream.Position = pos;
            
            if (bytes[0] == AstcHeader.Magic[0] && bytes[1] == AstcHeader.Magic[1] && bytes[2] == AstcHeader.Magic[2] && bytes[3] == AstcHeader.Magic[3])
            {
                return new AstcHeader(bytes);
            }

            return null;
        }

        public static AstcHeader ParseAstcHeader(byte[] bytes)
        {
            if (IsAstcHeader(bytes))
            {
                return new AstcHeader(bytes.Take(16).ToArray());
            }

            return null;
        }

        public static bool IsAstcHeader(in byte[] bytes)
        {
            if (bytes.Length > 16 && bytes.Take(4).SequenceEqual(AstcHeader.Magic))
            {
                return true;
            }

            return false;
        }

        public static bool IsAstcHeader(BinaryReader br)
        {
            var stream = br.BaseStream;
            var pos = stream.Position;
            if (stream.Length - stream.Position < 16)
            {
                return false;
            }
            
            var bytes = br.ReadBytes(4);
            stream.Position = pos;

            return bytes.SequenceEqual(AstcHeader.Magic);
        }

        public static bool IsAstcHeader(Stream stream)
        {
            var pos = stream.Position;
            if (stream.Length - stream.Position < 16)
            {
                return false;
            }

            byte[] bytes = new byte[4];
            stream.Read(bytes, 0, 4);
            stream.Position = pos;

            return bytes.SequenceEqual(AstcHeader.Magic);
        }

        public static byte[] CutHeader(byte[] bts)
        {
            return IsAstcHeader(bts) ? bts.AsSpan(AstcHeader.Length).ToArray() : bts;
        }
    }
}
