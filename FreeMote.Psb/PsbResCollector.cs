using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace FreeMote.Psb
{
    public static class PsbResCollector
    {
        /// <summary>
        /// The string with this prefix will be convert to number when compile/decompile
        /// </summary>
        public const string NumberStringPrefix = "#0x";

        /// <summary>
        /// The string with this prefix (with ID followed) will be convert to resource when compile/decompile
        /// </summary>
        public const string ResourceIdentifier = "#resource#";

        public const string ResourceKey = "pixel";
        public const string MotionSourceKey = "source";
        public const string MmoSourceKey = "sourceChildren";
        public const string MmoBgSourceKey = "bgChildren";
        public const string PimgSourceKey = "layers";
        public const string TachieSourceKey = "imageList";

        /// <summary>
        /// delimiter for output texture filename
        /// </summary>
        internal const string ResourceNameDelimiter = "-";

        /// <summary>
        /// Get all resources with necessary info
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="deDuplication">if true, we focus on Resource itself </param>
        /// <returns></returns>
        public static List<ResourceMetadata> CollectResources(this PSB psb, bool deDuplication = true)
        {
            List<ResourceMetadata> resourceList = psb.Resources == null
                ? new List<ResourceMetadata>()
                : new List<ResourceMetadata>(psb.Resources.Count);

            switch (psb.Type)
            {
                case PsbType.Tachie:
                    FindTachieResources(resourceList, psb.Objects[TachieSourceKey]);
                    break;
                case PsbType.Pimg:
                case PsbType.Scn:
                    resourceList.AddRange(psb.Objects.Where(k => k.Value is PsbResource).Select(k =>
                        new ResourceMetadata()
                        {
                            Name = k.Key,
                            Resource = k.Value as PsbResource,
                            Compress = k.Key.EndsWith(".tlg", true, null) ? PsbCompressType.Tlg : PsbCompressType.ByName
                        }));
                    FindPimgResources(resourceList, psb.Objects[PimgSourceKey], deDuplication);
                    break;
                case PsbType.Mmo:
                    FindMmoResources(resourceList, psb.Objects[MmoBgSourceKey], MmoBgSourceKey, deDuplication);
                    FindMmoResources(resourceList, psb.Objects[MmoSourceKey], MmoSourceKey, deDuplication);
                    break;
                case PsbType.Motion:
                default:
                    FindMotionResources(resourceList, psb.Objects[MotionSourceKey], deDuplication);
                    //Set Spec
                    resourceList.ForEach(r => r.Spec = psb.Platform);
                    break;
            }

            resourceList.Sort((md1, md2) => (int) (md1.Index - md2.Index));

            return resourceList;
        }

        private static void FindTachieResources(List<ResourceMetadata> list, IPsbValue obj, string currentLabel = "")
        {
            switch (obj)
            {
                case PsbCollection c:
                    c.ForEach(o => FindTachieResources(list, o));
                    break;
                case PsbDictionary d:
                    if (d["label"] is PsbString label)
                    {
                        if (string.IsNullOrWhiteSpace(currentLabel))
                        {
                            currentLabel = label;
                        }
                        else
                        {
                            currentLabel = string.Join("-", currentLabel, label);
                        }
                        
                    }
                    if (d[ResourceKey] is PsbResource r)
                    {
                        list.Add(GenerateTachieResMetadata(d, r, currentLabel));
                    }

                    foreach (var o in d.Values)
                    {
                        FindTachieResources(list, o, currentLabel);
                    }

                    break;
            }
        }

        private static ResourceMetadata GenerateTachieResMetadata(PsbDictionary d, PsbResource r, string label = "")
        {
            int width = 1, height = 1;
            int top = 0, left = 0;
            var dd = d.Parent as PsbDictionary?? d;
            if ((d["width"] ?? d["truncated_width"] ?? dd["width"]) is PsbNumber nw)
            {
                width = (int)nw;
            }

            if ((d["height"] ?? d["truncated_height"] ?? dd["height"]) is PsbNumber nh)
            {
                height = (int)nh;
            }

            if ((dd["top"] ?? d["top"]) is PsbNumber nx)
            {
                top = nx.AsInt;
            }

            if ((dd["left"] ?? d["left"]) is PsbNumber ny)
            {
                left = ny.AsInt;
            }

            var md = new ResourceMetadata()
            {
                Top = top,
                Left = left,
                TypeString = d["type"] as PsbString,
                Width = width,
                Height = height,
                Name = r.Index.ToString(),
                Part = label,
                Resource = r,
            };
            return md;
        }

        /// <summary>
        /// Add stub <see cref="PsbResource"/> to this PSB
        /// </summary>
        /// <param name="psb"></param>
        internal static List<PsbResource> MotionResourceInstrument(this PSB psb)
        {
            if (!psb.Objects.ContainsKey(MotionSourceKey))
            {
                return null;
            }
            var resources = new List<PsbResource>();
            GenerateMotionResourceStubs(resources, psb.Objects[MotionSourceKey]);
            return resources;
        }

        private static void FindMmoResources(List<ResourceMetadata> list, IPsbValue obj, in string defaultPartname = "",
            bool deDuplication = true)
        {
            switch (obj)
            {
                case PsbCollection c:
                    foreach (var o in c) FindMmoResources(list, o, defaultPartname, deDuplication);
                    break;
                case PsbDictionary d:
                    if (d[ResourceKey] is PsbResource r)
                    {
                        if (!deDuplication)
                        {
                            list.Add(GenerateMmoResMetadata(d, defaultPartname, r));
                        }
                        else if (r.Index == null || list.FirstOrDefault(md => md.Index == r.Index.Value) == null)
                        {
                            list.Add(GenerateMmoResMetadata(d, defaultPartname, r));
                        }
                    }

                    foreach (var o in d.Values)
                    {
                        FindMmoResources(list, o, defaultPartname, deDuplication);
                    }

                    break;
            }
        }

        private static ResourceMetadata GenerateMmoResMetadata(PsbDictionary d, string defaultPartName = "",
            PsbResource r = null)
        {
            if (r == null)
            {
                r = d.Values.FirstOrDefault(v => v is PsbResource) as PsbResource;
            }

            var dd = d.Parent.Parent as PsbDictionary ?? d;

            string name = "";
            string part = defaultPartName;
            if ((dd["label"]) is PsbString lbl)
            {
                name = lbl.Value;
            }

            //if (dd.Parent.Parent["className"] is PsbString className)
            //{
            //    part = className;
            //}

            bool is2D = false;
            var compress = PsbCompressType.None;
            if (d["compress"] is PsbString sc)
            {
                is2D = true;
                if (sc.Value.ToUpperInvariant() == "RL")
                {
                    compress = PsbCompressType.RL;
                }
            }

            int width = 1, height = 1;
            float originX = 0, originY = 0;
            if ((d["width"] ?? dd["width"]) is PsbNumber nw)
            {
                is2D = true;
                width = (int) nw;
            }

            if ((d["height"] ?? dd["height"]) is PsbNumber nh)
            {
                is2D = true;
                height = (int) nh;
            }

            if ((dd["originX"] ?? d["originX"]) is PsbNumber nx)
            {
                is2D = true;
                originX = (float) nx;
            }

            if ((dd["originY"] ?? d["originY"]) is PsbNumber ny)
            {
                is2D = true;
                originY = (float) ny;
            }

            var md = new ResourceMetadata()
            {
                Is2D = is2D,
                Compress = compress,
                OriginX = originX,
                OriginY = originY,
                Width = width,
                Height = height,
                Name = name,
                Part = part,
                Resource = r,
            };
            return md;
        }

        private static void FindPimgResources(List<ResourceMetadata> list, IPsbValue obj, bool deDuplication = true)
        {
            if (obj is PsbCollection c)
            {
                foreach (var o in c)
                {
                    if (!(o is PsbDictionary dic)) continue;
                    if (dic["layer_id"] is PsbString layerId)
                    {
                        var res = list.FirstOrDefault(k => k.Name.StartsWith(layerId.Value, true, null));
                        if (res == null)
                        {
                            continue;
                        }

                        if (uint.TryParse(layerId.Value, out var id))
                        {
                            res.Index = id;
                        }

                        if (dic["width"] is PsbNumber nw)
                        {
                            res.Width = deDuplication ? Math.Max((int) nw, res.Width) : (int) nw;
                        }

                        if (dic["height"] is PsbNumber nh)
                        {
                            res.Height = deDuplication ? Math.Max((int) nh, res.Height) : (int) nh;
                        }
                    }
                }
            }
        }

        private static void FindMotionResources(List<ResourceMetadata> list, IPsbValue obj, bool deDuplication = true)
        {
            switch (obj)
            {
                case PsbCollection c:
                    c.ForEach(o => FindMotionResources(list, o, deDuplication));
                    break;
                case PsbDictionary d:
                    if (d[ResourceKey] is PsbResource r)
                    {
                        if (!deDuplication)
                        {
                            list.Add(GenerateMotionResMetadata(d, r));
                        }
                        else if (r.Index == null || list.FirstOrDefault(md => md.Index == r.Index.Value) == null)
                        {
                            list.Add(GenerateMotionResMetadata(d, r));
                        }
                    }

                    foreach (var o in d.Values)
                    {
                        FindMotionResources(list, o, deDuplication);
                    }

                    break;
            }
        }

        /// <summary>
        /// Add stubs (<see cref="PsbResource"/> with null Data) into a Motion PSB. A stub must be linked with a texture, or it will be null after <see cref="PSB.Build"/>
        /// </summary>
        /// <param name="resources"></param>
        /// <param name="obj"></param>
        private static void GenerateMotionResourceStubs(List<PsbResource> resources, IPsbValue obj)
        {
            switch (obj)
            {
                case PsbCollection c:
                    c.ForEach(o => GenerateMotionResourceStubs(resources, o));
                    break;
                case PsbDictionary d:
                    if (d.ContainsKey(ResourceKey) && (d[ResourceKey] == null || d[ResourceKey] is PsbNull))
                    {
                        if (d.ContainsKey("width") && d.ContainsKey("height"))
                        {
                            //confirmed, add stub
                            PsbResource res = new PsbResource();
                            resources.Add(res);
                            res.Index = (uint) resources.IndexOf(res);
                            d[ResourceKey] = res;
                        }
                    }

                    foreach (var o in d.Values)
                    {
                        GenerateMotionResourceStubs(resources, o);
                    }

                    break;
            }
        }

        /// <summary>
        /// Extract resource info
        /// </summary>
        /// <param name="d">PsbObject which contains "pixel"</param>
        /// <param name="r">Resource</param>
        /// <returns></returns>
        internal static ResourceMetadata GenerateMotionResMetadata(PsbDictionary d, PsbResource r = null)
        {
            if (r == null)
            {
                r = d.Values.FirstOrDefault(v => v is PsbResource) as PsbResource;
            }

            bool is2D = false;
            var part = d.GetPartName();
            var name = d.GetName();
            RectangleF clip = RectangleF.Empty;

            if (d["clip"] is PsbDictionary clipDic && clipDic.Count > 0)
            {
                is2D = true;
                clip = RectangleF.FromLTRB(
                    left: clipDic["left"] == null ? 0f : (float) (PsbNumber) clipDic["left"],
                    top: clipDic["top"] == null ? 0f : (float) (PsbNumber) clipDic["top"],
                    right: clipDic["right"] == null ? 1f : (float) (PsbNumber) clipDic["right"],
                    bottom: clipDic["bottom"] == null ? 1f : (float) (PsbNumber) clipDic["bottom"]
                );
            }

            var compress = PsbCompressType.None;
            if (d["compress"] is PsbString sc)
            {
                is2D = true;
                if (sc.Value.ToUpperInvariant() == "RL")
                {
                    compress = PsbCompressType.RL;
                }
            }

            int width = 1, height = 1;
            float originX = 0, originY = 0;
            if (d["width"] is PsbNumber nw)
            {
                is2D = true;
                width = (int) nw;
            }

            if (d["height"] is PsbNumber nh)
            {
                is2D = true;
                height = (int) nh;
            }

            if (d["originX"] is PsbNumber nx)
            {
                is2D = true;
                originX = (float) nx;
            }

            if (d["originY"] is PsbNumber ny)
            {
                is2D = true;
                originY = (float) ny;
            }

            PsbString typeString = null;
            if (d["type"] is PsbString typeStr)
            {
                typeString = typeStr;
            }

            int top = 0, left = 0;
            if (d["top"] is PsbNumber nt)
            {
                is2D = true;
                top = (int) nt;
            }

            if (d["left"] is PsbNumber nl)
            {
                is2D = true;
                left = (int) nl;
            }

            var md = new ResourceMetadata()
            {
                Index = r.Index ?? int.MaxValue,
                Compress = compress,
                Name = name,
                Part = part,
                Clip = clip,
                Is2D = is2D,
                OriginX = originX,
                OriginY = originY,
                Top = top,
                Left = left,
                Width = width,
                Height = height,
                TypeString = typeString,
                Resource = r,
            };
            return md;
        }

        /// <summary>
        /// Get related name on depth 3 (not a common method)
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        private static string GetPartName(this IPsbChild c)
        {
            while (c != null)
            {
                if (c.Parent?.Parent?.Parent == null)
                {
                    return c.GetName();
                }

                c = c.Parent;
            }

            return null;
        }
    }
}