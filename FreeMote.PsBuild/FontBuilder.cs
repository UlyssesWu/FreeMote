using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using FreeMote.Psb;
using FreeMote.Psb.Textures;
using SixLabors.Fonts;
using SixLabors.Fonts.Unicode;
using SixLabors.ImageSharp.Drawing;
using Font = SixLabors.Fonts.Font;

// ReSharper disable PossibleMultipleEnumeration

namespace FreeMote.PsBuild
{
    /// <summary>
    /// Generate font PSB
    /// </summary>
    public class FontBuilder
    {
        private readonly FontCollection _fontCollection = new FontCollection();

        internal FontCollection FontCollection => _fontCollection;

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

        /// <summary>
        /// Determines whether the font with the specified name exists.
        /// </summary>
        /// <param name="fontName">Name of the font.</param>
        public bool FontExists(string fontName)
        {
            return _fontCollection.TryGet(fontName, out _);
        }

/* {
  "code": {
    " ": {
      "a": 0,
      "b": 16,
      "d": 19.0,
      "h": 20,
      "height": 20,
      "id": 0,
      "w": 7.0,
      "width": 7.0,
      "x": 1,
      "y": 1
    },
    "￥": {
      "a": -2,
      "b": 14,
      "d": 17.0,
      "h": 16,
      "height": 20,
      "id": 5,
      "w": 20.0,
      "width": 20.0,
      "x": 331.0,
      "y": 412
    }
  },
    "id": "font",
  "label": "新規プロジェクト",
  "maxHeight": 20,
  "maxWidth": 20,
  "minHeight": 20,
  "minWidth": 4,
  "source": [
{
      "height": 512,
      "pal": "#resource#0",
      "palType": "RGBA8",
      "pixel": "#resource#1",
      "type": "CI4",
      "width": 512
    }],
  "spec": "psp",
  "version": 1.08
}
*/

        private SixLabors.ImageSharp.Color ToColor(Color color)
        {
            return SixLabors.ImageSharp.Color.FromRgba(color.R, color.G, color.B, color.A);
        }

        /// <summary>
        /// Generate font PSB
        /// </summary>
        /// <param name="fontName">the name of the font (in file)</param>
        /// <param name="characters">all character needed, with font name and size</param>
        /// <param name="foregroundColor">the color of characters, usually <see cref="Color.White"/></param>
        /// <param name="backgroundColor">the color of the background, usually <see cref="Color.Transparent"/></param>
        /// <param name="platform">target PSB platform</param>
        /// <param name="pixelFormat">the pixel format used for PSB (auto choose according to <see cref="PsbSpec"/> if use <see cref="PsbPixelFormat.None"/>)</param>
        /// <param name="psbVersion">PSB version</param>
        /// <returns>the generated font PSB</returns>
        public PSB BuildFontPsb(string fontName, List<(string FontName, HashSet<char> Characters, int Size)> characters,
            Color foregroundColor, Color backgroundColor, PsbSpec platform = PsbSpec.common,
            PsbPixelFormat pixelFormat = PsbPixelFormat.None, ushort psbVersion = 2)
        {
            var context = new FontContext(pixelFormat, platform, ToColor(foregroundColor), ToColor(backgroundColor));
            PSB psb = new PSB(psbVersion)
            {
                Objects = new PsbDictionary
                {
                    ["spec"] = platform.ToPsbString(),
                    ["version"] = new PsbNumber(1.08f),
                    ["id"] = "font".ToPsbString(),
                    ["label"] = (fontName ?? "新規プロジェクト").ToPsbString()
                },
                Type = PsbType.BmpFont
            };
            var source = new PsbList();
            psb.Objects["source"] = source;
            var code = BuildCode(characters, context);
            context.Code = code;
            psb.Objects["code"] = code;
            context.Debug = true;
            context.OutlineColor = ToColor(Color.Gold);
            context.OutlineWidth = 2;
            context.Pack();
            
            return psb;
        }

        internal PsbDictionary BuildCode(List<(string FontName, HashSet<char> Characters, int Size)> characters, FontContext context)
        {
            var code = new PsbDictionary();
            foreach (var tuple in characters)
            {
                if (tuple.Characters.Count == 0)
                {
                    continue;
                }

                var family = string.IsNullOrEmpty(tuple.FontName) ? FontCollection.Families.First() : FontCollection.Get(tuple.FontName);
                var font = family.CreateFont(tuple.Size);
                foreach (var c in tuple.Characters)
                {
                    context.CharFonts[c] = font;
                    code[c.ToString()] = BuildChar(font, c, context);
                }
            }

            return code;
        }

        internal PsbDictionary BuildChar(Font font, char character, FontContext context)
        {
            var obj = new PsbDictionary();
            if (!font.TryGetGlyphs(new CodePoint(character), ColorFontSupport.None, out var glyphs))
            {
                return null;
            }
            if (glyphs.Any())
            {
                //height = a + d
                //b = baseline: a/n:17, b/l:25, g:18
                var fontSize = font.Size;
                var sizeOfOnePixel = font.FontMetrics.UnitsPerEm / fontSize;
                var glyph = glyphs.First();
                var metrics = glyph.GlyphMetrics;
                var cHeight = (int) Math.Ceiling(metrics.Height / sizeOfOnePixel);
                var cWidth = (int) Math.Ceiling(metrics.Width / sizeOfOnePixel);
                context.Glyphs.Add(new TextureInfo {Height = cHeight, Width = cWidth, Source = character.ToString()});

                obj["h"] = new PsbNumber(cHeight); //actual glyph height
                obj["w"] = new PsbNumber(cWidth); //actual glyph width
                obj["width"] = obj["w"];
                obj["height"] = new PsbNumber(fontSize);
                obj["a"] = new PsbNumber(Math.Ceiling(-(metrics.TopSideBearing / sizeOfOnePixel))); //internal leading; top side bearing
                obj["d"] = new PsbNumber(Math.Ceiling(fontSize - (metrics.TopSideBearing / sizeOfOnePixel))); //dimension?
                obj["b"] = obj["d"]; //TODO: how to calculate baseline
                obj["x"] = PsbNumber.Zero;
                obj["y"] = PsbNumber.Zero;
                obj["id"] = PsbNumber.Zero;
                return obj;
            }

            return null;
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
        public PSB BuildFontPsb(ReadOnlySpan<char> characters, List<string> fontPaths, string fontName = null,
            PsbSpec platform = PsbSpec.common, PsbPixelFormat pixelFormat = PsbPixelFormat.None)
        {
            return null;
        }
    }
}