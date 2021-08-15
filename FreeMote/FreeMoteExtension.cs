using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace FreeMote
{
    public static class FreeMoteExtension
    {
        /// <summary>
        /// Get RGBA bytes from palette
        /// </summary>
        /// <param name="palette"></param>
        /// <param name="palettePixelFormat"></param>
        /// <returns></returns>
        public static byte[] GetPaletteBytes(this ColorPalette palette, PsbPixelFormat palettePixelFormat)
        {
            byte[] bytes = new byte[palette.Entries.Length * 4];
            for (var i = 0; i < palette.Entries.Length; i++)
            {
                //LE ARGB
                var color = palette.Entries[i];
                bytes[i * 4] = color.B;
                bytes[i * 4 + 1] = color.G;
                bytes[i * 4 + 2] = color.R;
                bytes[i * 4 + 3] = color.A;
                //var bt = BitConverter.GetBytes(color.ToArgb());
            }

            switch (palettePixelFormat)
            {
                case PsbPixelFormat.BeRGBA8:
                    RL.Switch_0_2(ref bytes);
                    break;
            }

            return bytes;
        }

        /// <summary>
        /// Get shell type from suffix
        /// </summary>
        /// <param name="suffixString">e.g. xxx.xxx.m will return "MDF"</param>
        /// <returns></returns>
        public static string DefaultShellType(this string suffixString)
        {
            var suffix = suffixString.ToLowerInvariant();
            if (suffix.EndsWith(".m"))
            {
                return "MDF";
            }
            else
            {
                return "";
            }
        }

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
        /// Whether the <see cref="PsbPixelFormat"/> use palette
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        public static bool UsePalette(this PsbPixelFormat format)
        {
            switch (format)
            {
                case PsbPixelFormat.CI8_SW:
                case PsbPixelFormat.CI4_SW:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether the <see cref="PsbSpec"/> should use <see cref="PostProcessing.TileTexture"/>
        /// </summary>
        /// <param name="spec"></param>
        /// <returns></returns>
        public static bool UseTile(this PsbSpec spec)
        {
            switch (spec)
            {
                case PsbSpec.ps4:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether the <see cref="PsbSpec"/> should use BigEndian
        /// </summary>
        /// <param name="spec"></param>
        /// <returns></returns>
        public static bool UseBigEndian(this PsbSpec spec)
        {
            switch (spec)
            {
                case PsbSpec.common:
                case PsbSpec.ems:
                //case PsbSpec.vita: //TODO: is vita BigEndian?
                    return true;
                default:
                    return false;
            }
        }

        public static int? GetBitDepth(this PsbPixelFormat format)
        {
            switch (format) //sadly, missing case won't be hinted when using switch expression 
            {
                case PsbPixelFormat.None:
                    return null;
                case PsbPixelFormat.LeRGBA8:
                case PsbPixelFormat.BeRGBA8:
                case PsbPixelFormat.RGBA8_SW:
                case PsbPixelFormat.TileRGBA8_SW:
                    return 32;
                case PsbPixelFormat.LeRGBA4444:
                case PsbPixelFormat.LeRGBA4444_SW:
                case PsbPixelFormat.BeRGBA4444:
                case PsbPixelFormat.RGBA5650:
                case PsbPixelFormat.RGBA5650_SW:
                case PsbPixelFormat.TileRGBA5650_SW:
                case PsbPixelFormat.A8L8:
                case PsbPixelFormat.A8L8_SW:
                case PsbPixelFormat.TileA8L8_SW:
                    return 16;
                case PsbPixelFormat.L8:
                case PsbPixelFormat.L8_SW:
                case PsbPixelFormat.TileL8_SW:
                case PsbPixelFormat.A8:
                case PsbPixelFormat.A8_SW:
                case PsbPixelFormat.TileA8_SW:
                case PsbPixelFormat.CI8_SW:
                    return 8;
                case PsbPixelFormat.CI4_SW:
                    return 4;
                case PsbPixelFormat.ASTC_8BPP:
                case PsbPixelFormat.DXT5:
                default:
                    return null;
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
                case PsbSpec.vita:
                    return PsbPixelFormat.BeRGBA8;
                case PsbSpec.krkr:
                case PsbSpec.win:
                    return PsbPixelFormat.LeRGBA8;
                case PsbSpec.other:
                default:
                    return PsbPixelFormat.None;
            }
        }
        
        public static ImageFormat ToImageFormat(this PsbImageFormat imageFormat)
        {
            switch (imageFormat)
            {
                case PsbImageFormat.bmp:
                    return ImageFormat.Bmp;
                case PsbImageFormat.png:
                default:
                    return ImageFormat.Png;
            }
        }

        public static string DefaultExtension(this PsbImageFormat imageFormat)
        {
            switch (imageFormat)
            {
                case PsbImageFormat.png:
                    return ".png";
                case PsbImageFormat.bmp:
                    return ".bmp";
                default:
                    return $".{imageFormat}";
            }
        }

        public static string DefaultExtension(this PsbAudioFormat audioFormat)
        {
            switch (audioFormat)
            {
                case PsbAudioFormat.Atrac9:
                    return ".at9";
                case PsbAudioFormat.WAV:
                    return ".wav";
                case PsbAudioFormat.OPUS:
                    return ".opus";
                case PsbAudioFormat.XWMA:
                    return ".xwma";
                case PsbAudioFormat.VAG:
                    return ".vag";
                case PsbAudioFormat.ADPCM:
                    return ".adpcm";
                case PsbAudioFormat.Unknown:
                    return ".raw";
                default:
                    return $".{audioFormat.ToString().ToLowerInvariant()}";
            }
        }

        // Yet another PSB bad design: some types do not have a type id while others do.

        public static string DefaultTypeId(this PsbType type)
        {
            switch (type)
            {
                case PsbType.Tachie:
                    return "image";
                case PsbType.ArchiveInfo:
                    return "archive";
                case PsbType.BmpFont:
                    return "font";
                case PsbType.Motion:
                    return "motion";
                case PsbType.SoundArchive:
                    return "sound_archive";
                case PsbType.PSB:
                case PsbType.Mmo:
                case PsbType.Scn:
                case PsbType.Pimg:
                default:
                    return "";
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

            bool useTile = spec.UseTile();

            switch (typeStr.ToUpperInvariant())
            {
                case "CI4_SW":
                    return PsbPixelFormat.CI4_SW;
                case "CI8_SW":
                    return PsbPixelFormat.CI8_SW;
                case "DXT5":
                    return PsbPixelFormat.DXT5;
                case "RGBA4444":
                    if (spec.UseBigEndian())
                        return PsbPixelFormat.BeRGBA4444;
                    return PsbPixelFormat.LeRGBA4444;
                case "RGBA4444_SW":
                    return PsbPixelFormat.LeRGBA4444_SW;
                case "RGBA8":
                    if (spec.UseBigEndian() || spec == PsbSpec.vita)
                        return PsbPixelFormat.BeRGBA8;
                    else
                        return PsbPixelFormat.LeRGBA8;
                case "RGBA8_SW":
                    return useTile ? PsbPixelFormat.TileRGBA8_SW : PsbPixelFormat.RGBA8_SW;
                case "A8_SW":
                    return useTile ? PsbPixelFormat.TileA8_SW : PsbPixelFormat.A8_SW;
                case "L8_SW":
                    return useTile ? PsbPixelFormat.TileL8_SW : PsbPixelFormat.L8_SW;
                case "A8L8":
                    return PsbPixelFormat.A8L8;
                case "A8L8_SW":
                    return useTile ? PsbPixelFormat.TileA8L8_SW : PsbPixelFormat.A8L8_SW;
                case "RGBA5650":
                    return PsbPixelFormat.RGBA5650;
                case "RGBA5650_SW":
                    return useTile ? PsbPixelFormat.TileRGBA5650_SW : PsbPixelFormat.RGBA5650_SW;
                case "ASTC_8BPP":
                    return PsbPixelFormat.ASTC_8BPP;
                default:
                    return Enum.TryParse(typeStr, true, out PsbPixelFormat pixelFormat) ? pixelFormat : PsbPixelFormat.None;
            }
        }

        public static string ToStringForPsb(this PsbPixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case PsbPixelFormat.None:
                case PsbPixelFormat.LeRGBA8:
                case PsbPixelFormat.BeRGBA8:
                    return "RGBA8";
                case PsbPixelFormat.LeRGBA4444:
                case PsbPixelFormat.BeRGBA4444:
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

        public static uint ReadUInt32BE(this BinaryReader br)
        {
            return BinaryPrimitives.ReadUInt32BigEndian(br.ReadBytes(4));
        }

        public static int ReadInt32BE(this BinaryReader br)
        {
            return BinaryPrimitives.ReadInt32BigEndian(br.ReadBytes(4));
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

        public static void WriteUTF8(this BinaryWriter bw, string value)
        {
            bw.Write(Encoding.UTF8.GetBytes(value));
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

        //https://stackoverflow.com/a/31107925
        internal static unsafe long IndexOf(this byte[] haystack, byte[] needle, long startOffset = 0)
        {
            fixed (byte* h = haystack) fixed (byte* n = needle)
            {
                for (byte* hNext = h + startOffset, hEnd = h + haystack.LongLength + 1 - needle.LongLength, nEnd = n + needle.LongLength; hNext < hEnd; hNext++)
                    for (byte* hInc = hNext, nInc = n; *nInc == *hInc; hInc++)
                        if (++nInc == nEnd)
                            return hNext - h;
                return -1;
            }
        }

        // Takes same patterns, and executes in parallel
        public static IEnumerable<string> GetFiles(string path,
            string[] searchPatterns,
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return searchPatterns.AsParallel()
                .SelectMany(searchPattern =>
                    Directory.EnumerateFiles(path, searchPattern, searchOption));
        }

        /// <summary>
        /// Print byte array in Hex
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        internal static string PrintInHex(this byte[] bytes)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var b in bytes)
            {
                var s = Convert.ToString(b, 16);
                if (s.Length == 1)
                {
                    s = "0" + s;
                }

                sb.Append(s);
            }

            return sb.ToString();
        }
    }
}