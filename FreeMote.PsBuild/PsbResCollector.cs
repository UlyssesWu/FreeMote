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
        /// <returns></returns>
        public static List<ResourceMetadata> CollectResources(this PSB psb)
        {
            List<ResourceMetadata> resourceList = new List<ResourceMetadata>(psb.Resources.Count);

            FindResources(resourceList, psb.Objects[SourceKey]);

            resourceList.ForEach(r => r.Spec = psb.Platform);

            return resourceList;
        }

        private static void FindResources(List<ResourceMetadata> list, IPsbValue obj)
        {
            switch (obj)
            {
                case PsbCollection c:
                    c.Value.ForEach(o => FindResources(list, o));
                    break;
                case PsbDictionary d:
                    if (d[ResourceKey] is PsbResource r)
                    {
                        list.Add(GenerateResourceMetadata(d, r));
                    }
                    foreach (var o in d.Value.Values)
                    {
                        FindResources(list, o);
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
                    left: (float?)(clipDic["left"] as PsbNumber)?.Value ?? 0,
                    top: (float?)(clipDic["top"] as PsbNumber)?.Value ?? 0,
                    right: (float?)(clipDic["right"] as PsbNumber)?.Value ?? 1,
                    bottom: (float?)(clipDic["bottom"] as PsbNumber)?.Value ?? 1
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
            var md = new ResourceMetadata()
            {
                Index = r.Index ?? 0,
                Compress = compress,
                Name = name,
                Part = part,
                Clip = clip,
                Is2D = is2D,
                OriginX = originX,
                OriginY = originY,
                Width = width,
                Height = height,
                Type = type,
                Data = r.Data,
            };
            return md;
        }

        /// <summary>
        /// Get related name on depth 3
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        private static string GetPartName(this IPsbCollection c)
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
        public static string GetName(this IPsbCollection c)
        {
            var source = c?.Parent as PsbDictionary;
            var result = source?.Value.FirstOrDefault(pair => Equals(pair.Value, c));
            return result?.Value == null ? null : result.Value.Key;
        }
    }
}
