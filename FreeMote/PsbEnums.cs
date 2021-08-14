using System;

// ReSharper disable InconsistentNaming

namespace FreeMote
{
    internal class PsbBadFormatException : FormatException
    {
        public PsbBadFormatReason Reason { get; }

        public PsbBadFormatException(PsbBadFormatReason reason, string message = null, Exception innerException = null) : base(message, innerException)
        {
            Reason = reason;
        }
    }

    /// <summary>
    /// PSB.Combine(PSB)
    /// </summary>
    public enum PsbCombineMethod
    {
        /// <summary>
        /// Default
        /// </summary>
        Default,
        /// <summary>
        /// Merge everything
        /// </summary>
        All,
        /// <summary>
        /// Only merge objects
        /// </summary>
        Objects
    }

    internal enum PsbBadFormatReason
    {
        Header,
        IsMdf,
        Objects,
        Resources,
        Array,
        Body,
    }

    /// <summary>
    /// PSB Type
    /// </summary>
    public enum PsbType
    {
        /// <summary>
        /// Unknown type PSB
        /// </summary>
        PSB = 0,
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
        /// Images with Layouts (used in PS4, PSV)
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
        /// <summary>
        /// EMT
        /// </summary>
        Motion = 7,
        /// <summary>
        /// Sound Archive
        /// </summary>
        SoundArchive = 8,
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
        /// <summary>
        /// NSwitch
        /// </summary>
        nx,
        other = Byte.MaxValue,
    }

    public enum PsbImageFormat
    {
        png,
        bmp,
    }

    /// <summary>
    /// How to handle resource when decompiling
    /// </summary>
    public enum PsbExtractOption
    {
        /// <summary>
        /// Keep original
        /// </summary>
        Original,
        /// <summary>
        /// Try to convert to common format
        /// </summary>
        Extract,
        ///// <summary>
        ///// Decompress if needed
        ///// </summary>
        //[Obsolete]
        //Decompress,
        ///// <summary>
        ///// Compress if needed
        ///// </summary>
        //[Obsolete]
        //Compress,
    }
    
    public enum PsbPixelFormat
    {
        None = 0,
        /// <summary>
        /// Little Endian RGBA8 (plat: win)
        /// </summary>
        LeRGBA8,
        /// <summary>
        /// Big Endian RGBA8 (plat: common)
        /// </summary>
        BeRGBA8,
        /// <summary>
        /// Little Endian RGBA4444 (plat: win)
        /// </summary>
        LeRGBA4444,
        /// <summary>
        /// Big Endian RGBA4444 (plat: common)
        /// </summary>
        BeRGBA4444,
        /// <summary>
        /// RGBA5650
        /// </summary>
        RGBA5650,
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

        //SW and Tile

        /// <summary>
        /// BeRGBA8_SW (Swizzle) for vita
        /// </summary>
        RGBA8_SW,
        /// <summary>
        /// LeRGBA8_SW (Tile) for PS4
        /// </summary>
        TileRGBA8_SW,
        /// <summary>
        /// Little Endian RGBA4444 with Swizzle for vita
        /// </summary>
        LeRGBA4444_SW,
        /// <summary>
        /// RGBA5650 with Swizzle for vita
        /// </summary>
        RGBA5650_SW,
        /// <summary>
        /// RGBA5650 with Tile for PS4
        /// </summary>
        TileRGBA5650_SW,
        /// <summary>
        /// A8L8 with Swizzle for vita
        /// </summary>
        A8L8_SW,
        /// <summary>
        /// A8L8_SW (Tile) for PS4
        /// </summary>
        TileA8L8_SW,
        /// <summary>
        /// L8 with Swizzle for vita
        /// </summary>
        L8_SW,
        /// <summary>
        /// L8_SW (Tile) for PS4
        /// </summary>
        TileL8_SW,
        /// <summary>
        /// A8_SW (Swizzle)
        /// </summary>
        A8_SW,
        /// <summary>
        /// A8_SW (Tile) for PS4
        /// </summary>
        TileA8_SW,
        /// <summary>
        /// CI8 (C8) with Swizzle for vita
        /// </summary>
        /// REF: http://wiki.tockdom.com/wiki/Image_Formats#C8_.28CI8.29
        CI8_SW,
        /// <summary>
        /// CI4 with Swizzle for vita
        /// </summary>
        CI4_SW,

        //Special

        /// <summary>
        /// Big Endian DXT5
        /// </summary>
        DXT5,
        /// <summary>
        /// ASTC with block 4x4 for nx
        /// </summary>
        ASTC_8BPP,
    }

    public enum PsbAudioFormat
    {
        Unknown = 0,
        WAV,
        Atrac9,
        OPUS,
        XWMA,
        VAG,
        ADPCM,
    }

    public enum PsbAudioPan
    {
        /// <summary>
        /// 1 Channel
        /// </summary>
        Mono,
        /// <summary>
        /// Left Channel
        /// </summary>
        Left,
        /// <summary>
        /// Right Channel
        /// </summary>
        Right,
        /// <summary>
        /// 2 channels, first is Left, second is Right
        /// </summary>
        LeftRight,
        /// <summary>
        /// Stereo (in 1 Channel)
        /// </summary>
        Stereo,
        /// <summary>
        /// Multiple Channels (2+)
        /// </summary>
        Multiple,
        /// <summary>
        /// Intro (for Loop)
        /// </summary>
        Intro,
        /// <summary>
        /// Body (for Loop)
        /// </summary>
        Body,
        /// <summary>
        /// Intro + Body (NX OPUS)
        /// </summary>
        IntroBody,
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

