using System;
using System.Drawing;
using FreeMote.Psb;

namespace FreeMote.PsBuild.Fonts
{
    /// <summary>
    /// Options used to build an E-mote bitmap-font PSB.
    /// </summary>
    public sealed class FontBuildOptions
    {
        /// <summary>
        /// TrueType/OpenType font file path. Font collections are supported through <see cref="FontIndex"/>.
        /// </summary>
        public string FontPath { get; set; }

        /// <summary>
        /// Zero-based face index for TTC/OTC font collections.
        /// </summary>
        public int FontIndex { get; set; }

        /// <summary>
        /// Logical character-cell height in pixels.
        /// </summary>
        public int FontSize { get; set; }

        /// <summary>
        /// Synthetic style applied to the outlines from <see cref="FontPath"/>.
        /// Use Regular when the selected font file already contains the desired style.
        /// </summary>
        public FontStyle FontStyle { get; set; } = FontStyle.Regular;

        /// <summary>
        /// Maximum width and height of one atlas page. Must be a power of two.
        /// </summary>
        public int AtlasSize { get; set; } = 2048;

        /// <summary>
        /// Transparent pixels left between atlas entries.
        /// </summary>
        public int Padding { get; set; } = 2;

        /// <summary>
        /// Target PSB platform.
        /// </summary>
        public PsbSpec Platform { get; set; } = PsbSpec.win;

        /// <summary>
        /// Atlas pixel format name as stored in PSB. When omitted, a sample-compatible default is selected
        /// for <see cref="Platform"/>.
        /// </summary>
        public string PixelType { get; set; }

        /// <summary>
        /// Font label stored in the PSB root object.
        /// </summary>
        public string Label { get; set; } = "normal";

        /// <summary>
        /// Binary PSB format version.
        /// </summary>
        public ushort PsbVersion { get; set; } = 2;

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(FontPath))
            {
                throw new ArgumentException("Font path must be specified.", nameof(FontPath));
            }

            if (FontIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(FontIndex), "Font collection index cannot be negative.");
            }

            if (FontSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(FontSize), "Font size must be greater than zero.");
            }

            const FontStyle supportedStyles = FontStyle.Bold | FontStyle.Italic | FontStyle.Underline |
                                              FontStyle.Strikeout;
            if ((FontStyle & ~supportedStyles) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(FontStyle), "The requested font style is not supported.");
            }

            if (AtlasSize < 16 || (AtlasSize & (AtlasSize - 1)) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(AtlasSize), "Atlas size must be a power of two and at least 16.");
            }

            if (Padding < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Padding), "Atlas padding cannot be negative.");
            }

            if (Platform == PsbSpec.none || Platform == PsbSpec.other)
            {
                throw new ArgumentOutOfRangeException(nameof(Platform), "A concrete PSB platform must be selected.");
            }

            if (PsbVersion < 2 || PsbVersion > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(PsbVersion), "PSB version must be between 2 and 4.");
            }
        }
    }
}
