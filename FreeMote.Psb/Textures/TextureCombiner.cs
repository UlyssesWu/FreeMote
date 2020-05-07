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
        /// Combine Tachie texture parts 
        /// </summary>
        /// <param name="psb">Tachie (image) type PSB</param>
        /// <returns></returns>
        public static Dictionary<string, Bitmap> CombineTachie(PSB psb)
        {
            if (psb.Type != PsbType.Tachie)
            {
                throw new NotSupportedException("PSB is not image type");
            }

            Dictionary<string, Bitmap> images = new Dictionary<string, Bitmap>();
            PsbCollection imageList = psb.Objects["imageList"] as PsbCollection;
            if (imageList == null)
            {
                return images;
            }

            foreach (var psbValue in imageList)
            {
                var imageItem = psbValue as PsbDictionary;

                var texture = imageItem?["texture"] as PsbCollection;
                if (texture == null)
                {
                    continue;
                }

                var height = imageItem["height"].GetInt();
                var width = imageItem["width"].GetInt();
                var label = imageItem["label"].ToString();

                Bitmap img = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (var f = img.FastLock())
                {
                    foreach (var texObj in texture)
                    {
                        var tex = (PsbDictionary)texObj;
                        var md = PsbResCollector.GenerateMotionResMetadata(tex.Children("image") as PsbDictionary);
                        md.Spec = psb.Platform;

                        var left = tex["left"].GetInt();
                        var top = tex["top"].GetInt();
                        var tHeight = tex["height"].GetInt();
                        var tWidth = tex["width"].GetInt();

                        f.CopyRegion(md.ToImage(), new Rectangle(0,0, md.Width, md.Height), new Rectangle(left, top, tWidth, tHeight));
                    }
                }
                
                images.Add(label, img);
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
                kv.Value.Save(Path.Combine(dirPath, $"{kv.Key}.{extractFormat}"), extractFormat.ToImageFormat());
            }
        }

    }
}
