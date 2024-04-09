using FreeMote.Psb.Textures;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace FreeMote.Psb
{
    //This painter only paints the initial state of a motion. The accuracy is not guaranteed.

    /// <summary>
    /// Motion PSB Painter
    /// </summary>
    public class MtnPainter
    {
        public PSB Source { get; set; }

        private readonly List<ImageMetadata> _allResources;

        public string BaseMotion { get; private set; }

        public MtnPainter(PSB psb)
        {
            Source = psb;
            _allResources = Source.Platform == PsbSpec.krkr ? Source.CollectResources<ImageMetadata>().ToList() : Source.CollectSplitResources();
            var motionName = (Source.Objects["object"] as PsbDictionary)?.Keys.FirstOrDefault();

            if (string.IsNullOrEmpty(motionName))
            {
                throw new Exception("cannot find base motion object");
            }

            BaseMotion = motionName;
        }

        /// <summary>
        /// Draw all sub-motions with auto size
        /// </summary>
        /// <returns></returns>
        public IEnumerable<(string Name, Bitmap Image)> DrawAll()
        {
            var subMotions = GetSubMotionNames();
            foreach (var subMotion in subMotions)
            {
                var img = Draw(subMotion);
                if (img == null)
                {
                    continue;
                }
                yield return (subMotion, img);
            }
        }

        /// <summary>
        /// Draw a sub-motion, set both <paramref name="width"/> and <paramref name="height"/> to 0 to use auto size
        /// </summary>
        /// <param name="subMotion"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public Bitmap Draw(string subMotion, int width = 0, int height = 0)
        {
            bool autoSize = width <= 0 && height <= 0;
            var resources = CollectResource(subMotion);

            var drawRes = new List<ImageMetadata>();
            foreach (var res in resources)
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

            if (drawRes.Count == 0)
            {
                return null;
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

            Debug.WriteLine(
                $"Drawing {subMotion} ({width} x {height})");

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
                    matrix.Matrix33 = res.Opacity / 10.0f;

                    //create image attributes  
                    ImageAttributes attributes = new ImageAttributes();

                    //set the color(opacity) of the image  
                    attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                    //now draw the image  
                    var image = res.ToImage();
                    g.DrawImage(image, new Rectangle((int)Math.Ceiling(x), (int)Math.Ceiling(y), image.Width, image.Height), 0, 0, image.Width, image.Height,
                        GraphicsUnit.Pixel, attributes); //TODO: the offset is rounded!
                }
            }
            //bmp.Save("renderKrkr.png", ImageFormat.png);
            g.Dispose();
            return bmp;
        }

        public List<string> GetSubMotionNames()
        {
            var list = new List<string>();
            var subMotionDic = Source.Objects["object"].Children(BaseMotion).Children("motion") as PsbDictionary;
            if (subMotionDic == null)
            {
                return list;
            }

            list.AddRange(subMotionDic.Keys);
            return list;
        }

        /// <summary>
        /// Collect paint-able resources
        /// </summary>
        private List<ImageMetadata> CollectResource(string subMotion)
        {
            var result = new List<ImageMetadata>();

            //Console.WriteLine($"Motion: {motion.Key}");
            var layerCol = Source.Objects["object"].Children(BaseMotion).Children("motion").Children(subMotion).Children("layer") as PsbList;
            foreach (var layer in layerCol)
            {
                if (layer is PsbDictionary o)
                {
                    Travel(o, subMotion, null);
                }
            }
            
            result = result.OrderBy(metadata => metadata.ZIndex).ToList();
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
                            var coordTuple = (x: (float) (PsbNumber) coord[0], y: (float) (PsbNumber) coord[1], z: (float) (PsbNumber) coord[2]);

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
                                var content = (PsbDictionary) obj["content"];
                                var s = content["src"] as PsbString;
                                var opa = content.ContainsKey("opa") ? (int) (PsbNumber) content["opa"] : 10;
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
                                    var res = _allResources.FirstOrDefault(resMd =>
                                        resMd.Part == partName && resMd.Name == iconName);
                                    if (baseLocation != null && res != null)
                                    {
                                        //copy from res
                                        var nRes = res.Clone();
                                        var location = baseLocation.Value;
                                        nRes.Label = labelName;
                                        nRes.MotionName = motionName;
                                        //Console.WriteLine($"Locate {partName}/{iconName} at {location.x},{location.y},{location.z}");
                                        nRes.OriginX = location.x - ox;
                                        nRes.OriginY = location.y - oy;
                                        nRes.ZIndex = location.z;
                                        nRes.Opacity = opa;
                                        nRes.Visible = time <= 0 && visible;
                                        result.Add(nRes);
                                    }
                                }
                                else if (s.Value.StartsWith("motion/"))
                                {
                                    if (baseLocation != null)
                                    {
                                        var ps = s.Value.Substring("motion/".Length)
                                            .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                                        Travel(
                                            (IPsbCollection) Source.Objects["object"].Children(ps[0]).Children("motion")
                                                .Children(ps[1]).Children("layer"), ps[1], baseLocation, suggestVisible);
                                    }
                                }
                                //win
                                else if (!string.IsNullOrEmpty(icon) && ((PsbDictionary) Source.Objects["source"]).ContainsKey(s.Value))
                                {
                                    //src
                                    var res = _allResources.FirstOrDefault(resMd =>
                                        resMd.Part == s.Value && resMd.Name == icon);
                                    if (baseLocation != null && res != null)
                                    {
                                        //copy from res
                                        var nRes = res.Clone();
                                        var location = baseLocation.Value;
                                        nRes.Label = labelName;
                                        nRes.MotionName = motionName;
                                        //Console.WriteLine($"Locate {partName}/{iconName} at {location.x},{location.y},{location.z}");
                                        nRes.OriginX = location.x + ox;
                                        nRes.OriginY = location.y + oy;
                                        nRes.ZIndex = location.z;
                                        nRes.Opacity = opa;
                                        nRes.Visible = time <= 0 && visible;
                                        result.Add(nRes);
                                    }
                                }
                                else if (!string.IsNullOrEmpty(icon) && ((PsbDictionary) Source.Objects["object"]).ContainsKey(s.Value))
                                {
                                    //motion
                                    if (baseLocation != null)
                                    {
                                        Travel(
                                            (IPsbCollection) Source.Objects["object"].Children(s.Value).Children("motion")
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

            return result;
        }
        
    }
}
