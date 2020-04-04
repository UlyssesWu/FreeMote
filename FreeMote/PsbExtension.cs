using System;
using System.IO;
using System.Linq;

namespace FreeMote
{
    public static class PsbExtension
    {
        /// <summary>
        /// Get <see cref="PsbType"/>'s default extension
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string DefaultExtension(this PsbType type)
        {
            switch (type)
            {
                case PsbType.Mmo:
                    return ".mmo";
                case PsbType.Pimg:
                    return ".pimg";
                case PsbType.Scn:
                    return ".scn";
                case PsbType.ArchiveInfo:
                    return ".psb.m";
                case PsbType.Motion:
                default:
                    return ".psb";
            }
        }

        /// <summary>
        /// Get <see cref="PsbSpec"/>'s default <see cref="PsbPixelFormat"/>
        /// </summary>
        /// <param name="spec"></param>
        /// <returns></returns>
        public static PsbPixelFormat DefaultPixelFormat(this PsbSpec spec)
        {
            switch (spec)
            {
                case PsbSpec.common:
                case PsbSpec.ems:
                    return PsbPixelFormat.CommonRGBA8;
                case PsbSpec.krkr:
                case PsbSpec.win:
                    return PsbPixelFormat.WinRGBA8;
                case PsbSpec.other:
                default:
                    return PsbPixelFormat.None;
            }
        }

        /// <summary>
        /// Get <see cref="PsbPixelFormat"/> from string and <see cref="PsbSpec"/>
        /// </summary>
        /// <param name="typeStr"></param>
        /// <param name="spec"></param>
        /// <returns></returns>
        public static PsbPixelFormat ToPsbPixelFormat(this string typeStr, PsbSpec spec)
        {
            if (string.IsNullOrEmpty(typeStr))
            {
                return PsbPixelFormat.None;
            }

            switch (typeStr.ToUpperInvariant())
            {
                case "CI8_SW":
                    return PsbPixelFormat.CI8_SW;
                case "DXT5":
                    return PsbPixelFormat.DXT5;
                case "RGBA8":
                    if (spec == PsbSpec.common || spec == PsbSpec.ems)
                        return PsbPixelFormat.CommonRGBA8;
                    else
                        return PsbPixelFormat.WinRGBA8;
                case "RGBA8_SW":
                    if (spec == PsbSpec.ps4)
                        return PsbPixelFormat.TileRGBA8_SW;
                    else
                        return PsbPixelFormat.RGBA8_SW;
                case "A8_SW":
                    if (spec == PsbSpec.ps4)
                        return PsbPixelFormat.TileA8_SW;
                    else
                        return PsbPixelFormat.A8_SW;
                case "RGBA4444":
                    if (spec == PsbSpec.common || spec == PsbSpec.ems)
                        return PsbPixelFormat.CommonRGBA4444;
                    return PsbPixelFormat.WinRGBA4444;
                case "A8L8":
                    return PsbPixelFormat.A8L8;
                default:
                    return Enum.TryParse(typeStr, true, out PsbPixelFormat pixelFormat) ? pixelFormat : PsbPixelFormat.None;
            }
        }

        public static string ToStringForPsb(this PsbPixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case PsbPixelFormat.None:
                case PsbPixelFormat.WinRGBA8:
                case PsbPixelFormat.CommonRGBA8:
                    return "RGBA8";
                case PsbPixelFormat.DXT5:
                    return "DXT5";
                case PsbPixelFormat.WinRGBA4444:
                case PsbPixelFormat.CommonRGBA4444:
                    return "RGBA4444";
                default:
                    return pixelFormat.ToString();
                    //throw new ArgumentOutOfRangeException(nameof(pixelFormat), pixelFormat, null);
            }
        }

        /// <summary>
        /// Read a <see cref="uint"/> from <see cref="BinaryReader"/>, and then encode using <see cref="PsbStreamContext"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="br"></param>
        /// <returns></returns>
        public static uint ReadUInt32(this PsbStreamContext context, BinaryReader br)
        {
            return BitConverter.ToUInt32(context.Encode(br.ReadBytes(4)), 0);
        }

