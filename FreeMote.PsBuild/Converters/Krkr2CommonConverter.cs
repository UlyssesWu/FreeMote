using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using FreeMote.Psb;
using FreeMote.PsBuild.Textures;

namespace FreeMote.PsBuild.Converters
{
    /// <summary>
    /// Convert krkr to common/win
    /// </summary>
    class Krkr2CommonConverter : ISpecConverter
    {
        private const string Delimiter = "@";

        public Krkr2CommonConverter(bool toWin = false)
        {
            ToWin = toWin;
            TargetPixelFormat = ToWin ? PsbPixelFormat.WinRGBA8 : PsbPixelFormat.CommonRGBA8;
        }

        public SpecConvertOption ConvertOption { get; set; } = SpecConvertOption.Default;

        public PsbPixelFormat TargetPixelFormat { get; set; }
        public bool UseRL { get; set; } = false;
        public IList<PsbSpec> FromSpec { get; } = new List<PsbSpec> { PsbSpec.krkr };
        public IList<PsbSpec> ToSpec { get; } = new List<PsbSpec> { PsbSpec.win, PsbSpec.common };
        public bool ToWin { get; set; }

        public int? TextureSideLength { get; set; } = null;
        public int TexturePadding { get; set; } = 5;
        public BestFitHeuristic FitHeuristic { get; set; } = BestFitHeuristic.MaxOneAxis;

        public bool UseMeaningfulName { get; set; } = true;

        public void Convert(PSB psb)
        {
            if (!FromSpec.Contains(psb.Platform))
            {
                throw new FormatException("Can not convert Spec for this PSB");
            }
            if (ConvertOption == SpecConvertOption.Minimum)
            {
                Remove(psb);
            }
            var iconInfo = TranslateResources(psb);
            Travel((PsbDictionary)psb.Objects["object"], iconInfo);
            Add(psb);
            psb.Platform = ToWin ? PsbSpec.win : PsbSpec.common;
        }

        private void Remove(PSB psb)
        {
            //remove /metadata/attrcomp
            var metadata = (PsbDictionary)psb.Objects["metadata"];
            metadata.Remove("attrcomp");
        }

        private void Add(PSB psb)
        {
            //add `easing`
            if (!psb.Objects.ContainsKey("easing"))
            {
                psb.Objects.Add("easing", new PsbCollection(0));
            }

            //add `/object/*/motion/*/bounds`
            //add `/object/*/motion/*/layerIndexMap`
            var obj = (PsbDictionary)psb.Objects["object"];
            foreach (var o in obj)
            {
                //var name = o.Key;
                foreach (var m in (PsbDictionary)((PsbDictionary)o.Value)["motion"])
                {
                    if (m.Value is PsbDictionary mDic)
                    {
                        if (!mDic.ContainsKey("bounds"))
                        {
                            var bounds = new PsbDictionary(4)
                            {
                            {"top", new PsbNumber(0)},
                            {"left", new PsbNumber(0)},
                            {"right", new PsbNumber(0)},
                            {"bottom", new PsbNumber(0)}
                            };
                            mDic.Add("bounds", bounds);
                        }


                        if (!(mDic["layer"] is PsbCollection col))
                        {
                            continue;
                        }

                        if (!mDic.ContainsKey("layerIndexMap"))
                        {
                            var layerIndexList = new List<string>();
                            LayerTravel(col, layerIndexList);
                            var layerIndexMap = new PsbDictionary(layerIndexList.Count);
                            int index = 0;
                            foreach (var layerName in layerIndexList)
                            {
                                if (layerIndexMap.ContainsKey(layerName))
                                {
                                    continue;
                                }
                                layerIndexMap.Add(layerName, new PsbNumber(index));
                                index++;
                            }
                            mDic.Add("layerIndexMap", layerIndexMap);
                        }
                    }
                }
            }

            void LayerTravel(PsbCollection collection, List<string> indexList)
            {
                foreach (var col in collection)
                {
                    if (col is PsbDictionary dic && dic.ContainsKey("children"))
                    {
                        if (dic["label"] is PsbString str)
                        {
                            indexList.Add(str.Value);
                        }
                        if (dic["children"] is PsbCollection childrenCollection)
                        {
                            LayerTravel(childrenCollection, indexList);
                        }
                    }
                }
            }
        }

