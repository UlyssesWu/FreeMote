using System;
using System.Drawing;
using System.IO;
using System.Linq;
using FreeMote.Psb;
using FreeMote.PsBuild.Fonts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FreeMote.Tests
{
    [TestClass]
    public class FontBuilderTest
    {
        [TestMethod]
        public void BuildFontPsbKeepsGlyphMetricsAndAtlasResourcesConsistent()
        {
            var fontPath = GetWindowsFont("arial.ttf");
            var psb = new FontBuilder(new FontBuildOptions
            {
                FontPath = fontPath,
                FontSize = 24,
                AtlasSize = 128,
                PixelType = "RGBA8",
                Platform = PsbSpec.win
            }).Build(" Aag!\r\nAa");

            Assert.AreEqual("font", psb.TypeId);
            Assert.AreEqual(PsbSpec.win, psb.Platform);

            var code = (PsbDictionary)psb.Objects["code"];
            var sources = (PsbList)psb.Objects["source"];
            Assert.AreEqual(5, code.Count);
            Assert.IsTrue(sources.Count > 0);

            foreach (var pair in code)
            {
                var glyph = (PsbDictionary)pair.Value;
                var a = Number(glyph, "a");
                var d = Number(glyph, "d");
                var width = Number(glyph, "width");
                var height = Number(glyph, "height");
                var page = Number(glyph, "id");
                var x = Number(glyph, "x");
                var y = Number(glyph, "y");
                var w = Number(glyph, "w");
                var h = Number(glyph, "h");

                Assert.AreEqual(24, height, pair.Key);
                Assert.AreEqual(a + height, d, pair.Key);
                Assert.AreEqual(width, w, pair.Key);
                Assert.IsTrue(page >= 0 && page < sources.Count, pair.Key);

                var source = (PsbDictionary)sources[page];
                Assert.IsTrue(x >= 0 && x + w <= Number(source, "width"), pair.Key);
                Assert.IsTrue(y >= 0 && y + h <= Number(source, "height"), pair.Key);
            }

            foreach (PsbDictionary source in sources)
            {
                var width = Number(source, "width");
                var height = Number(source, "height");
                var resource = (PsbResource)source["pixel"];
                Assert.AreEqual(width * height * 4, resource.Data.Length);
            }

            var bytes = psb.Build();
            using (var stream = new MemoryStream(bytes))
            {
                var roundTripped = new PSB(stream);
                Assert.AreEqual("font", roundTripped.TypeId);
                Assert.AreEqual(code.Count, ((PsbDictionary)roundTripped.Objects["code"]).Count);
                Assert.AreEqual(sources.Count, ((PsbList)roundTripped.Objects["source"]).Count);
            }
        }

        [TestMethod]
        public void BuildFontPsbSupportsTrueTypeCollections()
        {
            var fontPath = GetWindowsFont("msyh.ttc");
            var psb = new FontBuilder(new FontBuildOptions
            {
                FontPath = fontPath,
                FontIndex = 0,
                FontSize = 24,
                AtlasSize = 128,
                PixelType = "A8L8",
                Platform = PsbSpec.win
            }).Build("A中あ");

            Assert.AreEqual(3, ((PsbDictionary)psb.Objects["code"]).Count);
            Assert.IsTrue(psb.Resources.Count > 0);
        }

        [TestMethod]
        public void BuildFontPsbReportsMissingUnicodeScalars()
        {
            var fontPath = GetWindowsFont("arial.ttf");
            var builder = new FontBuilder(new FontBuildOptions
            {
                FontPath = fontPath,
                FontSize = 24,
                AtlasSize = 128
            });

            var exception = Assert.ThrowsExactly<InvalidDataException>(
                () => builder.Build(char.ConvertFromUtf32(0x10FFFF)));
            StringAssert.Contains(exception.Message, "U+10FFFF");
        }

        [TestMethod]
        public void KnownFontPlatformsHaveSampleCompatibleDefaultPixelTypes()
        {
            Assert.AreEqual("A8L8", FontBuilder.GetDefaultPixelType(PsbSpec.win));
            Assert.AreEqual("A8", FontBuilder.GetDefaultPixelType(PsbSpec.and));
            Assert.AreEqual("CI4", FontBuilder.GetDefaultPixelType(PsbSpec.psp));
            Assert.AreEqual("CI4_SW", FontBuilder.GetDefaultPixelType(PsbSpec.vita));
            Assert.AreEqual("CI4", FontBuilder.GetDefaultPixelType(PsbSpec.revo));
            Assert.AreEqual("RGBA8", FontBuilder.GetDefaultPixelType(PsbSpec.ps4));
        }

        [TestMethod]
        public void BuildFontPsbSupportsBoldAndVitaCi4Swizzle()
        {
            var fontPath = GetWindowsFont("arial.ttf");
            var regular = new FontBuilder(new FontBuildOptions
            {
                FontPath = fontPath,
                FontSize = 32,
                AtlasSize = 128,
                PixelType = "RGBA8",
                Platform = PsbSpec.win
            }).Build("AMg");
            var bold = new FontBuilder(new FontBuildOptions
            {
                FontPath = fontPath,
                FontSize = 32,
                FontStyle = FontStyle.Bold,
                AtlasSize = 128,
                PixelType = "RGBA8",
                Platform = PsbSpec.win
            }).Build("AMg");

            Assert.IsTrue(CountNonTransparentRgbaPixels(bold) > CountNonTransparentRgbaPixels(regular));

            var vita = new FontBuilder(new FontBuildOptions
            {
                FontPath = fontPath,
                FontSize = 32,
                FontStyle = FontStyle.Bold | FontStyle.Italic,
                AtlasSize = 128,
                Platform = PsbSpec.vita
            }).Build("AMg");

            PsbResource sharedPalette = null;
            foreach (PsbDictionary source in (PsbList)vita.Objects["source"])
            {
                Assert.AreEqual("CI4_SW", ((PsbString)source["type"]).Value);
                Assert.AreEqual("RGBA8", ((PsbString)source["palType"]).Value);

                var width = Number(source, "width");
                var height = Number(source, "height");
                var pixel = (PsbResource)source["pixel"];
                var palette = (PsbResource)source["pal"];
                Assert.AreEqual(width * height / 2, pixel.Data.Length);
                Assert.AreEqual(16 * 4, palette.Data.Length);

                if (sharedPalette == null)
                {
                    sharedPalette = palette;
                    Assert.AreEqual((uint)0, palette.Index);
                }
                else
                {
                    Assert.AreSame(sharedPalette, palette);
                }

                using (var decoded = RL.ConvertToImageWithPalette(pixel.Data, palette.Data, width, height,
                           PsbPixelFormat.CI4_SW, PsbPixelFormat.BeRGBA8))
                {
                    Assert.IsTrue(HasNonTransparentPixel(decoded));
                }
            }

            var bytes = vita.Build();
            using (var stream = new MemoryStream(bytes))
            {
                var roundTripped = new PSB(stream);
                var source = (PsbDictionary)((PsbList)roundTripped.Objects["source"])[0];
                Assert.AreEqual("CI4_SW", ((PsbString)source["type"]).Value);
                Assert.IsNotNull(((PsbResource)source["pal"]).Data);
            }
        }

        private static int CountNonTransparentRgbaPixels(PSB psb)
        {
            return ((PsbList)psb.Objects["source"])
                .Cast<PsbDictionary>()
                .Select(source => ((PsbResource)source["pixel"]).Data)
                .Sum(data => Enumerable.Range(0, data.Length / 4).Count(i => data[i * 4 + 3] != 0));
        }

        private static bool HasNonTransparentPixel(Bitmap bitmap)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    if (bitmap.GetPixel(x, y).A != 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int Number(PsbDictionary dictionary, string key)
        {
            return ((PsbNumber)dictionary[key]).AsInt;
        }

        private static string GetWindowsFont(string fileName)
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var path = Path.Combine(windows, "Fonts", fileName);
            if (!File.Exists(path))
            {
                Assert.Inconclusive($"Required Windows test font was not found: {path}");
            }

            return path;
        }
    }
}
