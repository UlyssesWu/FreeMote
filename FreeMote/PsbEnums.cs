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
        /// Images (pimg, dapk)
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
        /// RGBA4444
        /// </summary>
        RGBA4444,
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
