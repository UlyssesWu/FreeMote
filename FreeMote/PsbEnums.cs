using System;

// ReSharper disable InconsistentNaming

namespace FreeMote
{
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
    }

    /// <summary>
    /// PSB Platform
    /// </summary>
    public enum PsbSpec : byte
    {
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
        /// Uncompress if needed
        /// </summary>
        Uncompress,
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
        /// Big Endian DXT5
        /// </summary>
        DXT5,
        /// <summary>
        /// Unsupport
        /// </summary>
        A8L8,
        /// <summary>
        /// Unsupport
        /// </summary>
        L8,
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
