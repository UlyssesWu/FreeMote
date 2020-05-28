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
    public class PsbPainter
    {
        public string GroupMark { get; set; } = "■";
        public PSB Source { get; set; }
        public List<ImageMetadata> Resources { get; private set; } = new List<ImageMetadata>();

        public PsbPainter(PSB psb)
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
                throw new FormatException("PsbPainter only works for Motion(psb) models.");
            }

            Resources = new List<ImageMetadata>();
            CollectResource();
        }

        /// <summary>
        /// Render the model to an image
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public Bitmap Draw(int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            Graphics g = Graphics.FromImage(bmp);

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

            foreach (var res in drawRes)
            {
                Debug.WriteLine(
                    $"Drawing {res} at {res.OriginX},{res.OriginY} w:{res.Width},h:{res.Height}");
                //g.DrawImage(res.ToImage(), new PointF(res.OriginX + width / 2f, res.OriginY + height / 2f));
                if (res.Opacity >= 10)
                {
                    g.DrawImage(res.ToImage(), new PointF(res.OriginX + width / 2f - res.Width / 2f, res.OriginY + height / 2f - res.Height / 2f));
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
                    g.DrawImage(image, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, image.Width, image.Height,
                        GraphicsUnit.Pixel, attributes);
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
                if (Source.Objects["metadata"].Children("base").Children("chara") is PsbString chara && !string.IsNullOrEmpty(chara.Value))
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
                        if (coordObj.Any())
                        {
                            var coord = coordObj.First() as PsbList;
                            var coordTuple = (x: (float)(PsbNumber)coord[0], y: (float)(PsbNumber)coord[1], z: (float)(PsbNumber)coord[2]);

                            if (baseLocation == null)
                            {
                                //Console.WriteLine($"Set coord: {coordTuple.x},{coordTuple.y},{coordTuple.z}");
                                baseLocation = coordTuple;
                            }
                            else
                            {
                                var loc = baseLocation.Value;
                                baseLocation = (loc.x + coordTuple.x, loc.y + coordTuple.y, loc.z + coordTuple.z);
                                //Console.WriteLine($"Update coord: {loc.x},{loc.y},{loc.z} -> {baseLocation?.x},{baseLocation?.y},{baseLocation?.z}");
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
                                        res.OriginX = location.x;
                                        res.OriginY = location.y;
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
                                        res.OriginX = location.x;
                                        res.OriginY = location.y;
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
                                        Travel(
                                            (IPsbCollection)Source.Objects["object"].Children(s.Value).Children("motion")
                                                .Children(icon).Children("layer"), icon, baseLocation, suggestVisible);
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