        /// <summary>
        /// Read bytes from <see cref="BinaryReader"/>, and then encode using <see cref="PsbStreamContext"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="br"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static byte[] ReadBytes(this PsbStreamContext context, BinaryReader br, int count)
        {
            return context.Encode(br.ReadBytes(count));
        }

        /// <summary>
        /// Read a <see cref="ushort"/> from <see cref="BinaryReader"/>, and then encode using <see cref="PsbStreamContext"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="br"></param>
        /// <returns></returns>
        public static ushort ReadUInt16(this PsbStreamContext context, BinaryReader br)
        {
            return BitConverter.ToUInt16(context.Encode(br.ReadBytes(2)), 0);
        }

        /// <summary>
        /// Encode a value and write using <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="value"></param>
        /// <param name="bw"></param>
        public static void Write(this PsbStreamContext context, uint value, BinaryWriter bw)
        {
            bw.Write(context.Encode(BitConverter.GetBytes(value)));
        }

        /// <summary>
        /// Encode a value and write using <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="value"></param>
        /// <param name="bw"></param>
        public static void Write(this PsbStreamContext context, ushort value, BinaryWriter bw)
        {
            bw.Write(context.Encode(BitConverter.GetBytes(value)));
        }

        public static string ReadStringZeroTrim(this BinaryReader br)
        {
            var pos = br.BaseStream.Position;
            var length = 0;
            while (br.ReadByte() > 0)
            {
                length++;
            }
            br.BaseStream.Position = pos;
            var str = Consts.PsbEncoding.GetString(br.ReadBytes(length));
            br.ReadByte(); //skip \0 - fail if end without \0
            return str;
        }

        public static void WriteStringZeroTrim(this BinaryWriter bw, string str)
        {
            //bw.Write(str.ToCharArray());
            bw.Write(Consts.PsbEncoding.GetBytes(str));
            bw.Write((byte)0);
        }

        /// <summary>
        /// Big-Endian Write
        /// </summary>
        /// <param name="bw"></param>
        /// <param name="num"></param>
        public static void WriteBE(this BinaryWriter bw, uint num)
        {
            bw.Write(BitConverter.GetBytes(num).Reverse().ToArray());
        }

        public static void Pad(this BinaryWriter bw, int length, byte paddingByte = 0x0)
        {
            if (length <= 0)
            {
                return;
            }

            if (paddingByte == 0x0)
            {
                bw.Write(new byte[length]);
                return;
            }

            for (int i = 0; i < length; i++)
            {
                bw.Write(paddingByte);
            }
        }

        /// <summary>
        /// 查找一个byte数组在另一个byte数组第一次出现位置
        /// </summary>
        /// <param name="array">被查找的数组（大）</param>
        /// <param name="array2">要查找的数组（小）</param>
        /// <returns>找到返回索引，找不到返回-1</returns>
        internal static int FindIndex(byte[] array, byte[] array2)
        {
            int i, j;

            for (i = 0; i < array.Length; i++)
            {
                if (i + array2.Length <= array.Length)
                {
                    for (j = 0; j < array2.Length; j++)
                    {
                        if (array[i + j] != array2[j]) break;
                    }

                    if (j == array2.Length) return i;
                }
                else
                    break;
            }

            return -1;
        }

        /// <summary>
        /// Get package name from {package name}_info.psb.m
        /// </summary>
        /// <param name="fileName">e.g. {package name}_info.psb.m</param>
        /// <returns>can be null if failed</returns>
        public static string ArchiveInfoGetPackageName(string fileName)
        {
            var nameSlicePos = fileName.IndexOf("_info.", StringComparison.Ordinal);
            string name = null;
            if (nameSlicePos > 0)
            {
                name = fileName.Substring(0, nameSlicePos);
            }
            else
            {
                nameSlicePos = fileName.IndexOf(".", StringComparison.Ordinal);
                if (nameSlicePos > 0)
                {
                    name = fileName.Substring(0, nameSlicePos);
                }
            }

            return name;
        }
    }
}
