using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using FastBitmapLib;

namespace FreeMote.Psb.Textures
{
    static class TextureCombiner
    {
        /// <summary>
        /// Combine Image texture parts 
        /// </summary>
        /// <param name="psb">Image (image) type PSB</param>
        /// <param name="hasPalette">Whether the source images are nBppIndexed images (which combined image cannot be converted back when repacking)</param>
        /// <returns></returns>
        public static Dictionary<string, (Bitmap CombinedImage, bool OriginHasPalette, bool CombinedWithPalette, List<ImageMetadata> Parts)> CombineTachie(PSB psb, out bool hasPalette)
        {
            hasPalette = false;
            if (psb.Type != PsbType.Tachie)
            {
                throw new NotSupportedException("PSB is not image type");
            }

            Dictionary<string, (Bitmap CombinedImage, bool HasPalette, bool CombinedWithPalette, List<ImageMetadata> Parts)> images = new();
            if (psb.Objects["imageList"] is not PsbList imageList)
            {
                return images;
            }

            foreach (var psbValue in imageList)
            {
                var imageItem = psbValue as PsbDictionary;

                if (imageItem?["texture"] is not PsbList texture)
                {
                    continue;
                }

                var height = imageItem["height"].GetInt();
                var width = imageItem["width"].GetInt();
                var label = imageItem["label"].ToString();
                bool currentHasPalette = false;
                bool currentCombinedWithPalette = false;
                PixelFormat indexedFormat = PixelFormat.Max;
                Bitmap img = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                List<ImageMetadata> parts = new List<ImageMetadata>(texture.Count);
                using (var f = img.FastLock())
                {
                    foreach (var texObj in texture)
                    {
                        var tex = (PsbDictionary)texObj;
                        var md = PsbResHelper.GenerateImageMetadata(tex.Children("image") as PsbDictionary);
                        md.Spec = psb.Platform;
                        md.PsbType = PsbType.Tachie;
                        md.Part = label;
                        parts.Add(md);

                        var left = tex["left"].GetInt();
                        var top = tex["top"].GetInt();
                        var tHeight = tex["height"].GetInt();
                        var tWidth = tex["width"].GetInt();

                        var image = md.ToImage();
                        if (((int)image.PixelFormat | (int)PixelFormat.Indexed) != 0)
                        {
                            hasPalette = true;
                            currentHasPalette = true;
                            if (indexedFormat != image.PixelFormat)
                            {
                                if (indexedFormat == PixelFormat.Undefined)
                                {
                                    //palette format conflict, there is nothing I can do
                                }
                                else if(indexedFormat == PixelFormat.Max)
                                {
                                    indexedFormat = image.PixelFormat;
                                }
                                else
                                {
                                    indexedFormat = PixelFormat.Undefined; //palette format conflict, there is nothing I can do
                                }
                            }
                            image = new Bitmap(image);
                        }
                        f.CopyRegion(image, new Rectangle(0, 0, md.Width, md.Height), new Rectangle(left, top, tWidth, tHeight));
                    }
                }


                //if (currentHasPalette) //Try to convert to indexed image
                //{
                //    if (indexedFormat != PixelFormat.Undefined && indexedFormat != PixelFormat.Max)
                //    {
                //        try
                //        {
                //            var indexedImg = img.Clone(new Rectangle(0, 0, img.Width, img.Height), indexedFormat); //Bad Quality
                //            img = indexedImg;
                //            currentCombinedWithPalette = true;
                //        }
                //        catch (Exception)
                //        {
                //            currentCombinedWithPalette = false;
                //        }
                //    }
                //}
                images.Add(label, (img, currentHasPalette, currentCombinedWithPalette, parts));
            }

            return images;
        }

        public static void CombineImagesToFile(string inputPath, PsbImageFormat extractFormat = PsbImageFormat.png)
        {
            if (!File.Exists(inputPath))
            {
                return;
            }

            var psb = new PSB(inputPath);
            if (psb.Type != PsbType.Tachie)
            {
                return;
            }

            var name = Path.GetFileNameWithoutExtension(inputPath);
            var dirPath = Path.Combine(Path.GetDirectoryName(inputPath), name);

            if (File.Exists(dirPath))
            {
                name += "-resources";
                dirPath += "-resources";
            }

            if (!Directory.Exists(dirPath)) //ensure there is no file with same name!
            {
                Directory.CreateDirectory(dirPath);
            }

            var bitmaps = CombineTachie(psb, out var hasPalette);
            foreach (var kv in bitmaps)
            {
                kv.Value.CombinedImage.Save(Path.Combine(dirPath, $"{kv.Key}.{extractFormat}"), extractFormat.ToImageFormat());
            }
        }

    }
}
