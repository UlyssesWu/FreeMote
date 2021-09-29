using System;
using System.Collections.Generic;
using System.Linq;

namespace FreeMote.Psb.Types
{
    class PimgType : BaseImageType, IPsbType
    {
        public const string PimgSourceKey = "layers";

        public PsbType PsbType => PsbType.Pimg;
        public bool IsThisType(PSB psb)
        {
            if (psb.Objects == null)
            {
                return false;
            }

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
                    Compress = k.Key.EndsWith(".tlg", true, null) ? PsbCompressType.Tlg : PsbCompressType.ByName,
                    PsbType = PsbType.Pimg,
                    Spec = psb.Platform
                }).Cast<T>());
            FindPimgResources(resourceList, psb.Objects[PimgSourceKey], deDuplication);

            return resourceList;
        }

        private static void FindPimgResources<T>(List<T> list, IPsbValue obj, bool deDuplication = true) where T: IResourceMetadata
        {
            if (obj is PsbList c)
            {
                foreach (var o in c)
                {
                    if (!(o is PsbDictionary dic)) continue;
                    if (dic.ContainsKey("layer_id"))
                    {
                        int layerId = 0;
                        if (dic["layer_id"] is PsbString sLayerId && int.TryParse(sLayerId.Value, out var id))
                        {
                            layerId = id;
                        }
                        else if (dic["layer_id"] is PsbNumber nLayerId)
                        {
                            layerId = nLayerId.IntValue;
                        }
                        else
                        {
                            Console.WriteLine($"[WARN] layer_id {dic["layer_id"]} is wrong.");
                            continue;
                        }

                        var res = (ImageMetadata)(IResourceMetadata)list.FirstOrDefault(k => k.Name.StartsWith(layerId.ToString(), true, null));
                        if (res == null)
                        {
                            continue;
                        }

                        res.Index = (uint)layerId;
                        //res.Part = layerId.ToString();

                        if (dic["width"] is PsbNumber nw)
                        {
                            res.Width = GetIntValue(nw, res.Width);
                        }

                        if (dic["height"] is PsbNumber nh)
                        {
                            res.Height = GetIntValue(nh, res.Height);
                        }

                        if (dic["top"] is PsbNumber nt)
                        {
                            res.Top = GetIntValue(nt, res.Top);
                        }

                        if (dic["left"] is PsbNumber nl)
                        {
                            res.Left = GetIntValue(nl, res.Left);
                        }

                        if (dic["opacity"] is PsbNumber no)
                        {
                            res.Opacity = GetIntValue(no, res.Opacity);
                        }

                        if (dic["group_layer_id"] is PsbNumber gLayerId)
                        {
                            res.Part = gLayerId.IntValue.ToString();
                        }

                        if (dic["visible"] is PsbNumber nv)
                        {
                            res.Visible = nv.IntValue != 0;
                        }

                        if (dic["name"] is PsbString nn)
                        {
                            res.Label = nn.Value;
                        }
                    }
                }
            }

            int GetIntValue(PsbNumber num, int ori) => deDuplication ? Math.Max((int)num, ori) : (int)num;
        }
    }
}
