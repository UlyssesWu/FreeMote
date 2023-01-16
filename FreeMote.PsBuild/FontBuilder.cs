using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using FreeMote.Psb;
using SixLabors.Fonts;

namespace FreeMote.PsBuild
{
    /// <summary>
    /// Generate font PSB
    /// </summary>
    public class FontBuilder
    {
        private readonly FontCollection _fontCollection = new FontCollection();

        public string AddFont(string path)
        {
            var family = _fontCollection.Add(path);
            return family.Name;
        }

        public string AddFont(Stream stream)
        {
            var family = _fontCollection.Add(stream);
            return family.Name;
        }

        /// <summary>
        /// Add .ttc font collection
        /// </summary>
        /// <param name="path"></param>
        public void AddFontCollection(string path)
        {
            _fontCollection.AddCollection(path);
        }

        /// <summary>
        /// Add .ttc font collection
        /// </summary>
        /// <param name="stream"></param>
        public void AddFontCollection(Stream stream)
        {
            _fontCollection.AddCollection(stream);
        }

        public bool FontExists(string fontName)
        {
            return _fontCollection.TryGet(fontName, out _);
        }

        /// <summary>
        /// Generate font PSB
        /// </summary>
        /// <param name="fontName">the name of the font (in file)</param>
        /// <param name="characters">all character needed</param>
        /// <param name="fontsWithSize">A list of fonts, if one character doesn't exist in a font, fallback to next font.</param>
        /// <param name="foregroundColor">the color of characters, usually <see cref="Color.White"/></param>
        /// <param name="backgroundColor">the color of the background, usually <see cref="Color.Transparent"/></param>
        /// <param name="platform">target PSB platform</param>
        /// <param name="pixelFormat">the pixel format used for PSB (auto choose according to <see cref="PsbSpec"/> if use <see cref="PsbPixelFormat.None"/>)</param>
        /// <returns>the generated font PSB</returns>
        public PSB BuildFontPsb(string fontName, HashSet<char> characters, IDictionary<string, int> fontsWithSize, Color foregroundColor, Color backgroundColor, PsbSpec platform = PsbSpec.common,
            PsbPixelFormat pixelFormat = PsbPixelFormat.None)
        {
            PSB psb = new PSB();
            return psb;
        }

        /// <summary>
        /// Generate font PSB (simple)
        /// </summary>
        /// <param name="characters">A string or array contains all character needed</param>
        /// <param name="fontPaths">the font file paths</param>
        /// <param name="fontName">PSB font name</param>
        /// <param name="platform">target PSB platform</param>
        /// <param name="pixelFormat">the pixel format used for PSB</param>
        /// <returns></returns>
        public PSB BuildFontPsb(ReadOnlySpan<char> characters, List<string> fontPaths, string fontName = null, PsbSpec platform = PsbSpec.common, PsbPixelFormat pixelFormat = PsbPixelFormat.None)
        {
            return null;
        }
    }
}
