using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FreeMote.Psb;
using FreeMote.Psb.Textures;
using WaterTrans.GlyphLoader;
using WaterTrans.GlyphLoader.Geometry;
using DrawingPoint = System.Drawing.PointF;
using GlyphPoint = WaterTrans.GlyphLoader.Geometry.Point;

namespace FreeMote.PsBuild.Fonts
{
    /// <summary>
    /// Builds an E-mote bitmap-font PSB from Unicode characters and an OpenType font.
    /// </summary>
    public sealed class FontBuilder
    {
        private static readonly HashSet<string> SupportedPixelTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RGBA8", "RGBA8_SW", "RGBA4444", "RGBA4444_SW", "RGBA5650", "RGBA5650_SW",
            "A8L8", "A8L8_SW", "L8", "L8_SW", "A8", "A8_SW",
            "CI4", "CI4_SW", "CI8", "CI8_SW"
        };

        private readonly FontBuildOptions _options;

        public FontBuilder(FontBuildOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Build a PSB from all distinct, non-control Unicode scalar values in <paramref name="characters"/>.
        /// Character order follows the first occurrence in the input.
        /// </summary>
        public PSB Build(string characters)
        {
            _options.Validate();
            if (characters == null)
            {
                throw new ArgumentNullException(nameof(characters));
            }

            var fontPath = ResolveFontPath(_options.FontPath);
            var codePoints = CollectCodePoints(characters);
            if (codePoints.Count == 0)
            {
                throw new InvalidDataException("The input does not contain any buildable characters.");
            }

            var pixelType = string.IsNullOrWhiteSpace(_options.PixelType)
                ? GetDefaultPixelType(_options.Platform)
                : _options.PixelType.Trim().ToUpperInvariant();
            if (!SupportedPixelTypes.Contains(pixelType))
            {
                throw new NotSupportedException(
                    $"Pixel type '{pixelType}' is not supported by the font builder. " +
                    $"Supported types: {string.Join(", ", SupportedPixelTypes)}.");
            }

            var pixelFormat = pixelType.ToPsbPixelFormat(_options.Platform);
            if (pixelFormat == PsbPixelFormat.None)
            {
                throw new NotSupportedException(
                    $"Pixel type '{pixelType}' is not available for platform '{_options.Platform}'.");
            }

            Typeface typeface;
            using (var stream = File.OpenRead(fontPath))
            {
                typeface = new Typeface(stream, _options.FontIndex);
            }

            if (typeface.Height <= 0 || double.IsNaN(typeface.Height) || double.IsInfinity(typeface.Height))
            {
                throw new InvalidDataException("The font contains invalid vertical metrics.");
            }

            var renderingEmSize = _options.FontSize / typeface.Height;
            var ascent = Clamp(
                (int)Math.Round(typeface.Baseline * renderingEmSize, MidpointRounding.AwayFromZero),
                0,
                _options.FontSize);

            var glyphs = RenderGlyphs(typeface, codePoints, renderingEmSize, ascent);
            try
            {
                return BuildPsb(glyphs, pixelFormat, pixelType, ascent);
            }
            finally
            {
                foreach (var glyph in glyphs)
                {
                    glyph.Bitmap.Dispose();
                }
            }
        }

        /// <summary>
        /// Read characters from a text file and build a PSB.
        /// </summary>
        public PSB BuildFromTextFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Character input path must be specified.", nameof(path));
            }

