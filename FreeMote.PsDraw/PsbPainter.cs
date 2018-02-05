using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using FreeMote.Psb;

namespace FreeMote.PsDraw
{
    /// <summary>
    /// Emote PSB Painter
    /// </summary>
    public class PsbPainter
    {
        public PSB Source { get; set; }
        public List<ResourceMetadata> Resources{ get; private set; } = new List<ResourceMetadata>();

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

            Resources = new List<ResourceMetadata>();
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

            var drawRes = new List<ResourceMetadata>();
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
                g.DrawImage(res.ToImage(), new PointF(res.OriginX + width / 2f - res.Width / 2f, res.OriginY + height / 2f - res.Height / 2f));

            }
            //bmp.Save("renderKrkr.png", ImageFormat.Png);
            g.Dispose();
            return bmp;
        }

        /// <summary>
        /// Collect paintable resources
        /// </summary>
        private void CollectResource()
        {
            var resources = Source.CollectResources(); //TODO: Collect resource in Win

            foreach (var motion in (PsbDictionary)Source.Objects["object"].Children("all_parts").Children("motion"))
            {
                //Console.WriteLine($"Motion: {motion.Key}");
                var layerCol = motion.Value.Children("layer") as PsbCollection;
                foreach (var layer in layerCol)
                {
                    if (layer is PsbDictionary o)
                    {
                        Travel(o, null);
                    }
                }
            }

            Resources.Sort((md1, md2) => (int) ((md1.ZIndex - md2.ZIndex) * 100));

            //Travel
            void Travel(IPsbCollection collection, (float x, float y, float z)? nLocation)
            {
                if (collection is PsbDictionary dic)
                {
                    if (dic.ContainsKey("frameList") && dic["frameList"] is PsbCollection col)
                    {
                        //Collect Locations
                        var coordObj = col
                            .Where(o => o is PsbDictionary d && d.ContainsKey("content") &&
                                        d["content"] is PsbDictionary d2 && d2.ContainsKey("coord"))
                            .Select(v => v.Children("content").Children("coord"));
                        if (coordObj.Any())
                        {
                            var coord = coordObj.First() as PsbCollection;
                            var coordTuple = (x: (float)(PsbNumber)coord[0], y: (float)(PsbNumber)coord[1], z: (float)(PsbNumber)coord[2]);

                            if (nLocation == null)
                            {
                                //Console.WriteLine($"Set coord: {coordTuple.x},{coordTuple.y},{coordTuple.z}");
                                nLocation = coordTuple;
                            }
                            else
                            {
                                var loc = nLocation.Value;
                                nLocation = (loc.x + coordTuple.x, loc.y + coordTuple.y, loc.z + coordTuple.z);
                                //Console.WriteLine($"Update coord: {loc.x},{loc.y},{loc.z} -> {nLocation?.x},{nLocation?.y},{nLocation?.z}");
                            }
                        }

                        //Collect Parts
                        var srcObj = col
                            .Where(o => o is PsbDictionary d && d.ContainsKey("content") &&
                                                 d["content"] is PsbDictionary d2 && d2.ContainsKey("src"))
                            .Select(s => s.Children("content") as PsbDictionary);
                        if (srcObj.Any())
                        {
                            bool visible = true;
                            foreach (var obj in srcObj)
                            {
                                var s = obj["src"] as PsbString;
                                var opa = obj.ContainsKey("opa") ? (int)(PsbNumber)obj["opa"] : 10;
                                var icon = obj.ContainsKey("icon") ? obj["icon"].ToString() : null;
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
                                    if (nLocation != null && res != null && !Resources.Contains(res))
                                    {
                                        var location = nLocation.Value;
                                        //Console.WriteLine($"Locate {partName}/{iconName} at {location.x},{location.y},{location.z}");
                                        res.OriginX = location.x;
                                        res.OriginY = location.y;
                                        res.ZIndex = location.z;
                                        res.Opacity = opa;
                                        res.Visible = visible;
                                        Resources.Add(res);
                                        visible = false;
                                    }
                                }
                                else if (s.Value.StartsWith("motion/"))
                                {
                                    if (nLocation != null)
                                    {
                                        var ps = s.Value.Substring("motion/".Length)
                                            .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                                        Travel(
                                            (IPsbCollection)Source.Objects["object"].Children(ps[0]).Children("motion")
                                                .Children(ps[1]).Children("layer"), nLocation);
                                    }
                                }
                                //win
                                else if (!string.IsNullOrEmpty(icon) && ((PsbDictionary)Source.Objects["source"]).ContainsKey(s.Value))
                                {
                                    //src
                                    var res = resources.FirstOrDefault(resMd =>
                                        resMd.Part == s.Value && resMd.Name == icon);
                                    if (nLocation != null && res != null && !Resources.Contains(res))
                                    {
                                        var location = nLocation.Value;
                                        //Console.WriteLine($"Locate {partName}/{iconName} at {location.x},{location.y},{location.z}");
                                        res.OriginX = location.x;
                                        res.OriginY = location.y;
                                        res.ZIndex = location.z;
                                        res.Opacity = opa;
                                        res.Visible = visible;
                                        Resources.Add(res);
                                        visible = false;
                                    }
                                }
                                else if (!string.IsNullOrEmpty(icon) && ((PsbDictionary)Source.Objects["object"]).ContainsKey(s.Value))
                                {
                                    //motion
                                    if (nLocation != null)
                                    {
                                        Travel(
                                            (IPsbCollection)Source.Objects["object"].Children(s.Value).Children("motion")
                                                .Children(icon).Children("layer"), nLocation);
                                    }
                                }
                            }
                        }

                    }

                    if (dic.ContainsKey("children") && dic["children"] is PsbCollection ccol)
                    {
                        Travel(ccol, nLocation);
                    }
                    if (dic.ContainsKey("layer") && dic["layer"] is PsbCollection ccoll)
                    {
                        Travel(ccoll, nLocation);
                    }
                }
                else if (collection is PsbCollection ccol)
                {
                    foreach (var cc in ccol)
                    {
                        if (cc is IPsbCollection ccc)
                        {
                            Travel(ccc, nLocation);
                        }
                    }
                }
            }
        }
    }
}
