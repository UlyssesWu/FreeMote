using System;
using System.Collections.Generic;
using System.Linq;
using FreeMote.Plugins;

namespace FreeMote.Psb.Types
{
    class PimgType : BaseImageType, IPsbType
    {
        public const string PimgSourceKey = "layers";

        public PsbType PsbType => PsbType.Pimg;
        public bool IsThisType(PSB psb)
        {
            if (psb.Objects.ContainsKey("layers") && psb.Objects.ContainsKey("height") && psb.Objects.ContainsKey("width"))
            {
                return true;
            }

            if (psb.Objects.Any(k => k.Key.Contains(".") && k.Value is PsbResource))
            {
                return true;
            }

            return false;
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : IResourceMetadata
        {
            List<T> resourceList = psb.Resources == null
                ? new List<T>()
                : new List<T>(psb.Resources.Count);

            resourceList.AddRange(psb.Objects.Where(k => k.Value is PsbResource).Select(k =>
                new ImageMetadata()
                {
                    Name = k.Key,
                    Resource = k.Value as PsbResource,
                    Compress = k.Key.EndsWith(".tlg", true, null) ? PsbCompressType.Tlg : PsbCompressType.ByName
                }).Cast<T>());
            FindPimgResources(resourceList, psb.Objects[PimgSourceKey], deDuplication);

            return resourceList;
        }

        public List<IResourceMetadata> CollectResources(PSB psb, bool deDuplication = true)
        {
            List<IResourceMetadata> resourceList = psb.Resources == null
                ? new List<IResourceMetadata>()
                : new List<IResourceMetadata>(psb.Resources.Count);

            resourceList.AddRange(psb.Objects.Where(k => k.Value is PsbResource).Select(k =>
                new ImageMetadata()
                {
                    Name = k.Key,
                    Resource = k.Value as PsbResource,
                    Compress = k.Key.EndsWith(".tlg", true, null) ? PsbCompressType.Tlg : PsbCompressType.ByName
                }));
            FindPimgResources(resourceList, psb.Objects[PimgSourceKey], deDuplication);

            return resourceList;
        }

        public Dictionary<string, string> OutputResources(PSB psb, FreeMountContext context, string filePath,
            PsbExtractOption extractOption = PsbExtractOption.Original)
        {
            throw new NotImplementedException();
        }

        private static void FindPimgResources<T>(List<T> list, IPsbValue obj, bool deDuplication = true) where T: IResourceMetadata
        {
            if (obj is PsbList c)
            {
                foreach (var o in c)
                {
                    if (!(o is PsbDictionary dic)) continue;
                    if (dic["layer_id"] is PsbString layerId)
                    {
                        var res = (ImageMetadata)(IResourceMetadata)list.FirstOrDefault(k => k.Name.StartsWith(layerId.Value, true, null));
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
                            res.Width = deDuplication ? Math.Max((int)nw, res.Width) : (int)nw;
                        }

                        if (dic["height"] is PsbNumber nh)
                        {
                            res.Height = deDuplication ? Math.Max((int)nh, res.Height) : (int)nh;
                        }
                    }
                }
            }
        }
    }
}
