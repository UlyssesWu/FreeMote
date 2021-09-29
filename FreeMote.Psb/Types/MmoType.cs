using System.Collections.Generic;
using System.Linq;

namespace FreeMote.Psb.Types
{
    class MmoType : BaseImageType, IPsbType
    {
        public const string MmoSourceKey = "sourceChildren";
        public const string MmoBgSourceKey = "bgChildren";

        public PsbType PsbType => PsbType.Mmo;
        public bool IsThisType(PSB psb)
        {
            if (psb.Objects == null)
            {
                return false;
            }
            return psb.Objects.ContainsKey("objectChildren") && psb.Objects.ContainsKey("sourceChildren");
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : IResourceMetadata
        {
            List<T> resourceList = psb.Resources == null
                ? new List<T>()
                : new List<T>(psb.Resources.Count);

            FindMmoResources(resourceList, psb.Objects[MmoBgSourceKey], MmoBgSourceKey, deDuplication);
            FindMmoResources(resourceList, psb.Objects[MmoSourceKey], MmoSourceKey, deDuplication);

            resourceList.ForEach(r =>
            {
                r.PsbType = PsbType.Mmo;
                r.Spec = psb.Platform;
            });
            return resourceList;
        }
        
        private static void FindMmoResources<T>(List<T> list, IPsbValue obj, in string defaultPartname = "",
    bool deDuplication = true) where T: IResourceMetadata
        {
            switch (obj)
            {
                case PsbList c:
                    foreach (var o in c) FindMmoResources(list, o, defaultPartname, deDuplication);
                    break;
                case PsbDictionary d:
                    if (d[Consts.ResourceKey] is PsbResource r)
                    {
                        if (!deDuplication)
                        {
                            list.Add((T)(IResourceMetadata)GenerateMmoResMetadata(d, defaultPartname, r));
                        }
                        else if (r.Index == null || list.FirstOrDefault(md => md.Index == r.Index.Value) == null)
                        {
                            list.Add((T)(IResourceMetadata)GenerateMmoResMetadata(d, defaultPartname, r));
                        }
                    }

                    foreach (var o in d.Values)
                    {
                        FindMmoResources(list, o, defaultPartname, deDuplication);
                    }

                    break;
            }
        }

        private static ImageMetadata GenerateMmoResMetadata(PsbDictionary d, string defaultPartName = "",
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
                width = (int)nw;
            }

            if ((d["height"] ?? dd["height"]) is PsbNumber nh)
            {
                is2D = true;
                height = (int)nh;
            }

            if ((dd["originX"] ?? d["originX"]) is PsbNumber nx)
            {
                is2D = true;
                originX = (float)nx;
            }

            if ((dd["originY"] ?? d["originY"]) is PsbNumber ny)
            {
                is2D = true;
                originY = (float)ny;
            }

            var md = new ImageMetadata()
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

    }
}