            return Build(File.ReadAllText(path));
        }

        /// <summary>
        /// Read characters from a text file and write the resulting PSB.
        /// </summary>
        public void BuildToFile(string inputPath, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Output path must be specified.", nameof(outputPath));
            }

            var fullOutputPath = Path.GetFullPath(outputPath);
            var outputDirectory = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            BuildFromTextFile(inputPath).BuildToFile(fullOutputPath);
        }

        /// <summary>
        /// Resolve a font path relative to the current directory, application directory, or Windows Fonts directory.
        /// </summary>
        public static string ResolveFontPath(string fontPath)
        {
            if (string.IsNullOrWhiteSpace(fontPath))
            {
                throw new ArgumentException("Font path must be specified.", nameof(fontPath));
            }

            var candidates = new List<string>();
            if (Path.IsPathRooted(fontPath))
            {
                candidates.Add(fontPath);
            }
            else
            {
                candidates.Add(Path.GetFullPath(fontPath));
                candidates.Add(Path.Combine(AppContext.BaseDirectory, fontPath));

                var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                if (!string.IsNullOrWhiteSpace(windowsDirectory))
                {
                    candidates.Add(Path.Combine(windowsDirectory, "Fonts", fontPath));
                }
            }

            var match = candidates.FirstOrDefault(File.Exists);
            if (match == null)
            {
                throw new FileNotFoundException(
                    $"Font file was not found. Checked: {string.Join("; ", candidates.Distinct(StringComparer.OrdinalIgnoreCase))}",
                    fontPath);
            }

            return Path.GetFullPath(match);
        }

        /// <summary>
        /// Return the bitmap-font pixel type used by known samples for a platform.
        /// Unknown platforms use RGBA8 as the conservative lossless fallback.
        /// </summary>
        public static string GetDefaultPixelType(PsbSpec platform)
        {
            switch (platform)
            {
                case PsbSpec.win:
                    return "A8L8";
                case PsbSpec.and:
                    return "A8";
                case PsbSpec.psp:
                    return "CI4";
                case PsbSpec.vita:
                    return "CI4_SW";
                case PsbSpec.revo:
                    return "CI4";
                default:
                    return "RGBA8";
            }
        }

        private List<RenderedGlyph> RenderGlyphs(Typeface typeface, IReadOnlyList<CodePoint> codePoints,
            double renderingEmSize, int ascent)
        {
            var missing = codePoints.Where(c => !typeface.CharacterToGlyphMap.ContainsKey(c.Value)).ToList();
            if (missing.Count > 0)
            {
                var display = string.Join(", ", missing.Take(32).Select(c => $"U+{c.Value:X4} ({Escape(c.Text)})"));
                if (missing.Count > 32)
                {
                    display += $", ... and {missing.Count - 32} more";
                }

                throw new InvalidDataException($"The selected font does not contain {missing.Count} requested character(s): {display}");
            }

            var glyphs = new List<RenderedGlyph>(codePoints.Count);
            foreach (var codePoint in codePoints)
            {
                var glyphIndex = typeface.CharacterToGlyphMap[codePoint.Value];
                var advanceWidth = typeface.AdvanceWidths.TryGetValue(glyphIndex, out var normalizedAdvance)
                    ? Math.Max(1, (int)Math.Ceiling(normalizedAdvance * renderingEmSize))
                    : 1;

                var geometry = typeface.GetGlyphOutline(glyphIndex, renderingEmSize);
                using (var path = ToGraphicsPath(geometry))
                {
                    ApplyFontStyle(path, advanceWidth, ascent);
                    if (path.PointCount == 0)
                    {
                        glyphs.Add(new RenderedGlyph
                        {
                            CodePoint = codePoint,
                            Bitmap = new Bitmap(advanceWidth, Math.Max(1, _options.FontSize)),
                            Width = advanceWidth,
                            Height = Math.Max(1, _options.FontSize),
                            Baseline = ascent
                        });
                        continue;
                    }

                    Pen boldPen = null;
                    try
                    {
                        if ((_options.FontStyle & FontStyle.Bold) != 0)
                        {
                            boldPen = new Pen(Color.White, Math.Max(1f, _options.FontSize / 24f))
                            {
                                LineJoin = LineJoin.Round
                            };
                        }

                        var bounds = boldPen == null ? path.GetBounds() : path.GetBounds(null, boldPen);
                        var top = (int)Math.Floor(bounds.Top);
                        var bottom = (int)Math.Ceiling(bounds.Bottom);
                        var left = (int)Math.Floor(bounds.Left);
                        var right = (int)Math.Ceiling(bounds.Right);
                        var xShift = left < 0 ? -left : 0;
                        var width = Math.Max(advanceWidth, right + xShift);
                        var height = Math.Max(1, bottom - top);
                        width = Math.Max(1, width);

                        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                        using (var graphics = Graphics.FromImage(bitmap))
                        using (var brush = new SolidBrush(Color.White))
                        {
                            graphics.Clear(Color.Transparent);
                            graphics.CompositingMode = CompositingMode.SourceCopy;
                            graphics.CompositingQuality = CompositingQuality.HighQuality;
                            graphics.SmoothingMode = SmoothingMode.AntiAlias;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            graphics.TranslateTransform(xShift, -top);
                            graphics.FillPath(brush, path);
                            if (boldPen != null)
                            {
                                graphics.CompositingMode = CompositingMode.SourceOver;
                                graphics.DrawPath(boldPen, path);
                            }
                        }

                        glyphs.Add(new RenderedGlyph
                        {
                            CodePoint = codePoint,
                            Bitmap = bitmap,
                            Width = width,
                            Height = height,
                            Baseline = -top
                        });
                    }
                    finally
                    {
                        boldPen?.Dispose();
                    }
                }
            }

            return glyphs;
        }

        private void ApplyFontStyle(GraphicsPath path, int advanceWidth, int ascent)
        {
            if ((_options.FontStyle & FontStyle.Italic) != 0 && path.PointCount > 0)
            {
                // Glyph coordinates use the baseline as Y=0. A negative shear therefore moves the
                // top of the outline to the right while keeping the baseline fixed.
                using (var matrix = new Matrix(1f, 0f, -0.2f, 1f, 0f, 0f))
                {
                    path.Transform(matrix);
                }
            }

            var decorationThickness = Math.Max(1f, _options.FontSize / 16f);
            if ((_options.FontStyle & FontStyle.Underline) != 0)
            {
                var underlineY = Math.Max(1f, (_options.FontSize - ascent) * 0.3f);
                path.AddRectangle(new RectangleF(0, underlineY, advanceWidth, decorationThickness));
            }

            if ((_options.FontStyle & FontStyle.Strikeout) != 0)
            {
                var strikeoutY = -ascent * 0.32f - decorationThickness / 2f;
                path.AddRectangle(new RectangleF(0, strikeoutY, advanceWidth, decorationThickness));
            }
        }

        private PSB BuildPsb(IReadOnlyList<RenderedGlyph> glyphs, PsbPixelFormat pixelFormat, string pixelType,
            int ascent)
        {
            var images = glyphs.ToDictionary(g => g.CodePoint.Text, g => (Image)g.Bitmap, StringComparer.Ordinal);
            var glyphMap = glyphs.ToDictionary(g => g.CodePoint.Text, StringComparer.Ordinal);
            var packer = new TexturePacker {FitHeuristic = BestFitHeuristic.Area};
            packer.Process(images, _options.AtlasSize, _options.Padding);

            var sources = new PsbList(packer.Atlasses.Count);
            PsbResource paletteResource = null;
            var palettePixelFormat = _options.Platform.DefaultPalettePixelFormat();
            if (palettePixelFormat == PsbPixelFormat.None)
            {
                palettePixelFormat = PsbPixelFormat.LeRGBA8;
            }

            for (var pageIndex = 0; pageIndex < packer.Atlasses.Count; pageIndex++)
            {
                var atlas = packer.Atlasses[pageIndex];
                var textureNodes = atlas.Nodes.Where(n => n.Texture != null).ToList();
                // The reference font atlases keep a transparent outer pixel. Preserve it whenever
                // the packed page has room without changing its power-of-two dimensions.
                var offsetX = textureNodes.Max(n => n.Bounds.Right) < atlas.Width ? 1 : 0;
                var offsetY = textureNodes.Max(n => n.Bounds.Bottom) < atlas.Height ? 1 : 0;
                foreach (var node in textureNodes)
                {
                    node.Bounds.Offset(offsetX, offsetY);
                    var glyph = glyphMap[node.Texture.Source];
                    glyph.Page = pageIndex;
                    glyph.X = node.Bounds.X;
                    glyph.Y = node.Bounds.Y;
                }

                using (var atlasImage = atlas.ToImage(background: Color.Transparent))
                {
                    Bitmap indexedAtlas = null;
                    try
                    {
                        var encodedImage = atlasImage;
                        if (pixelFormat.UsePalette())
                        {
                            var bitDepth = pixelFormat.GetBitDepth();
                            if (bitDepth != 4 && bitDepth != 8)
                            {
                                throw new NotSupportedException(
                                    $"Indexed pixel type '{pixelType}' does not have a supported bit depth.");
                            }

                            indexedAtlas = CreateAlphaIndexedBitmap((Bitmap)atlasImage, bitDepth.Value);
                            encodedImage = indexedAtlas;
                            if (paletteResource == null)
                            {
                                paletteResource = new PsbResource
                                {
                                    Data = indexedAtlas.Palette.GetPaletteBytes(palettePixelFormat)
                                };
                            }
                        }

                        var source = new PsbDictionary
                        {
                            ["height"] = new PsbNumber(atlas.Height)
                        };
                        if (paletteResource != null)
                        {
                            // Reference samples allocate the shared palette before page pixels, normally
                            // making it resource #0. Preserve that traversal order for closer compatibility.
                            source["pal"] = paletteResource;
                            source["palType"] = palettePixelFormat.ToStringForPsb().ToPsbString();
                        }

                        source["pixel"] = new PsbResource
                        {
                            Data = RL.GetPixelBytesFromImage(encodedImage, pixelFormat)
                        };
                        source["type"] = pixelType.ToPsbString();
                        source["width"] = new PsbNumber(atlas.Width);

                        sources.Add(source);
                    }
                    finally
                    {
                        indexedAtlas?.Dispose();
                    }
                }
            }

            var code = new PsbDictionary(glyphs.Count);
            foreach (var glyph in glyphs)
            {
                var a = glyph.Baseline - ascent;
                code.Add(glyph.CodePoint.Text, new PsbDictionary
                {
                    ["a"] = new PsbNumber(a),
                    ["b"] = new PsbNumber(glyph.Baseline),
                    ["d"] = new PsbNumber((float)(a + _options.FontSize)),
                    ["h"] = new PsbNumber(glyph.Height),
                    ["height"] = new PsbNumber((float)_options.FontSize),
                    ["id"] = new PsbNumber(glyph.Page),
                    ["w"] = new PsbNumber((float)glyph.Width),
                    ["width"] = new PsbNumber((float)glyph.Width),
                    ["x"] = new PsbNumber(glyph.X),
                    ["y"] = new PsbNumber(glyph.Y)
                });
            }

            var psb = new PSB(_options.PsbVersion)
            {
                Type = PsbType.BmpFont,
                Objects = new PsbDictionary
                {
                    ["code"] = code,
                    ["id"] = "font".ToPsbString(),
                    ["label"] = (_options.Label ?? "normal").ToPsbString(),
                    ["maxHeight"] = new PsbNumber(_options.FontSize),
                    ["maxWidth"] = new PsbNumber(glyphs.Max(g => g.Width)),
                    ["minHeight"] = new PsbNumber(_options.FontSize),
                    ["minWidth"] = new PsbNumber(glyphs.Min(g => g.Width)),
                    ["source"] = sources,
                    ["spec"] = _options.Platform.ToString().ToPsbString(),
                    ["version"] = new PsbNumber(1.08)
                }
            };
            psb.Merge(true);
            return psb;
        }

        /// <summary>
        /// Convert the white-on-transparent atlas to a fixed alpha palette. A shared linear palette is
        /// sufficient for font atlases and avoids page-to-page palette drift from a general image quantizer.
        /// </summary>
        private static Bitmap CreateAlphaIndexedBitmap(Bitmap source, int bitDepth)
        {
            if (bitDepth != 4 && bitDepth != 8)
            {
                throw new ArgumentOutOfRangeException(nameof(bitDepth), "Indexed font atlases must be CI4 or CI8.");
            }

            var targetFormat = bitDepth == 4 ? PixelFormat.Format4bppIndexed : PixelFormat.Format8bppIndexed;
            var target = new Bitmap(source.Width, source.Height, targetFormat);
            var palette = target.Palette;
            var maximumIndex = (1 << bitDepth) - 1;
            for (var i = 0; i <= maximumIndex; i++)
            {
                var alpha = (i * 255 + maximumIndex / 2) / maximumIndex;
                palette.Entries[i] = Color.FromArgb(alpha, 255, 255, 255);
            }

            target.Palette = palette;

            var bounds = new Rectangle(0, 0, source.Width, source.Height);
            BitmapData sourceData = null;
            BitmapData targetData = null;
            try
            {
                try
                {
                    sourceData = source.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    targetData = target.LockBits(bounds, ImageLockMode.WriteOnly, targetFormat);
                    var sourceRow = new byte[Math.Abs(sourceData.Stride)];
                    var targetRow = new byte[Math.Abs(targetData.Stride)];
                    for (var y = 0; y < source.Height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(sourceData.Scan0, y * sourceData.Stride), sourceRow, 0,
                            sourceRow.Length);
                        Array.Clear(targetRow, 0, targetRow.Length);
                        for (var x = 0; x < source.Width; x++)
                        {
                            var alpha = sourceRow[x * 4 + 3];
                            if (bitDepth == 8)
                            {
                                targetRow[x] = alpha;
                            }
                            else
                            {
                                var index = (alpha * maximumIndex + 127) / 255;
                                if ((x & 1) == 0)
                                {
                                    targetRow[x >> 1] = (byte)(index << 4);
                                }
                                else
                                {
                                    targetRow[x >> 1] |= (byte)index;
                                }
                            }
                        }

                        Marshal.Copy(targetRow, 0, IntPtr.Add(targetData.Scan0, y * targetData.Stride),
                            targetRow.Length);
                    }
                }
                finally
                {
                    if (sourceData != null)
                    {
                        source.UnlockBits(sourceData);
                    }

                    if (targetData != null)
                    {
                        target.UnlockBits(targetData);
                    }
                }
            }
            catch
            {
                target.Dispose();
                throw;
            }

            return target;
        }

        private static GraphicsPath ToGraphicsPath(PathGeometry geometry)
        {
            var path = new GraphicsPath
            {
                FillMode = geometry.FillRule == FillRule.Nonzero ? FillMode.Winding : FillMode.Alternate
            };

            foreach (var figure in geometry.Figures)
            {
                path.StartFigure();
                var current = figure.StartPoint;
                foreach (var segment in figure.Segments)
                {
                    switch (segment)
                    {
                        case LineSegment line:
                            path.AddLine(ToDrawingPoint(current), ToDrawingPoint(line.Point));
                            current = line.Point;
                            break;
                        case QuadraticBezierSegment quadratic:
                            var control1 = new GlyphPoint(
                                current.X + (quadratic.Point1.X - current.X) * 2.0 / 3.0,
                                current.Y + (quadratic.Point1.Y - current.Y) * 2.0 / 3.0);
                            var control2 = new GlyphPoint(
                                quadratic.Point2.X + (quadratic.Point1.X - quadratic.Point2.X) * 2.0 / 3.0,
                                quadratic.Point2.Y + (quadratic.Point1.Y - quadratic.Point2.Y) * 2.0 / 3.0);
                            path.AddBezier(ToDrawingPoint(current), ToDrawingPoint(control1),
                                ToDrawingPoint(control2), ToDrawingPoint(quadratic.Point2));
                            current = quadratic.Point2;
                            break;
                        case BezierSegment cubic:
                            path.AddBezier(ToDrawingPoint(current), ToDrawingPoint(cubic.Point1),
                                ToDrawingPoint(cubic.Point2), ToDrawingPoint(cubic.Point3));
                            current = cubic.Point3;
                            break;
                        default:
                            throw new NotSupportedException($"Unsupported glyph path segment: {segment.GetType().FullName}");
                    }
                }

                if (figure.IsClosed)
                {
                    path.CloseFigure();
                }
            }

            return path;
        }

        private static DrawingPoint ToDrawingPoint(GlyphPoint point)
        {
            return new DrawingPoint((float)point.X, (float)point.Y);
        }

        private static List<CodePoint> CollectCodePoints(string text)
        {
            var result = new List<CodePoint>();
            var seen = new HashSet<int>();
            for (var i = 0; i < text.Length; i++)
            {
                var value = char.ConvertToUtf32(text, i);
                var scalar = char.ConvertFromUtf32(value);
                if (scalar.Length == 2)
                {
                    i++;
                }

                if (char.IsControl(scalar, 0) || !seen.Add(value))
                {
                    continue;
                }

                result.Add(new CodePoint(value, scalar));
            }

            return result;
        }

        private static string Escape(string value)
        {
            if (value == " ")
            {
                return "SPACE";
            }

            return value;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }

        private sealed class CodePoint
        {
            public CodePoint(int value, string text)
            {
                Value = value;
                Text = text;
            }

            public int Value { get; }
            public string Text { get; }
        }

        private sealed class RenderedGlyph
        {
            public CodePoint CodePoint { get; set; }
            public Bitmap Bitmap { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int Baseline { get; set; }
            public int Page { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
        }
    }
}
