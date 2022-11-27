using System.Collections.Generic;
using FreeMote.Psb;
using Color = System.Drawing.Color;

namespace FreeMote.Font
{
    //a: ascent; b: baseline; d: descent; h: char height
    //https://docs.sixlabors.com/articles/fonts/gettingstarted.html
    
    /// <summary>
    /// Generate font PSB
    /// </summary>
    public class FontBuilder
    {
        /// <summary>
        /// label: the name of the font
        /// </summary>
        public string Name { get; set; } = "新規プロジェクト";

        /// <summary>
        /// all character needed
        /// </summary>
        public HashSet<char> Characters { get; set; }

        /// <summary>
        /// target PSB platform
        /// </summary>
        public PsbSpec Platform { get; set; } = PsbSpec.common;

        /// <summary>
        /// the pixel format used for PSB (auto choose according to <see cref="PsbSpec"/> if use <see cref="PsbPixelFormat.None"/>)
        /// </summary>
        public PsbPixelFormat PixelFormat { get; set; } = PsbPixelFormat.None;

        /// <summary>
        /// font PSB version, usually <c>1.08</c>
        /// </summary>
        public float Version { get; set; } = 1.08f;

        /// <summary>
        /// the color of characters, usually <see cref="System.Drawing.Color.White"/>
        /// </summary>
        public Color ForegroundColor { get; set; } = Color.White;

        /// <summary>
        /// the color of the background, usually <see cref="System.Drawing.Color.Transparent"/>
        /// </summary>
        public Color BackgroundColor { get; set; } = Color.Transparent;
        
        /// <summary>
        /// A list of fonts, if one character doesn't exist in a font, fallback to next font.
        /// </summary>
        public List<(string Font, int FontSize)> Fonts { get; set; }

        /// <summary>
        /// Generate font PSB
        /// </summary>
        /// <returns>the generated font PSB</returns>
        public PSB BuildFontPsb()
        {
            PSB psb = new PSB() {Platform = Platform};

            psb.Objects = new PsbDictionary()
            {
                {"label", Name.ToPsbString()},
                {"version", Version.ToPsbNumber()},
                {"id", "font".ToPsbString()},
            };

            var code = new PsbDictionary(Characters.Count);

            return psb;
        }

        //TODO: use GlyphInstance.Fallback
    }
}