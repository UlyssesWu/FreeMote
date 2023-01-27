using System.Collections.Generic;
using System.Numerics;
using FreeMote.Psb;
using FreeMote.Psb.Textures;
using SixLabors.Fonts;
using SixLabors.Fonts.Tables.AdvancedTypographic;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using Color = SixLabors.ImageSharp.Color;
using static System.Net.Mime.MediaTypeNames;

namespace FreeMote.PsBuild
{
    internal class FontContext
    {
        public bool Debug { get; set; } = false;
        public int? TextureSideLength { get; set; } = null;
        public int TexturePadding { get; set; } = 5;
        public BestFitHeuristic FitHeuristic { get; set; } = BestFitHeuristic.MaxOneAxis;
        public List<TextureInfo> Glyphs { get; set; } = new();
        public readonly Dictionary<char, Font> CharFonts = new();
        public PsbPixelFormat PixelFormat { get; set; }
        public PsbSpec Platform { get; set; }
        public Color ForegroundColor { get; set; }
        public Color BackgroundColor { get; set; }
        public Color? OutlineColor { get; set; }
        public int OutlineWidth { get; set; } = 1;
        public PsbDictionary Code { get; set; }

        public FontContext(PsbPixelFormat pixelFormat, PsbSpec platform, Color foregroundColor, Color backgroundColor)
        {
            PixelFormat = pixelFormat;
            Platform = platform;
            ForegroundColor = foregroundColor;
            BackgroundColor = backgroundColor;
        }

        public void Pack()
        {
            //Pack textures
            int size = 2048;
            int padding = TexturePadding is >= 0 and <= 100 ? TexturePadding : 1;

            TexturePacker packer = new TexturePacker
            {
                FitHeuristic = FitHeuristic
            };
            packer.Process(Glyphs, TextureSideLength ?? size, padding);

            Dictionary<Font, TextOptions> options = new Dictionary<Font, TextOptions>();
            int id = 0;
            DrawingOptions drawingOptions = new DrawingOptions();
            foreach (var atlas in packer.Atlasses)
            {
                var image = new Image<Rgba32>(atlas.Width, atlas.Height);
                image.Mutate(context =>
                {
                    context.Clear(BackgroundColor);
                    foreach (Node n in atlas.Nodes)
                    {
                        var glyph = n.Texture;
                        if (Debug)
                        {
                            context.DrawPolygon(Color.DarkRed, 1, 
                                new PointF(n.Bounds.X, n.Bounds.Y),
                                new PointF(n.Bounds.X + n.Bounds.Width, n.Bounds.Y),
                                new PointF(n.Bounds.X + n.Bounds.Width, n.Bounds.Y + n.Bounds.Height),
                                new PointF(n.Bounds.X, n.Bounds.Y + n.Bounds.Height));
                        }

                        
                        var font = CharFonts[glyph.Source[0]];
                        TextOptions o;
                        if (!options.TryGetValue(font, out o))
                        {
                            o = new TextOptions(font)
                            {
                                KerningMode = KerningMode.None, HintingMode = HintingMode.None, LayoutMode = LayoutMode.HorizontalTopBottom, 
                            };
                            options[font] = o;
                        }

                        o.Origin = Vector2.Zero;
                        //o.Origin = new Vector2(n.Bounds.X, n.Bounds.Y);
                        var path = TextBuilder.GenerateGlyphs(glyph.Source, o);
                        var offset = -path.Bounds.Location + new PointF(n.Bounds.X, n.Bounds.Y);
                        path = path.Translate(offset);
                        if (OutlineColor == null || OutlineWidth <= 0)
                        {
                            context.Fill(drawingOptions, ForegroundColor, path);
                        }
                        else
                        {
                            context.DrawText(drawingOptions, glyph.Source, font, Brushes.Solid(ForegroundColor),
                                Pens.Solid(OutlineColor.Value, OutlineWidth),
                                offset);
                        }
                    }
                });

                image.SaveAsPng($"{id++}.png");
            }
        }
    }
}
