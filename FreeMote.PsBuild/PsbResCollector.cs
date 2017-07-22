using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using FreeMote.Psb;

namespace FreeMote.PsBuild
{
    public static class PsbResCollector
    {
        public const string ResourceIdentifier = "#resource#";
        public const string ResourceKey = "pixel";
        public const string SourceKey = "source";

        /// <summary>
        /// Get all resources with necessary info
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="deDuplication">if true, We focus on Resource itself </param>
        /// <returns></returns>
        public static List<ResourceMetadata> CollectResources(this PSB psb, bool deDuplication = true)
        {
            List<ResourceMetadata> resourceList = psb.Resources == null ? new List<ResourceMetadata>() : new List<ResourceMetadata>(psb.Resources.Count);

            FindResources(resourceList, psb.Objects[SourceKey], deDuplication);

            resourceList.ForEach(r => r.Spec = psb.Platform);
            resourceList.Sort((md1, md2) => (int)(md1.Index - md2.Index));

            return resourceList;
        }

        private static void FindResources(List<ResourceMetadata> list, IPsbValue obj, bool deDuplication = true)
        {
            switch (obj)
            {
                case PsbCollection c:
                    c.Value.ForEach(o => FindResources(list, o, deDuplication));
                    break;
                case PsbDictionary d:
                    if (d[ResourceKey] is PsbResource r)
                    {
                        if (!deDuplication)
                        {
                            list.Add(GenerateResourceMetadata(d, r));
                        }
                        else if (r.Index == null || list.FirstOrDefault(md => md.Index == r.Index.Value) == null)
                        {
                            list.Add(GenerateResourceMetadata(d, r));
                        }
                    }
                    foreach (var o in d.Value.Values)
                    {
                        FindResources(list, o, deDuplication);
                    }
                    break;
            }
        }

        private static ResourceMetadata GenerateResourceMetadata(PsbDictionary d, PsbResource r)
        {
            bool is2D = false;
            var part = d.GetPartName();
            var name = d.GetName();
            RectangleF clip = RectangleF.Empty;

            if (d["clip"] is PsbDictionary clipDic && clipDic.Value.Count > 0)
            {
                is2D = true;
                clip = RectangleF.FromLTRB(
                    left: clipDic["left"] == null ? 0f : (float)(PsbNumber)clipDic["left"],
                    top: clipDic["top"] == null ? 0f : (float)(PsbNumber)clipDic["top"],
                    right: clipDic["right"] == null ? 1f : (float)(PsbNumber)clipDic["right"],
                    bottom: clipDic["bottom"] == null ? 1f : (float)(PsbNumber)clipDic["bottom"]
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
                width = (int)nw;
            }
            if (d["height"] is PsbNumber nh)
            {
                is2D = true;
                height = (int)nh;
            }
            if (d["originX"] is PsbNumber nx)
            {
                is2D = true;
                originX = (float)nx;
            }
            if (d["originY"] is PsbNumber ny)
            {
                is2D = true;
                originY = (float)ny;
            }
            string type = null;
            if (d["type"] is PsbString st)
            {
                type = st.Value;
            }
            int top = 0, left = 0;
            if (d["top"] is PsbNumber nt)
            {
                is2D = true;
                top = (int)nt;
            }
            if (d["left"] is PsbNumber nl)
            {
                is2D = true;
                left = (int)nl;
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
                Type = type,
                Resource = r,
            };
            return md;
        }

        /// <summary>
        /// Get related name on depth 3
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

        /// <summary>
        /// Get Name
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static string GetName(this IPsbChild c)
        {
            var source = c?.Parent as PsbDictionary;
            var result = source?.Value.FirstOrDefault(pair => Equals(pair.Value, c));
            return result?.Value == null ? null : result.Value.Key;
        }

        /// <summary>
        /// Get Name
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static string GetName(this IPsbSingleton c, PsbDictionary parent = null)
        {
            var source = parent ?? c?.Parents.FirstOrDefault(p => p is PsbDictionary) as PsbDictionary;
            var result = source?.Value.FirstOrDefault(pair => Equals(pair.Value, c));
            return result?.Value == null ? null : result.Value.Key;
        }

        /// <summary>
        /// If this spec uses RL
        /// </summary>
        /// <param name="spec"></param>
        /// <returns></returns>
        public static PsbCompressType CompressType(this PsbSpec spec)
        {
            switch (spec)
            {
                case PsbSpec.krkr:
                    return PsbCompressType.RL;
                case PsbSpec.common:
                case PsbSpec.win:
                case PsbSpec.other:
                default:
                    return PsbCompressType.None;
            }
        }

        /// <summary>
        /// Try to switch Spec
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="targetSpec"></param>
        public static void SwitchSpec(this PSB psb, PsbSpec targetSpec)
        {
            if (targetSpec == PsbSpec.other)
            {
                return;
            }
            var original = psb.Platform;
            psb.Platform = targetSpec;
            var resources = psb.CollectResources(false);

            if (original == PsbSpec.krkr && (targetSpec == PsbSpec.win || targetSpec == PsbSpec.common))
            {
                foreach (var resMd in resources)
                {
                    foreach (var parent in resMd.Resource.Parents)
                    {
                        var dic = parent as PsbDictionary;
                        dic?.Value.Remove("compress");
                    }
                }
            }

            if ((original == PsbSpec.win || original == PsbSpec.common) && targetSpec == PsbSpec.krkr)
            {
                //TODO:
            }
        }
    }
}
