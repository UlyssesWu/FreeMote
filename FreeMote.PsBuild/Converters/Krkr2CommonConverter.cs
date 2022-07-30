using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using FreeMote.Psb;
using FreeMote.Psb.Textures;
// ReSharper disable CompareOfFloatsByEqualityOperator

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
            TargetPixelFormat = ToWin ? PsbPixelFormat.LeRGBA8 : PsbPixelFormat.BeRGBA8;
        }

        public SpecConvertOption ConvertOption { get; set; } = SpecConvertOption.Default;

        public PsbPixelFormat TargetPixelFormat { get; set; }
        public bool UseRL { get; set; } = false;
        public IList<PsbSpec> FromSpec { get; } = new List<PsbSpec> {PsbSpec.krkr};
        public IList<PsbSpec> ToSpec { get; } = new List<PsbSpec> {PsbSpec.win, PsbSpec.common};
        public bool ToWin { get; set; }

        public int? TextureSideLength { get; set; } = null;
        public int TexturePadding { get; set; } = 5;
        public BestFitHeuristic FitHeuristic { get; set; } = BestFitHeuristic.MaxOneAxis;

        /// <summary>
        /// Use name gained from krkr PSB like "vr足l"
        /// </summary>
        public bool UseMeaningfulName { get; set; } = true;
        /// <summary>
        /// If enable, scale down the image to match the target resolution, maybe causing bad quality.
        /// <para>If not enable, ignore and remove "resolution" in icon (reset resolution to 1, making the image clear)</para>
        /// </summary>
        public bool EnableResolution { get; set; } = false;
        /// <summary>
        /// Expand texture edge
        /// </summary>
        public TextureEdgeProcess EdgeProcess { get; set; } = TextureEdgeProcess.Expand1Px;

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
            Travel((PsbDictionary) psb.Objects["object"], iconInfo);
            Add(psb);
            TranslateTimeline(psb);
            psb.Platform = ToWin ? PsbSpec.win : PsbSpec.common;
        }

        private void Remove(PSB psb)
        {
            //remove /metadata/attrcomp
            var metadata = (PsbDictionary) psb.Objects["metadata"];
            metadata.Remove("attrcomp");
        }

        private void Add(PSB psb)
        {
            //add `easing`
            if (!psb.Objects.ContainsKey("easing"))
            {
                psb.Objects.Add("easing", new PsbList(0));
            }

            //add `/object/*/motion/*/bounds`
            //add `/object/*/motion/*/layerIndexMap`
            var obj = (PsbDictionary) psb.Objects["object"];
            foreach (var o in obj)
            {
                //var name = o.Key;
                foreach (var m in (PsbDictionary) ((PsbDictionary) o.Value)["motion"])
                {
                    if (m.Value is PsbDictionary mDic)
                    {
                        if (!mDic.ContainsKey("bounds"))
                        {
                            var bounds = new PsbDictionary(4)
                            {
                                {"top", PsbNumber.Zero},
                                {"left", PsbNumber.Zero},
                                {"right", PsbNumber.Zero},
                                {"bottom", PsbNumber.Zero}
                            };
                            mDic.Add("bounds", bounds);
                        }


                        if (!(mDic["layer"] is PsbList col))
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

            void LayerTravel(PsbList collection, List<string> indexList)
            {
                foreach (var col in collection)
                {
                    if (col is PsbDictionary dic && dic.ContainsKey("children"))
                    {
                        if (dic["label"] is PsbString str)
                        {
                            indexList.Add(str.Value);
                        }

                        if (dic["children"] is PsbList childrenCollection)
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
            var source = (PsbDictionary) psb.Objects["source"];
            int maxSideLength = 2048;
            long area = 0;
            var texRegex = new Regex($"^tex#.+?{Delimiter}");

            //Collect textures
            foreach (var tex in source)
            {
                var texName = tex.Key;
                var icons = (PsbDictionary) ((PsbDictionary) tex.Value)["icon"];
                foreach (var icon in icons)
                {
                    var iconName = icon.Key;
                    var match = texRegex.Match(iconName);
                    if (match.Success)
                    {
                        iconName = iconName.Substring(match.Length);
                    }
                    var info = (PsbDictionary) icon.Value;
                    var width = (int) (PsbNumber) info["width"];
                    var height = (int) (PsbNumber) info["height"];
                    var res = info[Consts.ResourceKey] as PsbResource;
                    if (res == null)
                    {
                        Debug.WriteLine("pixel is null! Maybe External Texture."); //TODO: throw Exception
                        continue;
                    }
                    var bmp = info["compress"]?.ToString().ToUpperInvariant() == "RL"
                        ? RL.DecompressToImage(res.Data, width, height, psb.Platform.DefaultPixelFormat())
                        : RL.ConvertToImage(res.Data, width, height, psb.Platform.DefaultPixelFormat());
                    if (info.ContainsKey("resolution") && info["resolution"].GetFloat() != 1.0f && EnableResolution)
                    {
                        //scale down image, not recommended
                        var resolution = info["resolution"].GetFloat();
                        var newWidth = (int) Math.Ceiling(width * resolution);
                        var newHeight = (int) Math.Ceiling(height * resolution);
                        var resizedBmp = bmp.ResizeImage(newWidth, newHeight);
                        bmp.Dispose();
                        bmp = resizedBmp;
                        width = newWidth;
                        height = newHeight;
                    }
                    bmp.Tag = iconName;
                    textures.Add($"{texName}{Delimiter}{iconName}", bmp);
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

            int padding = TexturePadding is >= 0 and <= 100 ? TexturePadding : 1;

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
                var atlasImg = atlas.ToImage(edge: EdgeProcess);
                var data = UseRL
                    ? RL.CompressImage((Bitmap)atlasImg, TargetPixelFormat)
                    : RL.GetPixelBytesFromImage(atlasImg, TargetPixelFormat);

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

                    var delimiterPos = node.Texture.Source.IndexOf(Delimiter, StringComparison.Ordinal);
                    if (delimiterPos < 0)
                    {
                        throw new FormatException($"cannot parse icon path: {node.Texture.Source}");
                    }
                    var texPath = node.Texture.Source.Substring(0, delimiterPos);
                    var iconPath = node.Texture.Source.Substring(delimiterPos + 1);
                    //var paths = node.Texture.Source.Split(new[] {Delimiter}, StringSplitOptions.RemoveEmptyEntries);
                    var icon = (PsbDictionary) source[texPath].Children("icon").Children(iconPath);
                    icon.Remove("compress");
                    icon.Remove(Consts.ResourceKey);
                    icon["attr"] = PsbNumber.Zero;
                    icon["left"] = new PsbNumber(node.Bounds.Left);
                    icon["top"] = new PsbNumber(node.Bounds.Top);
                    if (icon.ContainsKey("resolution") && icon["resolution"].GetFloat() != 1.0f && !EnableResolution)
                    {
                        //Converting from krkr to win. Krkr has the full size image. We just keep resolution = 1.
                        //Maybe implement scale down later. 
                        var resolution = icon["resolution"].GetFloat();
                        icon["resolution_hint"] = new PsbNumber(resolution); //leave a hint here
                        icon.Remove("resolution");
                    }
                    icon.Parent = icons;
                    var iconName = id.ToString();
                    if (UseMeaningfulName)
                    {
                        var meaningfulName = node.Texture.Source;
                        var match = texRegex.Match(meaningfulName);
                        if (match.Success)
                        {
                            meaningfulName = meaningfulName.Substring(match.Length);
                        }
                        if (!string.IsNullOrWhiteSpace(meaningfulName) && !icons.ContainsKey(meaningfulName))
                        {
                            iconName = meaningfulName;
                        }
                    }
                    //var iconName = UseMeaningfulName ? node.Texture.Source : id.ToString();
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
                texture.Add(Consts.ResourceKey, new PsbResource {Data = data, Parents = new List<IPsbCollection> {texture}});
                texDic.Add("texture", texture);
                //type
                texDic.Add("type", PsbNumber.Zero);

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
                ////remove meshDivision
                //if (dic.ContainsKey("meshDivision"))
                //{
                //    if (dic.ContainsKey("inheritMask") && dic["inheritMask"] is PsbNumber p && p.AsInt == 33556476)
                //    {
                //        //do nothing
                //    }
                //    else
                //    {
                //        if (dic["meshDivision"] is PsbNumber p2 && p2.AsInt == 10)
                //        {
                //            dic.Remove("meshDivision");
                //        }
                //    }
                //}

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
                            if (!iconInfos.ContainsKey(name))
                            {
                                Console.WriteLine($"[WARN] cannot find icon {name} in source (it may happens in krkr PSB), ignored.");
                                dic.Remove("src");
                            }
                            else
                            {
                                dic["icon"] = new PsbString(iconInfos[name].IconName);
                                dic["src"] = new PsbString(iconInfos[name].Tex);
                            }
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
                        //remove src = layout || src = shape/point (0) ? //TODO: convert shape id to shape string?
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
                    var num = (PsbNumber) dic["mask"];
                    if (dic["ox"] is PsbNumber ox && dic["oy"] is PsbNumber oy && (ox != PsbNumber.Zero || oy != PsbNumber.Zero))
                    {
                        //keep ox,oy
                    }
                    else
                    {
                        //ox = 0,oy = 0, it's redundant, remove ox, oy
                        dic.Remove("ox");
                        dic.Remove("oy");
                        num.AsInt = num.IntValue & int.MaxValue - 1; //set last bit to 0
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

            if (collection is PsbList col)
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

        private void TranslateTimeline(PSB psb)
        {
            PsbList nList = new PsbList();

            void TranslateChildren(PsbList childrenList, string path)
            {
                foreach (var timeline in childrenList)
                {
                    if (timeline is PsbDictionary item)
                    {
                        if (item["type"] is PsbString { Value: "folder" } && item["children"] is PsbList children)
                        {
                            TranslateChildren(children, $"{path}/{item["label"].ToString()}");
                        }
                        else if (item["variableList"] is PsbList variableList)
                        {
                            item["path_hint"] = $"{path}/{item["label"].ToString()}".ToPsbString();
                            nList.Add(item);
                        }
                        else
                        {
                            Console.WriteLine($"[WARN] Cannot convert timeline {item["label"].ToString()}");
                        }
                    }
                }
            }

            if (psb.Objects["metadata"] is PsbDictionary metadata && metadata["timelineControl"] is PsbList timelines)
            {
                TranslateChildren(timelines, "");
                metadata["timelineControl"] = nList;
            }
        }
    }
}