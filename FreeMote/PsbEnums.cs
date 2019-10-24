using System;

// ReSharper disable InconsistentNaming

namespace FreeMote
{
    internal enum PsbBadFormatReason
    {
        Header,
        IsMdf,
        Objects,
        Resources,
        Array,
        Body,
    }

    internal class PsbBadFormatException : FormatException
    {
        public PsbBadFormatReason Reason { get; }

        public PsbBadFormatException(PsbBadFormatReason reason, string message = null, Exception innerException = null) : base(message, innerException)
        {
            Reason = reason;
        }
    }

    /// <summary>
    /// PSB Type
    /// <remarks>It should not use generic name such as `Images` since different image types may still have different structures.</remarks>
    /// </summary>
    public enum PsbType
    {
        /// <summary>
        /// Motion (psb)
        /// </summary>
        Motion = 0,
        /// <summary>
        /// Images (pimg, dpak)
        /// </summary>
        Pimg = 1,
        /// <summary>
        /// Script (scn)
        /// </summary>
        /// TODO: KS decompiler?
        Scn = 2,
        /// <summary>
        /// EMT project - M2 MOtion (mmo, emtproj)
        /// </summary>
        Mmo = 3,
        /// <summary>
        /// Images for Character (temp name)
        /// </summary>
        Tachie = 4,
        /// <summary>
        /// MDF Archive Index (_info.psb.m)
        /// </summary>
        ArchiveInfo = 5,
        /// <summary>
        /// BMP Font (e.g. textfont24)
        /// </summary>
        BmpFont = 6,
    }

    /// <summary>
    /// EMT PSB Platform
    /// </summary>
    public enum PsbSpec : byte
    {
        /// <summary>
        /// Do not have spec
        /// </summary>
        none = Byte.MinValue, 
        /// <summary>
        /// Unity & other
        /// </summary>
        common,
        /// <summary>
        /// Kirikiri
        /// </summary>
        krkr,
        /// <summary>
        /// DirectX
        /// </summary>
        win,
        /// <summary>
        /// WebGL
        /// </summary>
        ems,
        /// <summary>
        /// PS Vita
        /// </summary>
        vita,
        /// <summary>
        /// PS4
        /// </summary>
        ps4,
        other = Byte.MaxValue,
    }

    public enum PsbImageFormat
    {
        Bmp,
        Png,
    }

    /// <summary>
    /// How to handle images when decompiling
    /// </summary>
    public enum PsbImageOption
    {
        /// <summary>
        /// Keep original
        /// </summary>
        Original,
        /// <summary>
        /// Decompress if needed
        /// </summary>
        Decompress,
        /// <summary>
        /// Compress if needed
        /// </summary>
        Compress,
        /// <summary>
        /// Try to convert to common image format
        /// </summary>
        Extract,

    }

    public enum PsbPixelFormat
    {
        None,
        /// <summary>
        /// Little Endian RGBA8
        /// </summary>
        WinRGBA8,
        /// <summary>
        /// Big Endian RGBA8
        /// </summary>
        CommonRGBA8,
        /// <summary>
        /// Little Endian RGBA4444
        /// </summary>
        WinRGBA4444,
        /// <summary>
        /// Big Endian RGBA4444
        /// </summary>
        CommonRGBA4444,
        /// <summary>
        /// Big Endian DXT5
        /// </summary>
        DXT5,
        /// <summary>
        /// A8L8
        /// </summary>
        A8L8,
        /// <summary>
        /// L8
        /// </summary>
        L8,
        /// <summary>
        /// A8
        /// </summary>
        A8,
        /// <summary>
        /// RGBA8_SW (Swizzle) for vita
        /// </summary>
        RGBA8_SW,
        /// <summary>
        /// RGBA8_SW (Tile) for PS4
        /// </summary>
        TileRGBA8_SW,
        /// <summary>
        /// A8_SW (Swizzle)
        /// </summary>
        A8_SW,
        /// <summary>
        /// A8_SW (Tile) for PS4
        /// </summary>
        TileA8_SW,
    }

    public enum EncodeMode
    {
        Encrypt,
        Decrypt,
    }

    public enum EncodePosition
    {
        Body,
        Header,
        Full,
        /// <summary>
        /// Automata
        /// <para>if encrypt v3-V4, will only encrypt header.</para>
        /// <para>if encrypt v2, will only encrypt body(strings).</para>
        /// <para>if decrypt, clean header and body both.</para>
        /// </summary>
        Auto
    }
}

