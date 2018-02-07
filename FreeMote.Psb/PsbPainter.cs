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
            var resources = Source.Platform == PsbSpec.krkr ? Source.CollectResources() : Source.CollectSpiltedResources();

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
            void Travel(IPsbCollection collection, (float x, float y, float z)? baseLocation, bool baseVisible = true)
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
                                                .Children(ps[1]).Children("layer"), baseLocation, suggestVisible);
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
                                                .Children(icon).Children("layer"), baseLocation, suggestVisible);
                                    }
                                }
                            }
                        }

                    }

                    if (dic.ContainsKey("children") && dic["children"] is PsbCollection ccol)
                    {
                        Travel(ccol, baseLocation, baseVisible);
                    }
                    if (dic.ContainsKey("layer") && dic["layer"] is PsbCollection ccoll)
                    {
                        Travel(ccoll, baseLocation, baseVisible);
                    }
                }
                else if (collection is PsbCollection ccol)
                {
                    foreach (var cc in ccol)
                    {
                        if (cc is IPsbCollection ccc)
                        {
                            Travel(ccc, baseLocation, baseVisible);
                        }
                    }
                }
            }
        }
    }
}