        private Dictionary<string, (string Tex, string IconName)> TranslateResources(PSB psb)
        {
            Dictionary<string, (string Tex, string IconName)> iconInfos = new Dictionary<string, (string, string)>();
            Dictionary<string, Image> textures = new Dictionary<string, Image>();
            var source = (PsbDictionary)psb.Objects["source"];
            int maxSideLength = 2048;
            long area = 0;

            //Collect textures
            foreach (var tex in source)
            {
                var texName = tex.Key;
                var icons = (PsbDictionary)((PsbDictionary)tex.Value)["icon"];
                foreach (var icon in icons)
                {
                    var iconName = icon.Key;
                    var info = (PsbDictionary)icon.Value;
                    var width = (int)(PsbNumber)info["width"];
                    var height = (int)(PsbNumber)info["height"];
                    var res = (PsbResource)info["pixel"];
                    var bmp = info["compress"]?.ToString().ToUpperInvariant() == "RL"
                        ? RL.UncompressToImage(res.Data, height, width, psb.Platform.DefaultPixelFormat())
                        : RL.ConvertToImage(res.Data, height, width, psb.Platform.DefaultPixelFormat());
                    bmp.Tag = iconName;
                    textures.Add($"{texName}{Delimiter}{icon.Key}", bmp);
                    //estimate area and side length
                    area += width * height;
                    if (width >= maxSideLength || height >= maxSideLength)
                    {
                        maxSideLength = 4096;
                    }
                }
            }

            //Pack textures
            int size = 2048;
            if (maxSideLength > size || (area > 2048 * 2048 && psb.Header.Version > 2))
            {
                size = 4096;
            }
            int padding = TexturePadding >= 0 && TexturePadding <= 100 ? TexturePadding : 1;

            TexturePacker packer = new TexturePacker
            {
                FitHeuristic = FitHeuristic
            };
            packer.Process(textures, TextureSideLength ?? size, padding);

            //Add packed textures to source
            List<PsbDictionary> texs = new List<PsbDictionary>();
            for (var i = 0; i < packer.Atlasses.Count; i++)
            {
                var atlas = packer.Atlasses[i];
                var data = UseRL
                    ? RL.CompressImage((Bitmap)atlas.ToImage(), TargetPixelFormat)
                    : RL.GetPixelBytesFromImage(atlas.ToImage(), TargetPixelFormat);

                var texDic = new PsbDictionary(4);
                //metadata
                texDic.Add("metadata", new PsbString(i.ToString("D3")));
                var texName = $"tex#{texDic["metadata"]}";
                //icon
                var icons = new PsbDictionary(atlas.Nodes.Count);
                texDic.Add("icon", icons);
                int id = 0;
                foreach (var node in atlas.Nodes)
                {
                    if (node.Texture == null)
                    {
                        continue;
                    }
                    var paths = node.Texture.Source.Split(new[] { Delimiter }, StringSplitOptions.RemoveEmptyEntries);
                    var icon = (PsbDictionary)source[paths[0]].Children("icon").Children(paths[1]);
                    icon.Remove("compress");
                    icon.Remove("pixel");
                    icon["attr"] = new PsbNumber(0);
                    icon["left"] = new PsbNumber(node.Bounds.Left);
                    icon["top"] = new PsbNumber(node.Bounds.Top);
                    icon.Parent = icons;
                    var iconName = UseMeaningfulName ? node.Texture.Source : id.ToString();
                    icons.Add(iconName, icon);
                    iconInfos.Add(node.Texture.Source, (texName, iconName));
                    id++;
                }
                //texture
                //TODO: support truncated
                var texture = new PsbDictionary(6)
                {
                    {"height", new PsbNumber(atlas.Height)},
                    {"width", new PsbNumber(atlas.Width)},
                    {"truncated_height", new PsbNumber(atlas.Height)},
                    {"truncated_width", new PsbNumber(atlas.Width)},
                    {"type", new PsbString(TargetPixelFormat.ToStringForPsb())}
                };
                texture.Add("pixel", new PsbResource { Data = data, Parents = new List<IPsbCollection> { texture } });
                texDic.Add("texture", texture);
                //type
                texDic.Add("type", new PsbNumber(0));

                texs.Add(texDic);
            }

            source.Clear();
            foreach (var t in texs)
            {
                source.Add($"tex#{t["metadata"]}", t);
                t["metadata"] = PsbNull.Null;
            }
            return iconInfos;
        }

