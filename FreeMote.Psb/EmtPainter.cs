using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using FreeMote.Psb.Textures;

namespace FreeMote.Psb
{
    /// <summary>
    /// EMT PSB Painter
    /// </summary>
    public class EmtPainter
    {
        public string GroupMark { get; set; } = "■";
        public PSB Source { get; set; }
        public List<ImageMetadata> Resources { get; private set; } = new List<ImageMetadata>();

        public EmtPainter(PSB psb)
        {
            Source = psb;
            UpdateResource();
        }

        /// <summary>
        /// Gather resources for painting
        /// </summary>
        public void UpdateResource()
        {
            if (Source.InferType() != PsbType.Motion)
            {
                throw new FormatException("EmtPainter only works for Motion PSB models.");
            }

            Resources = new List<ImageMetadata>();
            CollectResource();
        }

        public (int Width, int Height, float OffsetX, float OffsetY) TryGetCanvasSize()
        {
            var drawRes = new List<ImageMetadata>();
            float xOffset = 0;
            float yOffset = 0;
            foreach (var res in Resources)
            {
                if (res.Opacity <= 0 || !res.Visible)
                {
                    continue;
                }

                //if (res.Name.StartsWith("icon") && res.Name != "icon1")
                //{
                //    continue;
                //}
                drawRes.Add(res);
            }

            var minX = drawRes.Min(md => md.OriginX - md.Width / 2f);
            var maxX = drawRes.Max(md => md.OriginX + md.Width / 2f);
            var minY = drawRes.Min(md => md.OriginY - md.Height / 2f);
            var maxY = drawRes.Max(md => md.OriginY + md.Height / 2f);

            xOffset = -(minX + maxX) / 2f; //to ensure there is no minus location when drawing
            yOffset = -(minY + maxY) / 2f;

            //Debug.WriteLine($"{minX}, {minX}, {minY}, {maxY}, {xOffset}, {yOffset}");

            var width = (int) Math.Ceiling(maxX - minX);
            var height = (int) Math.Ceiling(maxY - minY);

            return (width, height, xOffset, yOffset);
        }

        /// <summary>
        /// Render the model to an image
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public Bitmap Draw(int width, int height)
        {
            bool autoSize = width <= 0 && height <= 0;

            var drawRes = new List<ImageMetadata>();
            foreach (var res in Resources)
            {
                if (res.Opacity <= 0 || !res.Visible)
                {
                    continue;
                }

                if (res.Name.StartsWith("icon") && res.Name != "icon1")
                {
                    continue;
                }
                drawRes.Add(res);
            }

            Bitmap bmp;
            float xOffset = 0;
            float yOffset = 0;
            if (autoSize)
            {
                var minX = drawRes.Min(md => md.OriginX - md.Width / 2f);
                var maxX = drawRes.Max(md => md.OriginX + md.Width / 2f);
                var minY = drawRes.Min(md => md.OriginY - md.Height / 2f);
                var maxY = drawRes.Max(md => md.OriginY + md.Height / 2f);

                xOffset = -(minX + maxX) / 2f; //to ensure there is no minus location when drawing
                yOffset = -(minY + maxY) / 2f;

                //Debug.WriteLine($"{minX}, {minX}, {minY}, {maxY}, {xOffset}, {yOffset}");

                width = (int) Math.Ceiling(maxX - minX);
                height = (int) Math.Ceiling(maxY - minY);
                bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            }
            else
            {
                bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            }
            
            Graphics g = Graphics.FromImage(bmp);

            foreach (var res in drawRes)
            {
                //point: represents the upper-left corner of the drawn image. Graphics (0,0) is top-left
                var x = res.OriginX + width / 2f - res.Width / 2f + xOffset;
                var y = res.OriginY + height / 2f - res.Height / 2f + yOffset;

                Debug.WriteLine(
                    $"Drawing {res} at {x},{y} ({res.OriginX},{res.OriginY}) w:{res.Width},h:{res.Height}");
                //g.DrawImage(res.ToImage(), new PointF(res.OriginX + width / 2f, res.OriginY + height / 2f));

                if (res.Opacity >= 10)
                {
                    g.DrawImage(res.ToImage(), new PointF(x, y));
                }
                else
                {
                    //https://stackoverflow.com/a/4779371
                    //create a color matrix object  
                    ColorMatrix matrix = new ColorMatrix();

                    //set the opacity  
                    matrix.Matrix33 = res.Opacity /10.0f;

                    //create image attributes  
                    ImageAttributes attributes = new ImageAttributes();

                    //set the color(opacity) of the image  
                    attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                    //now draw the image  
                    var image = res.ToImage();
                    g.DrawImage(image, new Rectangle((int) Math.Ceiling(x), (int) Math.Ceiling(y), image.Width, image.Height), 0, 0, image.Width, image.Height,
                        GraphicsUnit.Pixel, attributes); //TODO: the offset is rounded!
                    //g.DrawImage(image, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, image.Width, image.Height,
                    //    GraphicsUnit.Pixel, attributes);
                }
            }
            //bmp.Save("renderKrkr.png", ImageFormat.png);
            g.Dispose();
            return bmp;
        }

