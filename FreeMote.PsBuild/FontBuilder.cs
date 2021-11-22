using System;
using System.Collections.Generic;
using System.Drawing;
using FreeMote.Psb;

namespace FreeMote.PsBuild
{
    /// <summary>
    /// Generate font PSB
    /// </summary>
    public static class FontBuilder
    {
        /// <summary>
        /// Generate font PSB
        /// </summary>
        /// <param name="fontName">the name of the font</param>
        /// <param name="characters">all character needed</param>
        /// <param name="fontsWithSize">A list of fonts, if one character doesn't exist in a font, fallback to next font.</param>
        /// <param name="foregroundColor">the color of characters, usually <see cref="Color.White"/></param>
        /// <param name="backgroundColor">the color of the background, usually <see cref="Color.Transparent"/></param>
        /// <param name="platform">target PSB platform</param>
        /// <param name="pixelFormat">the pixel format used for PSB (auto choose according to <see cref="PsbSpec"/> if use <see cref="PsbPixelFormat.None"/>)</param>
        /// <returns>the generated font PSB</returns>
        public static PSB BuildFontPsb(string fontName, HashSet<char> characters, IDictionary<Font, int> fontsWithSize, Color foregroundColor, Color backgroundColor, PsbSpec platform = PsbSpec.common,
            PsbPixelFormat pixelFormat = PsbPixelFormat.None)
        {
            PSB psb = new PSB();
            return psb;
        }

        //TODO: use GlyphInstance.Fallback

        /// <summary>
        /// Generate font PSB (simple)
        /// </summary>
        /// <param name="characters">A string or array contains all character needed</param>
        /// <param name="fontPaths">the font file paths</param>
        /// <param name="fontName">PSB font name</param>
        /// <param name="platform">target PSB platform</param>
        /// <param name="pixelFormat">the pixel format used for PSB</param>
        /// <returns></returns>
        public static PSB BuildFontPsb(ReadOnlySpan<char> characters, List<string> fontPaths, string fontName = null, PsbSpec platform = PsbSpec.common, PsbPixelFormat pixelFormat = PsbPixelFormat.None)
        {
            return null;
        }
    }
}
