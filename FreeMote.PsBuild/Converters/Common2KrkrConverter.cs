using System;
using System.Collections.Generic;
using FreeMote.Psb;
using FreeMote.Psb.Textures;

namespace FreeMote.PsBuild.Converters
{
    /// <summary>
    /// Convert common/win to krkr
    /// </summary>
    class Common2KrkrConverter : ISpecConverter
    {
        /// <summary>
        /// krkr uses <see cref="PsbPixelFormat.LeRGBA8"/> on windows platform
        /// </summary>
        public PsbPixelFormat TargetPixelFormat { get; set; } = PsbPixelFormat.LeRGBA8;

        public bool UseRL { get; set; } = true;
        public IList<PsbSpec> FromSpec { get; } = new List<PsbSpec> {PsbSpec.win, PsbSpec.common};
        public IList<PsbSpec> ToSpec { get; } = new List<PsbSpec> {PsbSpec.krkr};

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
            if (ConvertOption == SpecConvertOption.Maximum)
            {
                Add(psb);
            }

            psb.Platform = PsbSpec.krkr;
        }

        public SpecConvertOption ConvertOption { get; set; } = SpecConvertOption.Default;

        private void Remove(PSB psb)
        {
            //remove `easing`
            psb.Objects.Remove("easing");

            //remove `/object/*/motion/*/bounds`
            //remove `/object/*/motion/*/layerIndexMap`
            var obj = (PsbDictionary) psb.Objects["object"];
            foreach (var o in obj)
            {
                //var name = o.Key;
                foreach (var m in (PsbDictionary) ((PsbDictionary) o.Value)["motion"])
                {
                    if (m.Value is PsbDictionary mDic)
                    {
                        mDic.Remove("bounds");
                        mDic.Remove("layerIndexMap");
                    }
                }
            }
        }

        private Dictionary<string, List<string>> TranslateResources(PSB psb)
        {
            Dictionary<string, List<string>> iconInfos = new Dictionary<string, List<string>>();
            var source = (PsbDictionary) psb.Objects["source"];
            foreach (var tex in source)
            {
                if (tex.Value is PsbDictionary texDic)
                {
                    var iconList = new List<string>();
                    iconInfos.Add(tex.Key, iconList);
                    var bmps = TextureSpliter.SplitTexture(texDic, psb.Platform);
                    var icons = (PsbDictionary) texDic["icon"];
                    foreach (var iconPair in icons)
                    {
                        iconList.Add(iconPair.Key);
                        var icon = (PsbDictionary) iconPair.Value;
                        var data = UseRL
                            ? RL.CompressImage(bmps[iconPair.Key], TargetPixelFormat)
                            : RL.GetPixelBytesFromImage(bmps[iconPair.Key], TargetPixelFormat);
                        icon[Consts.ResourceKey] =
                            new PsbResource {Data = data, Parents = new List<IPsbCollection> {icon}};
                        icon["compress"] = UseRL ? new PsbString("RL") : new PsbString();
                        icon.Remove("left");
                        icon.Remove("top");
                        //There is no obvious match for attr?
                        //if (icon["attr"] is PsbNumber n && n.AsInt > 0)
                        //{
                        //    icon["attr"] = PsbNull.Null;
                        //}
                        //else
                        //{
                        //    icon.Remove("attr");
                        //}
                        icon.Remove("attr");
                    }

                    texDic.Remove("texture");
                    texDic["type"] = new PsbNumber(1);
                }
            }

            return iconInfos;
        }

        private void Travel(IPsbCollection collection, Dictionary<string, List<string>> iconInfo)
        {
            //TODO: recover icon names
            if (collection is PsbDictionary dic)
            {
                //mask+=1
                //add ox=0, oy=0 //explain: the last bit of mask (00...01) is whether to use ox & oy. if use, last bit of mask is 1
                //change src
                if (dic.ContainsKey("mask") && dic.GetName() == "content")
                {
                    if (dic["src"] is PsbString s)
                    {
                        //"blank" ("icon" : "32:32:16:16") -> "blank/32:32:16:16"
                        if (s.Value == "blank")
                        {
                            var size = dic["icon"].ToString();
                            dic["src"] = new PsbString($"blank/{size}");
                        }
                        //"tex" ("icon" : "0001") -> "src/tex/0001"
                        else if (iconInfo.ContainsKey(s))
                        {
                            var iconName = dic["icon"].ToString();
                            dic["src"] = new PsbString($"src/{s}/{iconName}");
                        }
                        //"ex_body_a" ("icon" : "差分A") -> "motion/ex_body_a/差分A"
                        else
                        {
                            var iconName = dic["icon"].ToString();
                            dic["src"] = new PsbString($"motion/{s}/{iconName}");
                        }
                    }

                    var num = (PsbNumber) dic["mask"];
                    num.AsInt |= 1;
                    //num.IntValue = num.IntValue + 1;
                    //add src = layout || src = shape/point (0)
                    if (num.IntValue == 1 || num.IntValue == 3 || num.IntValue == 19)
                    {
                        if (!dic.ContainsKey("src"))
                        {
                            bool isLayout = true;
                            //content -> {} -> [] -> {}
                            if (dic.Parent.Parent.Parent is PsbDictionary childrenArrayDic)
                            {
                                if (childrenArrayDic.ContainsKey("shape"))
                                {
                                    string shape = ((PsbNumber) childrenArrayDic["shape"]).ToShapeString();

                                    dic.Add("src", new PsbString($"shape/{shape}"));
                                    isLayout = false;
                                }
                            }

                            if (isLayout)
                            {
                                dic.Add("src", new PsbString("layout"));
                            }
                        }
                    }

                    if (!dic.ContainsKey("ox"))
                    {
                        dic.Add("ox", PsbNumber.Zero);
                    }

                    if (!dic.ContainsKey("oy"))
                    {
                        dic.Add("oy", PsbNumber.Zero);
                    }
                }

                foreach (var child in dic.Values)
                {
                    if (child is IPsbCollection childCol)
                    {
                        Travel(childCol, iconInfo);
                    }
                }
            }

            if (collection is PsbList col)
            {
                foreach (var child in col)
                {
                    if (child is IPsbCollection childCol)
                    {
                        Travel(childCol, iconInfo);
                    }
                }
            }
        }

        private void Add(PSB psb)
        {
            var metadata = (PsbDictionary) psb.Objects["metadata"];
            if (!metadata.ContainsKey("attrcomp"))
            {
                metadata.Add("attrcomp", new PsbDictionary(1));
            }
        }
    }
}