        /// <summary>
        /// Collect paint-able resources
        /// </summary>
        private void CollectResource()
        {
            //TODO: selectorControl
            var resources = Source.Platform == PsbSpec.krkr ? Source.CollectResources<ImageMetadata>().ToList() : Source.CollectSplitResources();
            //get base chara (in case of logo)
            var basePart = "all_parts";
            try
            {
                if (Source.Objects["metadata"] is PsbNull)
                {
                    var basePart2 = (Source.Objects["object"] as PsbDictionary)?.Keys.FirstOrDefault();
                    if (!string.IsNullOrEmpty(basePart2))
                    {
                        basePart = basePart2;
                    }
                }
                else if (Source.Objects["metadata"].Children("base").Children("chara") is PsbString chara && !string.IsNullOrEmpty(chara.Value))
                {
                    basePart = chara;
                }
            }
            catch
            {
                //ignore
            }
            foreach (var motion in (PsbDictionary)Source.Objects["object"].Children(basePart).Children("motion"))
            {
                //Console.WriteLine($"Motion: {motion.Key}");
                var layerCol = motion.Value.Children("layer") as PsbList;
                foreach (var layer in layerCol)
                {
                    if (layer is PsbDictionary o)
                    {
                        Travel(o, motion.Key, null);
                    }
                }
            }

            Resources = Resources.OrderBy(metadata => metadata.ZIndex).ToList();
            //TODO: mesh\bp&cc
            //Travel
            void Travel(IPsbCollection collection, string motionName, (float x, float y, float z)? baseLocation, bool baseVisible = true)
            {
                if (collection is PsbDictionary dic)
                {
                    if (dic.ContainsKey("frameList") && dic["frameList"] is PsbList col)
                    {
                        var labelName = dic["label"].ToString(); //part label, e.g. "涙R"
                        //Collect Locations
                        var coordObj = col
                            .Where(o => o is PsbDictionary d && d.ContainsKey("content") &&
                                        d["content"] is PsbDictionary d2 && d2.ContainsKey("coord"))
                            .Select(v => v.Children("content").Children("coord"));

                        float ox = 0, oy = 0;
                        if (coordObj.Any())
                        {
                            var coord = coordObj.First() as PsbList;
                            var coordTuple = (x: (float)(PsbNumber)coord[0], y: (float)(PsbNumber)coord[1], z: (float)(PsbNumber)coord[2]);

                            if (coord.Parent is PsbDictionary content)
                            {
                                if (content.ContainsKey("ox"))
                                {
                                    ox = content["ox"].GetFloat();
                                }

                                if (content.ContainsKey("oy"))
                                {
                                    oy = content["oy"].GetFloat();
                                }
                            }

                            if (baseLocation == null)
                            {
                                Debug.WriteLine($"Set coord: {coordTuple.x},{coordTuple.y},{coordTuple.z} | {dic.Path}");
                                baseLocation = coordTuple;
                            }
                            else
                            {
                                var loc = baseLocation.Value;
                                baseLocation = (loc.x + coordTuple.x, loc.y + coordTuple.y, loc.z + coordTuple.z);
                                Debug.WriteLine($"Update coord: {loc.x},{loc.y},{loc.z} + {coordTuple.x},{coordTuple.y},{coordTuple.z} -> {baseLocation?.x},{baseLocation?.y},{baseLocation?.z} | {dic.Path}");
                            }
                        }

                        //Collect Parts
                        var srcObj = col
                            .Where(o => o is PsbDictionary d && d.ContainsKey("content") &&
                                                 d["content"] is PsbDictionary d2 && d2.ContainsKey("src"))
                            .Select(s => s as PsbDictionary);
                        if (srcObj.Any())
                        {
                            bool visible = baseVisible;
                            foreach (var obj in srcObj)
                            {
                                var content = (PsbDictionary)obj["content"];
                                var s = content["src"] as PsbString;
                                var opa = content.ContainsKey("opa") ? (int)(PsbNumber)content["opa"] : 10;
                                var icon = content.ContainsKey("icon") ? content["icon"].ToString() : null;
                                int time = obj["time"] is PsbNumber n ? n.IntValue : 0;
                                bool suggestVisible = baseVisible && time <= 0 && opa > 0;
                                if (s == null || string.IsNullOrEmpty(s.Value))
                                {
                                    continue;
                                }
                                //Krkr
                                if (s.Value.StartsWith("src/"))
                                {
                                    //var iconName = dic["icon"].ToString();
                                    var iconName = s.Value.Substring(s.Value.LastIndexOf('/') + 1);
                                    var partName = new string(s.Value.SkipWhile(c => c != '/').Skip(1).TakeWhile(c => c != '/')
                                        .ToArray());
                                    var res = resources.FirstOrDefault(resMd =>
                                        resMd.Part == partName && resMd.Name == iconName);
                                    if (baseLocation != null && res != null && !Resources.Contains(res))
                                    {
                                        var location = baseLocation.Value;
                                        res.Label = labelName;
                                        res.MotionName = motionName;
                                        //Console.WriteLine($"Locate {partName}/{iconName} at {location.x},{location.y},{location.z}");
                                        res.OriginX = location.x + ox;
                                        res.OriginY = location.y + oy;
                                        res.ZIndex = location.z;
                                        res.Opacity = opa;
                                        res.Visible = time <= 0 && visible;
                                        Resources.Add(res);
                                    }
                                }
                                else if (s.Value.StartsWith("motion/"))
                                {
                                    if (baseLocation != null)
                                    {
                                        var ps = s.Value.Substring("motion/".Length)
                                            .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                                        Travel(
                                            (IPsbCollection)Source.Objects["object"].Children(ps[0]).Children("motion")
                                                .Children(ps[1]).Children("layer"), ps[1], baseLocation, suggestVisible);
                                    }
                                }
                                //win
                                else if (!string.IsNullOrEmpty(icon) && ((PsbDictionary)Source.Objects["source"]).ContainsKey(s.Value))
                                {
                                    //src
                                    var res = resources.FirstOrDefault(resMd =>
                                        resMd.Part == s.Value && resMd.Name == icon);
                                    if (baseLocation != null && res != null && !Resources.Contains(res))
                                    {
                                        var location = baseLocation.Value;
                                        res.Label = labelName;
                                        res.MotionName = motionName;
                                        //Console.WriteLine($"Locate {partName}/{iconName} at {location.x},{location.y},{location.z}");
                                        res.OriginX = location.x + ox;
                                        res.OriginY = location.y + oy;
                                        res.ZIndex = location.z;
                                        res.Opacity = opa;
                                        res.Visible = time <= 0 && visible;
                                        Resources.Add(res);
                                    }
                                }
                                else if (!string.IsNullOrEmpty(icon) && ((PsbDictionary)Source.Objects["object"]).ContainsKey(s.Value))
                                {
                                    //motion
                                    if (baseLocation != null)
                                    {
                                        var motion = Source.Objects["object"].Children(s.Value).Children("motion");
                                        if (motion is PsbDictionary motionDic && motionDic.TryGetValue(icon, out var iconDic))
                                        {
                                            //motion can be empty dic if it is a Partial exported PSB
                                            Travel((IPsbCollection)iconDic.Children("layer"), icon, baseLocation, suggestVisible);
                                        }
                                    }
                                }
                            }
                        }

                    }

                    if (dic.ContainsKey("children") && dic["children"] is PsbList ccol)
                    {
                        Travel(ccol, motionName, baseLocation, baseVisible);
                    }
                    if (dic.ContainsKey("layer") && dic["layer"] is PsbList ccoll)
                    {
                        Travel(ccoll, motionName, baseLocation, baseVisible);
                    }
                }
                else if (collection is PsbList ccol)
                {
                    foreach (var cc in ccol)
                    {
                        if (cc is IPsbCollection ccc)
                        {
                            Travel(ccc, motionName, baseLocation, baseVisible);
                        }
                    }
                }
            }
        }
    }
}
