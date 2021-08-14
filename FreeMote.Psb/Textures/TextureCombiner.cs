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
        /// <returns></returns>
        public static Dictionary<string, (Bitmap CombinedImage, List<ImageMetadata> Parts)> CombineTachie(PSB psb)
        {
            if (psb.Type != PsbType.Tachie)
            {
                throw new NotSupportedException("PSB is not image type");
            }

            Dictionary<string, (Bitmap CombinedImage, List<ImageMetadata> Parts)> images = new();
            if (psb.Objects["imageList"] is not PsbList imageList)
            {
                return images;
            }

            foreach (var psbValue in imageList)
            {
                var imageItem = psbValue as PsbDictionary;

                var texture = imageItem?["texture"] as PsbList;
                if (texture == null)
                {
                    continue;
                }

                var height = imageItem["height"].GetInt();
                var width = imageItem["width"].GetInt();
                var label = imageItem["label"].ToString();

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

                        f.CopyRegion(md.ToImage(), new Rectangle(0, 0, md.Width, md.Height), new Rectangle(left, top, tWidth, tHeight));
                    }
                }

                images.Add(label, (img, parts));
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

            var bitmaps = CombineTachie(psb);
            foreach (var kv in bitmaps)
            {
                kv.Value.CombinedImage.Save(Path.Combine(dirPath, $"{kv.Key}.{extractFormat}"), extractFormat.ToImageFormat());
            }
        }

    }
}