        private void Travel(IPsbCollection collection, Dictionary<string, (string Tex, string IconName)> iconInfos)
        {
            if (collection is PsbDictionary dic)
            {
                if (dic.ContainsKey("mask") && dic.GetName() == "content")
                {
                    if (dic["src"] is PsbString s)
                    {
                        //"blank" ("icon" : "32:32:16:16") <- "blank/32:32:16:16"
                        if (s.Value.StartsWith("blank"))
                        {
                            //var size = dic["icon"].ToString();
                            var iconName = s.Value.Substring(s.Value.LastIndexOf('/') + 1);
                            dic["icon"] = new PsbString(iconName);
                            dic["src"] = new PsbString("blank");
                        }
                        //"tex" ("icon" : "0001") <- "src/tex/0001"
                        else if (s.Value.StartsWith("src/"))
                        {
                            //var iconName = dic["icon"].ToString();
                            var iconName = s.Value.Substring(s.Value.LastIndexOf('/') + 1);
                            var partName = new string(s.Value.SkipWhile(c => c != '/').Skip(1).TakeWhile(c => c != '/')
                                .ToArray());
                            var name = $"{partName}{Delimiter}{iconName}";
                            dic["icon"] = new PsbString(iconInfos[name].IconName);
                            dic["src"] = new PsbString(iconInfos[name].Tex);
                        }
                        //"ex_body_a" ("icon" : "差分A") <- "motion/ex_body_a/差分A"
                        else if (s.Value.StartsWith("motion/"))
                        {
                            //var iconName = dic["icon"].ToString();
                            var iconName = s.Value.Substring(s.Value.LastIndexOf('/') + 1);
                            dic["icon"] = new PsbString(iconName);
                            dic["src"] = new PsbString(
                                new string(s.Value.SkipWhile(c => c != '/').Skip(1).TakeWhile(c => c != '/').ToArray()));
                        }
                        //remove src = layout || src = shape/point (0) ?
                        else if (s.Value == "layout" || s.Value.StartsWith("shape/"))
                        {
                            dic.Remove("src");
                        }
                        //wrong way↓
                        //var num = (PsbNumber)dic["mask"];
                        //if (num.IntValue == 1 || num.IntValue == 3 || num.IntValue == 19)
                        //{
                        //    dic.Remove("src");
                        //}
                    }
                    //mask -= 1
                    var num = (PsbNumber)dic["mask"];
                    num.IntValue = num.IntValue - 1;
                    //remove ox, oy ?
                    //if (ConvertOption == SpecConvertOption.Minimum)
                    {
                        dic.Remove("ox");
                        dic.Remove("oy");
                    }
                }

                foreach (var child in dic.Values)
                {
                    if (child is IPsbCollection childCol)
                    {
                        Travel(childCol, iconInfos);
                    }
                }
            }
            if (collection is PsbCollection col)
            {
                foreach (var child in col)
                {
                    if (child is IPsbCollection childCol)
                    {
                        Travel(childCol, iconInfos);
                    }
                }
            }
        }
    }
}
