using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using FreeMote.Plugins.Properties;
using FreeMote.Psb;
using PhotoshopFile;

namespace FreeMote.Plugins.Shells
{
    [Export(typeof(IPsbShell))]
    [ExportMetadata("Name", "FreeMote.Psd")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "PSD export.")]
    class PsdShell : IPsbShell
    {
        public int Width { get; set; } = 2000;
        public int Height { get; set; } = 2000;

        public bool TryGetCanvasSize { get; set; } = true;
        public string Name => "PSD";

        public bool IsInShell(Stream stream, Dictionary<string, object> context = null)
        {
            var header = new byte[4];
            var pos = stream.Position;
            stream.Read(header, 0, 4);
            stream.Position = pos;
            if (header.SequenceEqual(Signature))
            {
                if (context != null)
                {
                    context[Consts.Context_PsbShellType] = Name;
                }

                return true;
            }

            return false;
        }

        public MemoryStream ToPsb(Stream stream, Dictionary<string, object> context = null)
        {
            Console.WriteLine("PSD to PSB conversion is not supported.");
            return null;
            //throw new NotSupportedException("PSD to PSB conversion is not supported.");
            //var ms = new MemoryStream();
            //stream.CopyTo(ms);
            //return ms;
        }

        public MemoryStream ToShell(Stream stream, Dictionary<string, object> context = null)
        {
            Console.WriteLine(
                "[WARN] Exported PSD files should follow CC-BY-NC-SA 4.0. Please keep FreeMote information in PSD files.");
            var psb = new PSB(stream);
            if (psb == null)
            {
                throw new BadImageFormatException("Not a valid PSB file.");
            }

            PsbPainter painter = new PsbPainter(psb);

            if (TryGetCanvasSize && painter.Resources.Count > 0)
            {
                if (psb.TryGetCanvasSize(out var cw, out var ch))
                {
                    Width = (int)(cw * 1.8f);
                    Height = (int)(ch * 1.4f);
                }
                else
                {
                    //Try get from painter, not accurate if the PSB center is not (0,0)
                    Width = (int)(painter.Resources.Max(r => r.OriginX + r.Width / 2.0f) -
                                  painter.Resources.Min(r => r.OriginX - r.Width / 2.0f));
                    Height = (int)(painter.Resources.Max(r => r.OriginY + r.Height / 2.0f) -
                                   painter.Resources.Min(r => r.OriginY - r.Height / 2.0f));

                    Width = (int)(Width * 1.4f);
                    Height = (int)(Height * 1.4f);
                }

                if (context != null)
                {
                    if (context.ContainsKey("Width") && context["Width"] is int width)
                    {
                        Width = width;
                    }

                    if (context.ContainsKey("Height") && context["Height"] is int height)
                    {
                        Height = height;
                    }
                }
            }

            var psd = ConvertToPsd(painter, Width, Height);
            var ms = new MemoryStream();
            psd.Save(ms, Encoding.UTF8);
            return ms;
        }

        private PsdFile ConvertToPsd(PsbPainter painter, int width, int height)
        {
            PsdFile psd = new PsdFile
            {
                Width = width,
                Height = height,
                Resolution = new ResolutionInfo
                {
                    HeightDisplayUnit = ResolutionInfo.Unit.Centimeters,
                    WidthDisplayUnit = ResolutionInfo.Unit.Centimeters,
                    HResDisplayUnit = ResolutionInfo.ResUnit.PxPerInch,
                    VResDisplayUnit = ResolutionInfo.ResUnit.PxPerInch,
                    HDpi = new UFixed16_16(0, 350),
                    VDpi = new UFixed16_16(0, 350)
                },
                ImageCompression = ImageCompression.Rle
            };

            psd.ImageResources.Add(new XmpResource("") {XmpMetaString = Resources.Xmp});
            psd.BaseLayer.SetBitmap(new Bitmap(width, height, PixelFormat.Format32bppArgb),
                ImageReplaceOption.KeepCenter, psd.ImageCompression);

            string currentGroup = "";
            Layer beginSection = null;
            foreach (var resMd in painter.Resources)
            {
                if (resMd.Label.StartsWith(painter.GroupMark))
                {
                    resMd.Label = resMd.Label.Substring(1);
                }

                string name = $"{resMd.Label}-{resMd.Name}";

                var layer = psd.MakeImageLayer(resMd.ToImage(), name, (int) (resMd.OriginX + width / 2f - resMd.Width / 2f),
                    (int) (resMd.OriginY + height / 2f - resMd.Height / 2f));
                layer.Visible = resMd.Visible;
                if (resMd.Opacity <= 0)
                {
                    layer.Opacity = 0;
                }
                else
                {
                    layer.Opacity = (byte)(resMd.Opacity /10.0f * 255);
                }

                if (resMd.MotionName != currentGroup)
                {
                    currentGroup = resMd.MotionName;
                    if (beginSection != null)
                    {
                        psd.Layers.Add(beginSection);
                        beginSection = null;
                    }

                    if (!string.IsNullOrEmpty(currentGroup))
                    {
                        beginSection = psd.MakeSectionLayers(currentGroup, out var endLayer, false);
                        psd.Layers.Add(endLayer);
                    }
                }

                psd.Layers.Add(layer);
            }

            if (beginSection != null)
            {
                psd.Layers.Add(beginSection);
            }

            psd.Layers.Add(psd.MakeImageLayer(
                GenerateMarkText("Generated by FreeMote, wdwxy12345@gmail.com ", width, 200), "FreeMote", 0, 0));
            return psd;
        }

        private Bitmap GenerateMarkText(string text, int width, int height, string fontName = "Arial", int fontSize = 28)
        {
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmp);
            Font drawFont = new Font(fontName, fontSize);
            StringFormat sf = StringFormat.GenericTypographic;
            SizeF labelBox = g.MeasureString(text, drawFont, new SizeF(bmp.Size), sf);
            RectangleF rectBounds = new Rectangle(0, 0, (int) labelBox.Width, (int) labelBox.Height);
            g.FillRectangle(Brushes.Transparent, rectBounds);
            g.DrawString(text, drawFont, Brushes.Black, 1f, 1f);
            g.Dispose();
            return bmp;
        }

        public byte[] Signature { get; } = {(byte) '8', (byte) 'B', (byte) 'P', (byte) 'S'};
    }
}