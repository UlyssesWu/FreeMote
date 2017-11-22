#define USE_FASTBITMAP
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using FreeMote.Psb;

#if USE_FASTBITMAP
using FastBitmapLib;
#else
using System.Drawing.Drawing2D;
#endif

namespace FreeMote.PsBuild.Textures
{
    public static class TextureSpliter
    {
        public static Dictionary<string, Bitmap> SplitTexture(PsbDictionary tex, PsbSpec spec)
        {
            var icon = (PsbDictionary)tex["icon"];
            var texture = (PsbDictionary)tex["texture"];

            //var mipmap = (PsbDictionary)texture["mipMap"]; //TODO: Mipmap?
            Dictionary<string, Bitmap> textures = new Dictionary<string, Bitmap>(icon.Count);

            var md = PsbResCollector.GenerateResourceMetadata(texture, (PsbResource)texture["pixel"]);
            md.Spec = spec; //Important
            Bitmap bmp = md.ToImage();
            foreach (var iconPair in icon)
            {
                var info = (PsbDictionary)iconPair.Value;
                var width = (int)(PsbNumber)info["width"];
                var height = (int)(PsbNumber)info["height"];
                var top = (int)(PsbNumber)info["top"];
                var left = (int)(PsbNumber)info["left"];
                Bitmap b = new Bitmap(width, height, PixelFormat.Format32bppArgb);
#if USE_FASTBITMAP
                using (FastBitmap f = b.FastLock())
                {
                    f.CopyRegion(bmp, new Rectangle(left, top, width, height), new Rectangle(0, 0, b.Width, b.Height));
                }
#else
                    Graphics g = Graphics.FromImage(b);
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(bmp, new Rectangle(0, 0, b.Width, b.Height), new Rectangle(left, top, width, height),
                        GraphicsUnit.Pixel);
                    g.Dispose();
#endif
                textures.Add(iconPair.Key, b);
            }
            bmp.Dispose();
            return textures;
        }

        /// <summary>
        /// Split textures into parts and save to files
        /// </summary>
        /// <param name="psb">PSB</param>
        /// <param name="path">Save directory</param>
        /// <param name="option">Save option</param>
        /// <param name="imageFormat">Save format</param>
        /// <param name="pixelFormat">When save to PSB special formats, specific pixel format to use</param>
        public static void SplitTextureToFiles(this PSB psb, string path, PsbImageOption option = PsbImageOption.Extract, PsbImageFormat imageFormat = PsbImageFormat.Png, PsbPixelFormat pixelFormat = PsbPixelFormat.None)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            var source = (PsbDictionary)psb.Objects["source"];
            foreach (var texPair in source)
            {
                if (!(texPair.Value is PsbDictionary tex))
                {
                    continue;
                }
                var name = texPair.Key;
                if (!Directory.Exists(Path.Combine(path, name)))
                {
                    Directory.CreateDirectory(Path.Combine(path, name));
                }
                var icon = (PsbDictionary)tex["icon"];
                var texture = (PsbDictionary)tex["texture"];

                //var mipmap = (PsbDictionary)texture["mipMap"]; //TODO: Mipmap?

                var md = PsbResCollector.GenerateResourceMetadata(texture, (PsbResource)texture["pixel"]);
                md.Spec = psb.Platform; //Important
                Bitmap bmp = md.ToImage();

                foreach (var iconPair in icon)
                {
                    var savePath = Path.Combine(path, name, iconPair.Key);
                    var info = (PsbDictionary)iconPair.Value;
                    var width = (int)(PsbNumber)info["width"];
                    var height = (int)(PsbNumber)info["height"];
                    var top = (int)(PsbNumber)info["top"];
                    var left = (int)(PsbNumber)info["left"];
                    var attr = (int)(PsbNumber)info["attr"];
                    Bitmap b = new Bitmap(width, height, PixelFormat.Format32bppArgb);
#if USE_FASTBITMAP
                    using (FastBitmap f = b.FastLock())
                    {
                        f.CopyRegion(bmp, new Rectangle(left, top, width, height), new Rectangle(0, 0, b.Width, b.Height));
                    }
#else
                    Graphics g = Graphics.FromImage(b);
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(bmp, new Rectangle(0, 0, b.Width, b.Height), new Rectangle(left, top, width, height),
                        GraphicsUnit.Pixel);
                    g.Dispose();
#endif

                    switch (option)
                    {
                        case PsbImageOption.Raw:
                            File.WriteAllBytes(savePath + ".raw", RL.GetPixelBytesFromImage(b, pixelFormat));
                            break;
                        case PsbImageOption.Compress:
                            File.WriteAllBytes(savePath + ".rl", RL.CompressImage(b, pixelFormat));
                            break;
                        case PsbImageOption.Original:
                        case PsbImageOption.Extract:
                        default:
                            switch (imageFormat)
                            {
                                case PsbImageFormat.Bmp:
                                    b.Save(savePath + ".bmp", ImageFormat.Bmp);
                                    break;
                                case PsbImageFormat.Png:
                                default:
                                    b.Save(savePath + ".png", ImageFormat.Png);
                                    //b.Save(savePath + $"_{attr}.png", ImageFormat.Png);
                                    break;
                            }
                            break;
                    }
                }
                bmp.Dispose();
            }
        }
    }
}



