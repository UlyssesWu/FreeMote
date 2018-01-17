using System;

// ReSharper disable InconsistentNaming

namespace FreeMote
{
    public enum PsbType
    {
        /// <summary>
        /// Motion
        /// </summary>
        Motion = 0,
        /// <summary>
        /// Images
        /// </summary>
        Pimg = 1,
        /// <summary>
        /// Script
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
}
