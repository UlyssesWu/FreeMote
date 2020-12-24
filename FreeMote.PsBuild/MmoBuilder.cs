//This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License. To view a copy of this license, visit http://creativecommons.org/licenses/by-nc-sa/4.0/ or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.
//Author: Ulysses (wdwxy12345@gmail.com)

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using FreeMote.Psb;
using FreeMote.Psb.Textures;
using FreeMote.PsBuild.Properties;
using Newtonsoft.Json;
// ReSharper disable StringLiteralTypo
// ReSharper disable InconsistentNaming

namespace FreeMote.PsBuild
{
    /// <summary>
    /// Build MMO from EMT PSB
    /// <para>Current Ver: 3.12</para>
    /// </summary>
    public partial class MmoBuilder
    {
        internal bool DebugMode { get; set; } = false;
        public PSB Mmo { get; private set; }

        public Dictionary<string, MmoPsdMetadata> MmoPsdMetadatas { get; set; }

        /// <summary>
        /// Key: keyword in object path; Value: if find that keyword in path, set a custom menu path (for Editor)
        /// </summary>
        public Dictionary<string, string> CustomPartMenuPaths { get; set; } = new Dictionary<string, string>();

        public MmoBuilder() : this(false)
        { }

        internal MmoBuilder(bool debug = false)
        {
            #region Default Metadata

            DebugMode = debug;
            MmoPsdMetadatas = debug
            ? new Dictionary<string, MmoPsdMetadata>
            {
                    {
                        "face_eye_mabuta_l", new MmoPsdMetadata
                        {
                            SourceLabel = "face_eye_mabuta_l",
                            Category = "Emotion",
                            PsdGroup = "Eye-L",
                            Label = "Orbit-L"
                        }
                    },
                    {
                        "face_eye_mabuta_r", new MmoPsdMetadata
                        {
                            SourceLabel = "face_eye_mabuta_r",
                            Category = "Emotion",
                            PsdGroup = "Eye-R",
                            Label = "Orbit-R",
                        }
                    },
                    {
                        "face_eye_hitomi_l", new MmoPsdMetadata
                        {
                            SourceLabel = "face_eye_hitomi_l",
                            Category = "Emotion",
                            PsdGroup = "Eye-L",
                            Label = "Pupil-L",
                        }
                    },
                    {
                        "face_eye_hitomi_r", new MmoPsdMetadata
                        {
                            SourceLabel = "face_eye_hitomi_r",
                            Category = "Emotion",
                            PsdGroup = "Eye-R",
                            Label = "Pupil-R",
                        }
                    },
                    {
                        "face_eye_shirome_l", new MmoPsdMetadata
                        {
                            SourceLabel = "face_eye_shirome_l",
                            Category = "Emotion",
                            PsdGroup = "Eye-L",
                            Label = "EyeWhite-L",
                        }
                    },
                    {
                        "face_eye_shirome_r", new MmoPsdMetadata
                        {
                            SourceLabel = "face_eye_shirome_r",
                            Category = "Emotion",
                            PsdGroup = "Eye-R",
                            Label = "EyeWhite-R",
                        }
                    },
                    {
                        "face_eyebrow_l", new MmoPsdMetadata
                        {
                            SourceLabel = "face_eyebrow_l",
                            Category = "Emotion",
                            PsdGroup = "Eyebrow-L",
                            Label = "Eyebrow-L",
                        }
                    },
                    {
                        "face_eyebrow_r", new MmoPsdMetadata
                        {
                            SourceLabel = "face_eyebrow_r",
                            Category = "Emotion",
                            PsdGroup = "Eyebrow-R",
                            Label = "Eyebrow-R",
                        }
                    },
                    {
                        "face_mouth", new MmoPsdMetadata
                        {
                            SourceLabel = "face_mouth",
                            Category = "Emotion",
                            PsdGroup = "Mouth",
                            Label = "Mouth",
                        }
                    },
            }
            : new Dictionary<string, MmoPsdMetadata>();

            #endregion
        }

        /// <summary>
        /// Generate MMO from EMT KRKR PSB
        /// <para>When this method is called, the PSB you passed in can NO longer be used.</para>
        /// </summary>
        /// <param name="psb"></param>
        /// <returns></returns>
        public PSB Build(PSB psb)
        {
            Mmo = new PSB {Type = PsbType.Mmo};
            //Type initializer is tempting but we can't use it since the later object is using the former one
            Mmo.Objects = new PsbDictionary();
            Mmo.Objects["bgChildren"] = BuildBackground();
            Mmo.Objects["comment"] = psb.Objects["comment"] ?? "Built by FreeMote, wdwxy12345@gmail.com".ToPsbString();
            Mmo.Objects["defaultFPS"] = 60.ToPsbNumber();
            Mmo.Objects["fontInfoIdCount"] = PsbNull.Null;
            Mmo.Objects["fontInfoList"] = new PsbList(0);
            Mmo.Objects["forceRepack"] = 1.ToPsbNumber();
            Mmo.Objects["ignoreMotionPanel"] = PsbNumber.Zero;
            Mmo.Objects["keepSourceIconName"] = PsbNumber.Zero;
            Mmo.Objects["label"] = "FreeMote".ToPsbString();
            Mmo.Objects["marker"] = PsbNumber.Zero;
            Mmo.Objects["maxTextureSize"] = BuildMaxTextureSize(psb);
            Mmo.Objects["metadata"] = BuildMetadata(psb);
            Mmo.Objects["modelScale"] = 32.ToPsbNumber();
            Mmo.Objects["newScrapbookCellHeight"] = 8.ToPsbNumber();
            Mmo.Objects["newScrapbookCellWidth"] = 8.ToPsbNumber();
            Mmo.Objects["newTextureCellHeight"] = 8.ToPsbNumber();
            Mmo.Objects["newTextureCellWidth"] = 8.ToPsbNumber();
            Mmo.Objects["optimizeMargin"] = 1.ToPsbNumber();
            Mmo.Objects["outputDepth"] = PsbNumber.Zero;
            Mmo.Objects["previewSize"] = FillDefaultPreviewSize();
            Mmo.Objects["projectType"] = PsbNumber.Zero;
            Mmo.Objects["saveFormat"] = PsbNumber.Zero;
            Mmo.Objects["stereovisionProfile"] = psb.Objects["stereovisionProfile"];
            Mmo.Objects["targetOwn"] = FillDefaultTargetOwn();
            Mmo.Objects["unifyTexture"] = 1.ToPsbNumber();
            Mmo.Objects["version"] = new PsbNumber(3.12f);
            Mmo.Objects["objectChildren"] = BuildObjects(psb, out var rawPartsList, out var charaProfileList);
            Mmo.Objects["sourceChildren"] = BuildSources(psb);
            Mmo.Objects["metaformat"] = BuildMetaFormat(psb, Mmo, rawPartsList, charaProfileList);
            //1.ToPsbNumber();
            //mmo.Objects["uniqId"] = 114514.ToPsbNumber();

            //put to last since it's using obj & src children

            return Mmo;
        }

        /// <summary>
        /// Build from PSB source. Currently only works for krkr PSB
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="widthPadding"></param>
        /// <param name="heightPadding"></param>
        /// <returns></returns>
        private IPsbValue BuildSources(PSB psb, int widthPadding = 10, int heightPadding = 10)
        {
            var bitmaps = new Dictionary<uint, Bitmap>();
            var sourceChildren = new PsbList();
            foreach (var motionItemKv in (PsbDictionary)psb.Objects["source"])
            {
                var motionItem = new PsbDictionary();
                var item = (PsbDictionary)motionItemKv.Value;
                var icon = (PsbDictionary)item["icon"];
                var isTexture = icon.Values.Any(d => d is PsbDictionary dic && dic.ContainsKey("attr"));
                motionItem["label"] = motionItemKv.Key.ToPsbString();
                motionItem["comment"] = PsbString.Empty;
                motionItem["metadata"] = FillDefaultMetadata();
                motionItem["outputDepth"] = PsbNumber.Zero;
                motionItem["systemLock"] = PsbNumber.Zero;
                motionItem["resolution"] = 1.ToPsbNumber();
                motionItem["marker"] = isTexture ? MmoMarkerColor.Green.ToPsbNumber() : MmoMarkerColor.Blue.ToPsbNumber();
                if (isTexture) //Texture
                {
                    motionItem["className"] = "TextureItem".ToPsbString();
                    var iconList = new PsbList(icon.Count);
                    motionItem["iconList"] = iconList;
                    var texs = new Dictionary<string, Image>(icon.Count);
                    var texsOrigin = new Dictionary<string, (int oriX, int oriY, int width, int height)>(icon.Count);
                    foreach (var iconKv in icon)
                    {
                        var iconItem = (PsbDictionary)iconKv.Value;
                        iconItem["label"] = iconKv.Key.ToPsbString();
                        iconItem["metadata"] = FillDefaultMetadata();
                        iconItem["comment"] = PsbString.Empty;
                        if (!iconItem.ContainsKey("resolution"))
                        {
                            iconItem["resolution"] = 1.ToPsbNumber();
                        }
                        var height = ((PsbNumber)iconItem["height"]).AsInt;
                        var width = ((PsbNumber)iconItem["width"]).AsInt;
                        var originX = ((PsbNumber)iconItem["originX"]).AsInt;
                        var originY = ((PsbNumber)iconItem["originY"]).AsInt;
                        var (realWidth, realHeight) =
                            ExpandClipArea((PsbDictionary)iconItem["clip"], width, height);
                        texsOrigin.Add(iconKv.Key, (originX, originY, realWidth, realHeight));
                        var rl = iconItem["compress"] is PsbString s && s.Value.ToUpperInvariant() == "RL";
                        var res = (PsbResource)iconItem["pixel"];
                        Bitmap bmp = null;
                        if (res.Index == null)
                        {
                            throw new ArgumentNullException("Index", "PsbResource.Index can't be null at this time.");
                        }
                        if (bitmaps.ContainsKey(res.Index.Value))
                        {
                            bmp = bitmaps[res.Index.Value];
                        }
                        else
                        {
                            bmp = rl
                                ? RL.DecompressToImage(res.Data, height, width, psb.Platform.DefaultPixelFormat())
                                : RL.ConvertToImage(res.Data, height, width, psb.Platform.DefaultPixelFormat());
                            bitmaps.Add(res.Index.Value, bmp);
                        }
                        texs.Add(iconKv.Key, bmp);
                        iconItem.Remove("compress");
                        iconItem.Remove("attr");
                        iconList.Add(iconItem);
                    }
                    var packer = new TexturePacker();
                    var texture = packer.CellProcess(texs, texsOrigin, widthPadding, heightPadding, out var cellWidth,
                        out var cellHeight);
                    motionItem["image"] = BuildSourceImage(texture);
                    foreach (var iconKv in icon)
                    {
                        var iconItem = (PsbDictionary)iconKv.Value;
                        var node = packer.Atlasses[0].Nodes.Find(n => n.Texture.Source == iconKv.Key);
                        iconItem["left"] = node.Bounds.Left.ToPsbNumber();
                        iconItem["top"] = node.Bounds.Top.ToPsbNumber();
                        iconItem["originX"] = (node.Bounds.Width / 2).ToPsbNumber();
                        iconItem["originY"] = (node.Bounds.Height / 2).ToPsbNumber();
                        iconItem["width"] = (node.Bounds.Width).ToPsbNumber();
                        iconItem["height"] = (node.Bounds.Height).ToPsbNumber();
                        iconItem.Remove("pixel");
                    }

                    motionItem["cellWidth"] = cellWidth.ToPsbNumber();
                    motionItem["cellHeight"] = cellHeight.ToPsbNumber();
                }
                else //Scrapbook
                {
                    motionItem["cellHeight"] = 8.ToPsbNumber();
                    motionItem["cellWidth"] = 8.ToPsbNumber();
                    motionItem["className"] = "ScrapbookItem".ToPsbString();
                    var iconList = new PsbList(icon.Count);
                    motionItem["iconList"] = iconList;
                    foreach (var iconKv in icon)
                    {
                        var iconItem = (PsbDictionary)iconKv.Value;
                        iconItem["label"] = iconKv.Key.ToPsbString();
                        iconItem["metadata"] = FillDefaultMetadata();
                        iconItem["comment"] = PsbString.Empty;
                        if (!iconItem.ContainsKey("resolution"))
                        {
                            iconItem["resolution"] = 1.ToPsbNumber();
                        }
                        var height = ((PsbNumber)iconItem["height"]).AsInt;
                        var width = ((PsbNumber)iconItem["width"]).AsInt;
                        var rl = iconItem["compress"] is PsbString s && s.Value.ToUpperInvariant() == "RL";
                        var res = (PsbResource)iconItem["pixel"];
                        var texture = rl
                            ? RL.DecompressToImage(res.Data, height, width, psb.Platform.DefaultPixelFormat())
                            : RL.ConvertToImage(res.Data, height, width, psb.Platform.DefaultPixelFormat());

                        iconItem["image"] = BuildSourceImage(texture);
                        iconItem.Remove("compress");
                        iconItem.Remove("pixel");
                        iconList.Add(iconItem);
                    }
                }

                sourceChildren.Add(motionItem);
            }

            foreach (var bitmap in bitmaps.Values)
            {
                bitmap?.Dispose();
            }

            return sourceChildren;
        }

        private static (int width, int height) ExpandClipArea(PsbDictionary clip, int width, int height)
        {
            if (clip == null)
            {
                return (width, height);
            }
            var top = ((PsbNumber)clip["top"]).AsDouble;
            var bottom = ((PsbNumber)clip["bottom"]).AsDouble;
            var left = ((PsbNumber)clip["left"]).AsDouble;
            var right = ((PsbNumber)clip["right"]).AsDouble;

            return ((int)(width / (bottom - top)), (int)(height / (right - left)));
        }

        private PsbDictionary BuildSourceImage(Bitmap pixel, int type = 2)
        {
            var image = new PsbDictionary(2)
            {
                ["data"] = new PsbDictionary()
            {
                {"bitCount", 32.ToPsbNumber()},
                {"compress", "RL".ToPsbString()},
                {"height", pixel.Height.ToPsbNumber()},
                {"id", "rgbabitmap".ToPsbString()},
                {"pixel", new PsbResource {Data = RL.CompressImage(pixel, PsbPixelFormat.LeRGBA8)}},
                {"width", pixel.Width.ToPsbNumber()},
            },
                ["type"] = type.ToPsbNumber()
            };
            return image;
        }

        /// <summary>
        /// Build `objectChildren` from PSB `object`
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="partsList"></param>
        /// <param name="charaProfileList"></param>
        /// <returns></returns>
        private IPsbValue BuildObjects(PSB psb, out PsbList partsList, out PsbList charaProfileList)
        {
            //Dictionary<string, List<string>> disableFeatures = new Dictionary<string, List<string>>();
            var enableFeatures = new Dictionary<string, string>();
            var newPartsList = new PsbList();
            var newCharaProfileList = new PsbList();

            var objectChildren = new PsbList {Parent = Mmo.Objects};
            foreach (var motionItemKv in (PsbDictionary)psb.Objects["object"])
            {
                var motionItem = (PsbDictionary)motionItemKv.Value;
                var objectChildrenItem = new PsbDictionary();
                objectChildrenItem.Parent = objectChildren;
                objectChildrenItem["label"] = motionItemKv.Key.ToPsbString();
                objectChildrenItem["className"] = "CharaItem".ToPsbString();
                objectChildrenItem["comment"] = PsbString.Empty;
                objectChildrenItem["defaultCoordinate"] = PsbNumber.Zero;
                objectChildrenItem["marker"] = PsbNumber.Zero;
                objectChildrenItem["metadata"] = motionItem["metadata"] is PsbNull ? FillDefaultMetadata() : motionItem["metadata"];
                var motion = (PsbDictionary)motionItem["motion"];
                objectChildrenItem["children"] = BuildChildrenFromMotion(motion, objectChildrenItem);
                objectChildrenItem["templateReferenceChara"] = PsbString.Empty;
                objectChildrenItem["templateSourceMap"] = new PsbDictionary(0);
                //objectChildrenItem["uniqId"] = 4396;

                objectChildren.Add(objectChildrenItem);
            }

            partsList = newPartsList;
            charaProfileList = newCharaProfileList;

            #region Local Functions

            PsbList BuildChildrenFromMotion(PsbDictionary dic, IPsbCollection parent)
            {
                var objectChildren_children = new PsbList {Parent = parent};
                foreach (var motionItemKv in dic)
                {
                    var motionItem = (PsbDictionary)motionItemKv.Value;
                    var objectChildrenItem = new PsbDictionary();
                    objectChildrenItem.Parent = objectChildren_children;
                    objectChildrenItem["className"] = "MotionItem".ToPsbString();
                    objectChildrenItem["comment"] = PsbString.Empty;
                    objectChildrenItem["exportBounds"] = PsbNumber.Zero;
                    objectChildrenItem["exportSelf"] = 1.ToPsbNumber();
                    objectChildrenItem["forcePreviewLoop"] = PsbNumber.Zero;
                    objectChildrenItem["fps"] = 60.ToPsbNumber();
                    objectChildrenItem["isDelivered"] = PsbNumber.Zero;
                    objectChildrenItem["label"] = motionItemKv.Key.ToPsbString();
                    objectChildrenItem["lastTime"] = BuildLastTime(motionItem["lastTime"]);
                    objectChildrenItem["loopBeginTime"] = motionItem["loopTime"]; //TODO: loop
                    objectChildrenItem["loopEndTime"] = motionItem["loopTime"]; //currently begin = end = -1
                    objectChildrenItem["marker"] = PsbNumber.Zero;
                    objectChildrenItem["metadata"] = motionItem["metadata"] is PsbNull ? FillDefaultMetadata() : motionItem["metadata"]; //TODO: should we set all to default?
                    var parameter = (PsbList)motionItem["parameter"];
                    objectChildrenItem["parameterize"] = motionItem["parameterize"] is PsbNull
                        ? FillDefaultParameterize()
                        : parameter[((PsbNumber)motionItem["parameterize"]).IntValue];
                    objectChildrenItem["priorityFrameList"] = BuildPriorityFrameList((PsbList)motionItem["priority"]);
                    objectChildrenItem["referenceModelFileList"] = motionItem["referenceModelFileList"];
                    objectChildrenItem["referenceProjectFileList"] = motionItem["referenceProjectFileList"];
                    objectChildrenItem["streamed"] = PsbNumber.Zero;
                    objectChildrenItem["tagFrameList"] = motionItem["tag"];
                    //objectChildrenItem["uniqId"] = 1551;
                    objectChildrenItem["variableChildren"] = motionItem["variable"];
                    var layer = (PsbList)motionItem["layer"];
                    layer.Parent = objectChildrenItem;
                    BuildLayerChildren(layer, parameter);
                    objectChildrenItem["layerChildren"] = motionItem["layer"];

                    objectChildren_children.Add(objectChildrenItem);
                }

                return objectChildren_children;
            }

            void BuildLayerChildren(IPsbCollection child, PsbList parameter)
            {
                if (child is PsbList col)
                {
                    foreach (var c in col)
                    {
                        if (c is IPsbCollection cchild)
                        {
                            BuildLayerChildren(cchild, parameter);
                        }
                    }
                }
                else if (child is PsbDictionary dic)
                {
                    //ClassName
                    var typeNum = dic["type"] as PsbNumber;
                    var classType = MmoItemClass.ObjLayerItem;
                    if (typeNum != null)
                    {
                        classType = (MmoItemClass)typeNum.IntValue;
                    }

                    dic["className"] = classType.ToString().ToPsbString();
                    dic["comment"] = PsbString.Empty;

                    //Build frameList
                    MmoFrameMask frameMask = 0;
                    MmoFrameMaskEx frameMaskEx = 0;
                    List<string> motionRefs = null;
                    if (dic["frameList"] is PsbList frameList)
                    {
                        BuildFrameList(frameList, classType, out frameMask, out frameMaskEx, out motionRefs);
                    }

                    //parameterize: find from psb table and expand
                    string param = null;
                    if (dic["parameterize"] is PsbNumber parameterizeId && parameterizeId.IntValue >= 0)
                    {
                        dic["parameterize"] = parameter[parameterizeId.IntValue];
                        param = dic["parameterize"].Children("id").ToString();
                    }
                    else
                    {
                        dic["parameterize"] = FillDefaultParameterize();
                    }

                    //Disable features
                    if (!string.IsNullOrEmpty(param) && motionRefs != null && motionRefs.Count > 0)
                    {
                        for (var i = 0; i < motionRefs.Count; i++)
                        {
                            motionRefs[i] = motionRefs[i].Substring(motionRefs[i].IndexOf('/') + 1);
                        }
                    }

                    //stencilType conversion: 5 (psb) -> 1 (mmo)
                    if (dic["stencilType"] is PsbNumber stencilType)
                    {
                        if (stencilType.IntValue == 5)
                        {
                            dic["stencilType"] = 1.ToPsbNumber();
                        }
                    }

                    if (dic["metadata"] is PsbNull)
                    {
                        dic["metadata"] = new PsbDictionary(2) //metadata: data is string => type0; data is null?dictionary => type1
                        {
                            {"data", PsbString.Empty },
                            {"type", PsbNumber.Zero },
                        };
                    }
                    else if (dic["metadata"] is PsbString s)
                    {
                        dic["metadata"] = new PsbDictionary(2)
                        {
                            {"data", s },
                            {"type", PsbNumber.Zero },
                        };
                    }

                    //Expand meshSyncChildMask
                    if (dic["meshSyncChildMask"] is PsbNumber number)
                    {
                        dic["meshSyncChildShape"] = (number.IntValue & 8) == 8 ? 1.ToPsbNumber() : PsbNumber.Zero;
                        //project version
                        dic["meshSyncChildZoom"] = (number.IntValue & 4) == 4 ? 1.ToPsbNumber() : PsbNumber.Zero;
                        dic["meshSyncChildAngle"] = (number.IntValue & 2) == 2 ? 1.ToPsbNumber() : PsbNumber.Zero;
                        
                        dic["meshSyncChildCoord"] = (number.IntValue & 1) == 1 ? 1.ToPsbNumber() : PsbNumber.Zero;
                    }
                    else
                    {
                        dic["meshSyncChildShape"] = PsbNumber.Zero;
                        dic["meshSyncChildZoom"] = PsbNumber.Zero;
                        dic["meshSyncChildAngle"] = PsbNumber.Zero;
                        dic["meshSyncChildCoord"] = PsbNumber.Zero;
                    }

                    //if (!dic.ContainsKey("meshSyncChildAngle"))
                    //{
                    //    dic["meshSyncChildAngle"] = PsbNumber.Zero;
                    //}
                    //if (!dic.ContainsKey("meshSyncChildZoom"))
                    //{
                    //    dic["meshSyncChildZoom"] = PsbNumber.Zero;
                    //}

                    //Expand inheritMask
                    //inheritAngle:         16  10000
                    //inheritParent:
                    //inheritColorWeight:   512 1000000000
                    if (dic["inheritMask"] is PsbNumber inheritMask)
                    {
                        var mask = Convert.ToString(inheritMask.IntValue, 2).PadLeft(32, '0');
                        dic["inheritShape"] = mask[6] == '1' ? 1.ToPsbNumber() : PsbNumber.Zero;
                        dic["inheritParent"] = mask[9] == '1' ? 1.ToPsbNumber() : PsbNumber.Zero;
                        dic["inheritOpacity"] = mask[21] == '1' ? 1.ToPsbNumber() : PsbNumber.Zero;
                        dic["inheritColorWeight"] = mask[22] == '1' ? 1.ToPsbNumber() : PsbNumber.Zero;
                        dic["inheritSlantY"] = mask[23] == '1' ? 1.ToPsbNumber() : PsbNumber.Zero;
                        dic["inheritSlantX"] = mask[24] == '1' ? 1.ToPsbNumber() : PsbNumber.Zero;
                        dic["inheritZoomY"] = mask[25] == '1' ? 1.ToPsbNumber() : PsbNumber.Zero;
                        dic["inheritZoomX"] = mask[26] == '1' ? 1.ToPsbNumber() : PsbNumber.Zero;
                        dic["inheritAngle"] = mask[27] == '1' ? 1.ToPsbNumber() : PsbNumber.Zero;
                        dic["inheritFlipY"] = mask[28] == '1' ? 1.ToPsbNumber() : PsbNumber.Zero;
                        dic["inheritFlipX"] = mask[29] == '1' ? 1.ToPsbNumber() : PsbNumber.Zero;
                    }

                    if (classType == MmoItemClass.ShapeLayerItem)
                    {
                        if (dic["shape"] is PsbNumber shape)
                        {
                            dic["shape"] = shape.ToShapeString().ToPsbString();
                        }

                        dic.Remove("type"); //FIXED: the "type" here will be misunderstand for "point" type so must be removed
                        dic.Remove("inheritMask");
                    }

                    if (classType == MmoItemClass.ParticleLayerItem)
                    {
                        //All params are kept in PSB
                        if (dic["particle"] is PsbNumber particle)
                        {
                            switch (particle.IntValue)
                            {
                                case 0:
                                    dic["particle"] = "point".ToPsbString();
                                    break;
                                case 1:
                                    dic["particle"] = "ellipse".ToPsbString();
                                    break;
                                case 2:
                                    dic["particle"] = "quad".ToPsbString();
                                    break;
                                default:
                                    Console.WriteLine("[WARN] unknown particle!");
                                    break;
                            }
                        }
                    }

                    if (classType == MmoItemClass.TextLayerItem)
                    {
                        //TODO: Haven't seen any sample with Text
                    }

                    //other
                    FillDefaultsIntoChildren(dic, classType);

                    //build charaProfile
                    if (classType == MmoItemClass.LayoutLayerItem)
                    {
                        PsbDictionary charaProfile = null;
                        switch (dic["label"].ToString())
                        {
                            case "目L_le":
                                charaProfile = BuildCharaProfileItem("eye", "Eye", dic.GetMmoPath());
                                break;
                            case "口_le":
                                charaProfile = BuildCharaProfileItem("mouth", "Mouth", dic.GetMmoPath());
                                break;
                            case "胸_le":
                                charaProfile = BuildCharaProfileItem("bust", "Bust", dic.GetMmoPath());
                                break;
                            case "胴体_le":
                                charaProfile = BuildCharaProfileItem("body", "Body", dic.GetMmoPath());
                                break;
                        }

                        if (charaProfile != null)
                        {
                            newCharaProfileList.Add(charaProfile);
                        }
                    }

                    //build PartsList
                    //[MenuPath, Feature, CharaItem, Motion, Layer, ""]
                    if (classType == MmoItemClass.ObjLayerItem || classType == MmoItemClass.MotionLayerItem || classType == MmoItemClass.LayoutLayerItem || classType == MmoItemClass.MeshLayerItem)
                    {
                        var path = dic.GetMmoPath();
                        path = path.Substring(path.IndexOf('/') + 1);
                        var paths = path.Split('/');
                        var menuPath = InferDefaultPart(paths[0], path);
                        //Infer Feature
                        var features = InferFeatures(frameMask, frameMaskEx, classType);

                        if (!string.IsNullOrEmpty(param))
                        {
                            var parentPaths = enableFeatures.Where(kv => path.StartsWith(kv.Key));
                            if (parentPaths.Any(kv => kv.Value == param))
                            {
                                features.Remove("メッシュ");
                            }
                            else
                            {
                                enableFeatures[path] = param;
                            }

                            foreach (var motionRef in motionRefs)
                            {
                                enableFeatures[motionRef] = param;
                                var rem = newPartsList.Where(p =>
                                    p is PsbList c && string.Join("/",
                                            new[] { c[2].ToString(), c[3].ToString(), c[4].ToString() })
                                        .StartsWith(motionRef) && c[1].ToString().StartsWith("メッシュ")).ToList();
                                foreach (var r in rem)
                                {
                                    newPartsList.Remove(r);
                                }
                            }
                        }

                        foreach (var feature in features)
                        {
                            newPartsList.Add(new PsbList(6)
                            {
                                menuPath.ToPsbString(),
                                feature.ToPsbString(),
                                paths[0].ToPsbString(),
                                paths[1].ToPsbString(),
                                string.Join("/",paths.Skip(2)).ToPsbString(),
                                PsbString.Empty
                                //string.IsNullOrEmpty(param)? PsbString.Empty : param.ToPsbString()
                                //$"{FillDefaultCategory(pathList[0])}".ToPsbString()
                            });
                        }
                    }

                    //build children
                    if (dic["children"] is PsbList children)
                    {
                        BuildLayerChildren(children, parameter);
                    }
                }

            }

            PsbDictionary BuildCharaProfileItem(string id, string label, string path)
            {
                var paths = path.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
                var l = new PsbDictionary(3)
                {
                    {"chara", paths[1].ToPsbString()},
                    {"motion", paths[2].ToPsbString()},
                    {"layers", new PsbList(1){string.Join("/", paths.Skip(3)).ToPsbString()}}
                };
                return new PsbDictionary(3)
                {
                    {"id",  id.ToPsbString()},
                    {"label",  label.ToPsbString()},
                    {"layer", l}
                };
            }

            #endregion

            return objectChildren;
        }

        private HashSet<string> InferFeatures(MmoFrameMask frameMask, MmoFrameMaskEx frameMaskEx, MmoItemClass classType)
        {
            // "///^(透過表示|ビュー|レイアウト|レイアウト角度|角度|XY座標|XY座標角度|Z座標|メッシュ|パーティクル|削除|ブレンドモード)\\((.+)\\)$"
            // "///^(Transparent|View|Layout|Layout角度|角度|XY座標|XY座標角度|Z座標|Mesh|Particle|Remove|BlendMode)\\((.+)\\)$"
            var features = new HashSet<string>();

            //削除 impossible to get?
            if (classType == MmoItemClass.ObjLayerItem && frameMaskEx.HasFlag(MmoFrameMaskEx.SrcSrc))
            {
                features.Add("削除");
                features.Add("ブレンドモード");
            }

            if (frameMask == 0 || frameMask == (MmoFrameMask)1)
            {
                return features;
            }

            if (frameMask.HasFlag(MmoFrameMask.Opacity))
            {
                features.Add("透過表示");
            }
            //ビュー unknown
            if (frameMask.HasFlag(MmoFrameMask.Coord) && frameMaskEx.HasFlag(MmoFrameMaskEx.CoordXY) && frameMaskEx.HasFlag(MmoFrameMaskEx.CoordZ))
            {
                if (frameMask.HasFlag(MmoFrameMask.Angle))
                {
                    features.Add("レイアウト角度");
                }
                else
                {
                    features.Add("レイアウト");
                }
            }
            else
            {
                if (frameMaskEx.HasFlag(MmoFrameMaskEx.CoordXY))
                {
                    features.Add(frameMask.HasFlag(MmoFrameMask.Angle) ? "XY座標角度" : "XY座標");
                }
                if (frameMaskEx.HasFlag(MmoFrameMaskEx.CoordZ))
                {
                    if (frameMask.HasFlag(MmoFrameMask.Angle))
                    {
                        features.Add("角度");
                    }
                    features.Add("Z座標");
                }
            }
            if (frameMask.HasFlag(MmoFrameMask.Angle))
            {
                features.Add("角度");
            }
            if (frameMask.HasFlag(MmoFrameMask.Mesh) || frameMask.HasFlag(MmoFrameMask.Motion))
            {
                features.Add("メッシュ");
            }
            if (frameMask.HasFlag(MmoFrameMask.Particle))
            {
                features.Add("パーティクル");
            }
            if (frameMask.HasFlag(MmoFrameMask.BlendMode))
            {
                features.Add("ブレンドモード");
            }

            //won't happen
            //if (classType == MmoItemClass.LayoutLayerItem && frameMaskEx.HasFlag(MmoFrameMaskEx.SrcSrc))
            //{
            //    features.Add("レイアウト角度");
            //    features.Remove("レイアウト");
            //}

            return features;
        }

        private string InferDefaultPart(string part, string path)
        {
            foreach (var kv in CustomPartMenuPaths)
            {
                if (path.Contains(kv.Key))
                {
                    return kv.Value;
                }
            }
            if (path.Contains("スカート"))
            {
                return "胴体/スカート";
            }
            if (path.Contains("追加パーツ"))
            {
                return "追加パーツ";
            }
            if (path.Contains("目L") || path.Contains("瞳L") || path.Contains("涙L"))
            {
                return "表情/目L";
            }
            if (path.Contains("目R") || path.Contains("瞳R") || path.Contains("涙R"))
            {
                return "表情/目R";
            }
            if (path.Contains("眉L"))
            {
                return "表情/眉L";
            }
            if (path.Contains("眉R"))
            {
                return "表情/眉R";
            }
            if (path.Contains("口"))
            {
                return "表情/口";
            }
            if (path.Contains("鼻"))
            {
                return "表情/鼻";
            }
            if (path.Contains("前髪"))
            {
                return "頭部/前髪";
            }
            if (path.Contains("後髪"))
            {
                return "頭部/後髪";
            }
            if (path.Contains("腕L"))
            {
                return "胴体/腕L";
            }
            if (path.Contains("腕R"))
            {
                return "胴体/腕R";
            }
            if (path.Contains("胸"))
            {
                return "胴体/胸";
            }
            if (path.Contains("胴体回転中心") && path.Contains("胴体調整"))
            {
                return "胴体/胴体全体";
            }
            if (path.Contains("胴体"))
            {
                return "胴体/胴体";
            }
            if (path.Contains("頭部") && path.Contains("輪郭"))
            {
                return "頭部/輪郭";
            }
            if (path.Contains("胴体回転中心") && path.Contains("頭部調整"))
            {
                return "頭部/頭部全体";
            }
            if (path.EndsWith("背景") || path.EndsWith("背景_le"))
            {
                return "背景"; // 全体/背景 is going to fail if there are only one item in it
            }
            return "全体";
        }

        private static IPsbValue BuildLastTime(IPsbValue val)
        {
            //TODO: 目L：61 in krkr vs -1 in MMO
            if (val is PsbNumber num)
            {
                if (num.IntValue >= 0)
                {
                    num.IntValue -= 1;
                }

                return num;
            }

            return val;
        }

        private PsbList BuildPriorityFrameList(PsbList fl)
        {
            for (var i = 0; i < fl.Count; i++)
            {
                var flItem = fl[i];
                if (flItem is PsbDictionary dic)
                {
                    if (!dic.ContainsKey("content"))
                    {
                        fl.Remove(flItem);
                    }
                    else
                    {
                        if (dic["content"] is PsbNull)
                        {
                            fl.Remove(flItem);
                        }
                    }
                }
            }

            return fl;
        }

        private void BuildFrameList(PsbList frameList, MmoItemClass classType, out MmoFrameMask mask, out MmoFrameMaskEx maskEx, out List<string> motionRefs)
        {
            mask = 0;
            maskEx = 0;
            motionRefs = new List<string>();
            foreach (var fl in frameList)
            {
                if (fl is PsbDictionary dic)
                {
                    if (!dic.ContainsKey("content"))
                    {
                        dic.Add("content", PsbNull.Null);
                    }
                    else if (dic["content"] is PsbDictionary content)
                    {
                        if (content.ContainsKey("mask")) //Expand params from mask
                        {
                            if (content["mask"] is PsbNumber num)
                            {
                                mask = (MmoFrameMask)num.IntValue; //TODO: motion/timeOffset is special or not?
                            }

                            if (content["coord"] is PsbList col)
                            {
                                if (col[0] is PsbNumber x && x.IntValue != 0)
                                {
                                    maskEx |= MmoFrameMaskEx.CoordXY;
                                }
                                else if (col[1] is PsbNumber y && y.IntValue != 0)
                                {
                                    maskEx |= MmoFrameMaskEx.CoordXY;
                                }
                                if (col[2] is PsbNumber z && z.IntValue != 0)
                                {
                                    maskEx |= MmoFrameMaskEx.CoordZ;
                                }
                            }

                            //Low to High:
                            //0: ox,oy
                            //1: coord
                            //4: angle
                            //5,6: zx,zy
                            //9: color
                            //10: opa
                            //17: bm
                            //19: motion/timeOffset?
                            //25: mesh

                            if (content["src"] is PsbString s)
                            {
                                if (s.Value.StartsWith("motion/"))
                                {
                                    maskEx |= MmoFrameMaskEx.SrcMotion;
                                    motionRefs.Add(s.Value);
                                }
                                else if (s.Value.StartsWith("src/"))
                                {
                                    maskEx |= MmoFrameMaskEx.SrcSrc;
                                }
                                else if (s.Value.StartsWith("shape/"))
                                {
                                    maskEx |= MmoFrameMaskEx.SrcShape;
                                }
                            }

                            //if (content["src"] is PsbString s && s.Value.StartsWith("shape/"))
                            //{
                            //    content.Remove("mask"); //necessary to prevent Member "point" does not exist error
                            //}
                        }

                        if (content.ContainsKey("color"))
                        {
                            var colorObj = content["color"];
                            if (colorObj is PsbNumber num) //Expand Color
                            {
                                content["color"] = new PsbList(4) { num, num, num, num };
                            }
                        }

                        var hasMotion = false;

                        if (content.ContainsKey("motion"))
                        {
                            hasMotion = true;
                            var motion = (PsbDictionary)content["motion"];
                            if (motion.ContainsKey("timeOffset"))
                            {
                                content["mdofst"] = motion["timeOffset"];
                            }
                        }

                        if (content.ContainsKey("mesh")) //25
                        {
                            content["mbp"] = content["mesh"].Children("bp");
                            content["mcc"] = content["mesh"].Children("cc");
                        }

                        var hasStencil = classType == MmoItemClass.StencilLayerItem;
                        FillDefaultsIntoFrameListContent(content, hasMotion, hasStencil);
                    }
                }
            }
        }

        /// <summary>
        /// Essential for normal Editor
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="mmo"></param>
        /// <param name="partList"></param>
        /// <param name="charaProfileList"></param>
        /// <returns></returns>
        private PsbDictionary BuildMetaFormat(PSB psb, PSB mmo, PsbList partList, PsbList charaProfileList)
        {
            var jsonConverter = new PsbJsonConverter();
            PsbDictionary mmoRef = null;
            if (DebugMode && File.Exists("mmo.json"))
            {
                mmoRef = JsonConvert.DeserializeObject<PsbDictionary>(File.ReadAllText("mmo.json"),
                   jsonConverter);
            }
            else
            {
                mmoRef = JsonConvert.DeserializeObject<PsbDictionary>(Resources.Mmo,
                    jsonConverter);
            }

            var metaFormatContent = new PsbDictionary();
            var metaFormat = new PsbDictionary()
            {
                {"data", metaFormatContent },
                {"type", 1.ToPsbNumber() }
            };
            //return new PsbDictionary
            //{
            //    {"data", "by Ulysses, wdwxy12345@gmail.com".ToPsbString() },
            //    {"type", PsbNumber.Zero }
            //};

            var metadata = (PsbDictionary)psb.Objects["metadata"];
            if (metadata["base"] is PsbDictionary baseDic)
            {
                var baseChara = baseDic.Children("chara");
                if (baseChara != null && !(baseChara is PsbNull))
                {
                    metaFormatContent["baseChara"] = metadata["base"].Children("chara");
                }

                var baseMotion = baseDic.Children("motion");
                if (baseMotion != null && !(baseMotion is PsbNull))
                {
                    metaFormatContent["baseMotion"] = metadata["base"].Children("motion");
                }
            }

            metaFormatContent["bustControlDefinitionList"] = metadata["bustControl"];
            metaFormatContent["bustControlParameterDefinitionList"] = mmoRef["bustControlParameterDefinitionList"];
            metaFormatContent["captureList"] = new PsbList
            {
                new PsbDictionary
                {
                    {"chara", metaFormatContent["baseChara"] },
                    {"height", 600.ToPsbNumber()},
                    {"label", "FreeMote".ToPsbString() },
                    {"motion", metaFormatContent["baseMotion"] },
                    {"scale", new PsbNumber(0.5f) },
                    {"testChara", PsbString.Empty },
                    {"testMotion", PsbString.Empty },
                    {"width", 600.ToPsbNumber() }
                }
            };
            metaFormatContent["charaProfileDefinitionList"] = charaProfileList;
            metaFormatContent["clampControlDefinitionList"] = metadata["clampControl"];
            metaFormatContent["customPartsBaseDefinitionList"] = new PsbList(); //custom parts template
            metaFormatContent["customPartsCount"] = 99.ToPsbNumber(); //((PsbList)metadata["customPartsOrder"]).Count.ToPsbNumber(); //PsbNumber.Zero;
            metaFormatContent["customPartsDefinitionList"] = new PsbList(); //empty
            metaFormatContent["customPartsMountDefinitionList"] = new PsbList(); //mount to current objects
            metaFormatContent["eyeControlDefinitionList"] = metadata["eyeControl"];
            metaFormatContent["eyeControlParameterDefinitionList"] = mmoRef["eyeControlParameterDefinitionList"];
            metaFormatContent["eyebrowControlDefinitionList"] = metadata["eyebrowControl"];
            metaFormatContent["guideCount"] = PsbNumber.Zero;
            metaFormatContent["hairControlDefinitionList"] = BuildControlDefinition((PsbList)metadata["hairControl"]);
            metaFormatContent["hairControlParameterDefinitionList"] = mmoRef["hairControlParameterDefinitionList"];
            metaFormatContent["clampControlDefinitionList"] = metadata["clampControl"];
            metaFormatContent["instantVariableList"] = metadata["instantVariableList"];
            metaFormatContent["layoutDefinitionList"] = new PsbList(); //can be null
            metaFormatContent["license"] = 5.ToPsbNumber();
            metaFormatContent["logo"] = metadata["logo"] ?? PsbNumber.Zero;
            metaFormatContent["loopControlDefinitionList"] = metadata["loopControl"];
            metaFormatContent["loopControlParameterDefinitionList"] = new PsbList(); //default is empty
            metaFormatContent["mirrorDefinition"] = metadata["mirrorControl"];
            metaFormatContent["mouthControlDefinitionList"] = metadata["mouthControl"];
            var (orbitControl, orbitParamDef) = BuildOrbitControlParameterDef(metadata["orbitControl"], (PsbList)mmoRef["orbitControlParameterDefinitionList"]);
            metaFormatContent["orbitControlDefinitionList"] = orbitControl; //TODO: we don't have sample with orbit
            metaFormatContent["orbitControlParameterDefinitionList"] = orbitParamDef;
            metaFormatContent["parameterEditDefinition"] = mmoRef["parameterEditDefinition"];
            metaFormatContent["partialExportDefinitionList"] = new PsbList();
            metaFormatContent["partsControlDefinitionList"] = BuildControlDefinition((PsbList)metadata["partsControl"]);
            metaFormatContent["partsControlParameterDefinitionList"] = mmoRef["partsControlParameterDefinitionList"];
            metaFormatContent["physicsMotionList"] = new PsbList();
            metaFormatContent["physicsVariableList"] = new PsbList();
            metaFormatContent["scrapbookDefinitionList"] = BuildScrapbookDefinition(mmo); //Have to build for change scrapbook
            metaFormatContent["selectorControlDefinitionList"] = metadata["selectorControl"];
            metaFormatContent["sourceDefinitionOrderList"] = new PsbList(); //can be null?
            metaFormatContent["stereovisionDefinition"] = metadata["stereovisionControl"];
            metaFormatContent["subtype"] = "E-mote Meta Format".ToPsbString();
            metaFormatContent["testAnimationList"] = new PsbList();
            metaFormatContent["textureDefinitionList"] = BuildTextureDefinition(mmo); //Have to build for change texture
            metaFormatContent["transitionControlDefinitionList"] = metadata["transitionControl"];
            metaFormatContent["variableAliasFrameBind"] = new PsbDictionary();
            BuildVariableList((PsbList)metadata["variableList"], out var variableAlias, out var variableFrameAlias);
            metaFormatContent["variableAlias"] = variableAlias;
            metaFormatContent["variableFrameAlias"] = variableFrameAlias;
            metaFormatContent["variableFrameAliasUniq"] = new PsbDictionary();
            metaFormatContent["version"] = new PsbNumber(1.08f);
            metaFormatContent["windDefinitionList"] = mmoRef["windDefinitionList"];
            //put at last since it might use Variable list
            //Exposed layers: LayoutLayer, MeshLayer, ObjectLayer (w or w/o parameters)
            //[MenuPath, Desc, CharaItem, Motion, Layer, ""]
            //DeduplicatePartList(partList);
            metaFormatContent["partsList"] = partList; //Have to build for parameter feature

            return metaFormat;
        }

        private (PsbList orbitControl, PsbList orbitParamDef) BuildOrbitControlParameterDef(IPsbValue origin, PsbList refer)
        {
            if (origin == null || origin is PsbNull)
            {
                return (new PsbList(), refer);
            }

            var ori = (PsbList)origin;
            var newOrbitParamDef = new PsbList();
            var labels = new HashSet<string>();
            foreach (var o in ori)
            {
                //"comment": "60f間隔で0,30,60,90,120の順で周回する。",
                //"interval": 60,
                //"label": "12コマ順方向1秒",
                //"orbitFrameList": [30,60,90,120],
                //"tween": 1

                //{
                //    "comment": "",
                //    "enabled": 1,
                //    "label": "loop_b",
                //    "parameter": ""
                //}
                var item = (PsbDictionary)o;
                var dic = new PsbDictionary()
                {
                    {"comment", item["comment"] },
                    {"interval", item["interval"] },
                    {"label", item["label"] },
                    {"orbitFrameList", item["orbitFrameList"] },
                    {"tween", item["tween"] },
                };
                item.Remove("orbitFrameList");
                item.Remove("tween");
                newOrbitParamDef.Add(dic);
                labels.Add(item["label"].ToString());
            }

            foreach (var r in refer)
            {
                if (r is PsbDictionary rDic && !labels.Contains(rDic["label"].ToString()))
                {
                    newOrbitParamDef.Add(r);
                }
            }
            return (ori, newOrbitParamDef);
        }

        private IPsbValue BuildScrapbookDefinition(PSB mmo)
        {
            //var objectChildren = (PsbList)mmo.Objects["objectChildren"];
            var sourceChildren = (PsbList)mmo.Objects["sourceChildren"];
            var scrapDef = new PsbList();
            //We couldn't get complete metadata for this
            var scrapSources =
                sourceChildren.Where(s => s is PsbDictionary dic && dic["className"].ToString() == "ScrapbookItem").Cast<PsbDictionary>();
            foreach (var mmoItem in scrapSources)
            {
                foreach (var iconItem in (PsbList)mmoItem["iconList"])
                {
                    var iconDic = (PsbDictionary)iconItem;
                    var texDefItem = new PsbDictionary
                    {
                        {"sourceLabel", mmoItem["label"]},
                        {"comment", mmoItem["comment"]},
                        {"psdMargin", 5.ToPsbNumber()},
                        {"psdRange", 1.ToPsbNumber()},
                        {"iconLabel", iconDic["label"]},
                        {"meshList", new PsbList()},
                        //{"layoutFlags", 3.ToPsbNumber()},
                    };
                    var sLabel = iconDic["label"].ToString();
                    if (MmoPsdMetadatas.ContainsKey(sLabel))
                    {
                        var md = MmoPsdMetadatas[sLabel];
                        texDefItem.Add("category", new PsbList(1) { md.Category.ToPsbString() });
                        texDefItem.Add("psdGroup", md.PsdGroup.ToPsbString());
                        texDefItem.Add("psdFrameLabel", md.PsdFrameLabel.ToPsbString());
                        texDefItem.Add("psdComment", md.PsdComment.ToPsbString());
                        texDefItem.Add("label", md.Label.ToPsbString());
                    }
                    else
                    {
                        var category = FillDefaultCategory(mmoItem["label"].ToString());
                        texDefItem.Add("category", new PsbList(1) { category.ToPsbString() });
                        texDefItem.Add("psdGroup", mmoItem["label"]);
                        texDefItem.Add("psdFrameLabel", PsbString.Empty);
                        texDefItem.Add("psdComment", PsbString.Empty);
                        texDefItem.Add("label", iconDic["label"]);
                    }

                    scrapDef.Add(texDefItem);
                }
            }

            return scrapDef;
        }

        /// <summary>
        /// Infer default category from label
        /// </summary>
        /// <param name="label"></param>
        /// <returns></returns>
        private string FillDefaultCategory(string label)
        {
            //WARN: Category can not be named as `Expression`
            if (label.StartsWith("all_"))
            {
                return DebugMode ? "All" : "All";
            }
            if (label.StartsWith("face_"))
            {
                return DebugMode ? "Emotion" : "Emotion";
            }
            if (label.StartsWith("head_"))
            {
                return DebugMode ? "Head" : "Head";
            }
            if (label.StartsWith("body_"))
            {
                return DebugMode ? "Body" : "Body";
            }
            return DebugMode ? "Other" : "Other";
        }

        private IPsbValue BuildTextureDefinition(PSB mmo)
        {
            //var objectChildren = (PsbList)mmo.Objects["objectChildren"];
            var sourceChildren = (PsbList)mmo.Objects["sourceChildren"];
            var textureDef = new PsbList();
            //We couldn't get complete metadata for this
            var textureSources =
                sourceChildren.Where(s => s is PsbDictionary dic && dic["className"].ToString() == "TextureItem").Cast<PsbDictionary>();
            foreach (var mmoItem in textureSources)
            {
                var texDefItem = new PsbDictionary
                {
                    {"sourceLabel", mmoItem["label"]},
                    {"comment", mmoItem["comment"]},
                    {"psdMargin", 5.ToPsbNumber()},
                    {"psdRange", 1.ToPsbNumber()},
                    {"meshList", new PsbList()},
                    //{"layoutFlags", 3.ToPsbNumber()},
                };
                var sLabel = mmoItem["label"].ToString();
                if (MmoPsdMetadatas.ContainsKey(sLabel))
                {
                    var md = MmoPsdMetadatas[sLabel];
                    texDefItem.Add("category", new PsbList(1) { md.Category.ToPsbString() });
                    texDefItem.Add("psdGroup", md.PsdGroup.ToPsbString());
                    texDefItem.Add("psdFrameLabel", md.PsdFrameLabel.ToPsbString());
                    texDefItem.Add("psdComment", md.PsdComment.ToPsbString());
                    texDefItem.Add("label", md.Label.ToPsbString());
                }
                else
                {
                    var category = FillDefaultCategory(sLabel);
                    texDefItem.Add("category", new PsbList(1) { category.ToPsbString() });
                    texDefItem.Add("psdGroup", mmoItem["label"]);
                    texDefItem.Add("psdFrameLabel", PsbString.Empty);
                    texDefItem.Add("psdComment", PsbString.Empty);
                    texDefItem.Add("label", mmoItem["label"]);
                }

                var psdIconList = new PsbList();
                foreach (var iconItem in (PsbList)mmoItem["iconList"])
                {
                    psdIconList.Add(new PsbDictionary
                    {
                        {"comment", PsbString.Empty },
                        {"iconLabel", iconItem.Children("label") },
                        {"psdLabel", iconItem.Children("label") },
                    });
                }
                texDefItem.Add("psdIconList", psdIconList);
                textureDef.Add(texDefItem);
            }

            return textureDef;
        }

        private static string CombineMmoPath(PsbDictionary mmoPath)
        {
            var chara = mmoPath["chara"].ToString();
            var layer = mmoPath.ContainsKey("layer") ? mmoPath["layer"].ToString() : mmoPath["layers"].Children(0).ToString();
            var motion = mmoPath["motion"].ToString();
            return $"{chara}/{motion}/{layer}";
        }

        private static PsbDictionary AssemblyMmoPath(string mmoPath)
        {
            var paths = mmoPath.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
            var dic = new PsbDictionary(3)
            {
                {"chara", paths[1].ToPsbString()},
                {"motion", paths[2].ToPsbString()},
                {"layer", string.Join("/", paths.Skip(3)).ToPsbString()}
            };
            return dic;
        }

        private void BuildVariableList(PsbList variableList, out PsbList variableAlias, out PsbList variableFrameAlias)
        {
            variableAlias = new PsbList();
            variableFrameAlias = new PsbList();

            foreach (var val in variableList)
            {
                var item = (PsbDictionary)val;

                variableAlias.Add(new PsbDictionary
                {
                    {"bind", item["label"] }, //TODO: bind default name Dictionary
                    {"comment", PsbString.Empty },
                    {"id", item["label"] },
                    {"label", item["label"] },
                });

                variableFrameAlias.Add(new PsbDictionary
                {
                    {"comment", PsbString.Empty },
                    {"frames", item["frameList"] },
                    {"id", item["label"] },
                });
            }
        }

        private IPsbValue BuildControlDefinition(PsbList control)
        {
            var controlDefinition = new PsbList(control.Count);
            foreach (var psbValue in control)
            {
                var item = (PsbDictionary)psbValue;
                var defItem = new PsbDictionary()
                {
                    {"baseLayer", item["baseLayer"] },
                    {"comment", PsbString.Empty },
                    {"enabled", item["enabled"] },
                    {"label", item["label"] },
                    {"parameter", "標準".ToPsbString() }, //TODO: generate param
                    {"var_lr", item["var_lr"] },
                    {"var_lrm", item["var_lrm"] },
                    {"var_ud", item["var_ud"] },
                };
                controlDefinition.Add(defItem);
            }

            return controlDefinition;
        }

        private IPsbValue BuildCustomPartsBaseDefinition(PsbDictionary psbObjects)
        {
            return new PsbList();
        }

        /// <summary>
        /// Metadata: fetch from PSB
        /// </summary>
        /// <param name="psb"></param>
        /// <returns></returns>
        private IPsbValue BuildMetadata(PSB psb)
        {
            //TODO: Check metadata is valid

            var metadata = new PsbDictionary(2)
            {
                ["type"] = 1.ToPsbNumber(),
                ["data"] = psb.Objects["metadata"]
            };
            return metadata;
        }

        /// <summary>
        /// Default: 4096
        /// </summary>
        /// <param name="psb"></param>
        /// <returns></returns>
        private IPsbValue BuildMaxTextureSize(PSB psb)
        {
            return 4096.ToPsbNumber();
        }

        /// <summary>
        /// Can be null
        /// </summary>
        /// <returns></returns>
        private IPsbValue BuildBackground()
        {
            return new PsbList(0);
        }

        #region Fill Defaults
        private static void FillDefaultsIntoFrameListContent(PsbDictionary content, bool hasMotion = false, bool hasStencil = false)
        {
            foreach (var flContent in DefaultFrameListContent)
            {
                if (!content.ContainsKey(flContent.Key))
                {
                    content.Add(flContent.Key, flContent.Value);
                }
            }

            if (hasMotion)
            {
                foreach (var flContent in DefaultFrameListContent_Motion)
                {
                    if (!content.ContainsKey(flContent.Key))
                    {
                        content.Add(flContent.Key, flContent.Value);
                    }
                }
            }

            if (hasStencil)
            {
                foreach (var flContent in DefaultFrameListContent_Stencil)
                {
                    if (!content.ContainsKey(flContent.Key))
                    {
                        content.Add(flContent.Key, flContent.Value);
                    }
                }
            }

            //if (!(content["mbp"] is PsbNull) && content["src"] is PsbString s && s.Value.StartsWith("blank/"))
            //{
            //    content["color"] = DefaultFrameListContent_Color;
            //}

            return;
        }

        private static IPsbValue FillDefaultParameterize()
        {
            return new PsbDictionary(5)
            {
                {"discretization", PsbNumber.Zero },
                {"enabled", PsbNumber.Zero },
                {"id", "param".ToPsbString() },
                {"rangeBegin", PsbNumber.Zero },
                {"rangeEnd", 1.ToPsbNumber() },
            };
        }

        /// <summary>
        /// type 1: json (default = PsbNull) ; type 0: string (default = PsbString.Empty)
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static PsbDictionary FillDefaultMetadata(int type = 1)
        {
            return new PsbDictionary(2)
            {
                {"data", type == 0 ? (IPsbValue)PsbString.Empty : PsbNull.Null },
                {"type", type.ToPsbNumber() },
            };
        }

        private static void FillDefaultsIntoChildren(PsbDictionary dic, MmoItemClass classType)
        {
            if (!dic.ContainsKey("marker"))
            {
                dic["marker"] = PsbNumber.Zero;
            }

            if (classType == MmoItemClass.ObjLayerItem)
            {
                if (!dic.ContainsKey("objClipping"))
                {
                    dic["objClipping"] = PsbNumber.Zero;
                }

                if (!dic.ContainsKey("objMaskThresholdOpacity"))
                {
                    dic["objMaskThresholdOpacity"] = 64.ToPsbNumber();
                }
            }

            if (classType == MmoItemClass.MotionLayerItem)
            {
                if (!dic.ContainsKey("motionIndependentLayerInherit"))
                {
                    dic["motionIndependentLayerInherit"] = PsbNumber.Zero;
                }

                if (!dic.ContainsKey("motionMaskThresholdOpacity"))
                {
                    dic["motionMaskThresholdOpacity"] = 64.ToPsbNumber();
                }

                if (!dic.ContainsKey("motionClipping"))
                {
                    dic["motionClipping"] = PsbNumber.Zero;
                }
            }

            if (classType == MmoItemClass.StencilLayerItem)
            {
                if (!dic.ContainsKey("stencilMaskThresholdOpacity"))
                {
                    dic["stencilMaskThresholdOpacity"] = 64.ToPsbNumber();
                }
            }
        }

        private static readonly PsbDictionary DefaultFrameListContent = new PsbDictionary
        {
            {"acc", PsbNull.Null },
            {"act", PsbString.Empty },
            {"angle", PsbNumber.Zero },
            {"bm", 16.ToPsbNumber() },
            {"bp", PsbNumber.Zero },
            {"ccc", PsbNull.Null },
            {"cm", PsbString.Empty },
            {"color", PsbNull.Null },
            {"coord", new PsbList(3){PsbNumber.Zero, PsbNumber.Zero, PsbNumber.Zero } },
            {"cp", PsbNull.Null },
            {"fx", PsbNumber.Zero },
            {"fy", PsbNumber.Zero },
            {"mbp", PsbNull.Null },
            {"mcc", PsbNull.Null },
            {"md", new PsbDictionary(2)
                { {"data", PsbNull.Null}, {"type", 1.ToPsbNumber()} } },
            {"occ", PsbNull.Null },
            {"opa", 255.ToPsbNumber() },
            {"ox", PsbNumber.Zero }, //TODO: since ox,oy is always used AFAIK, what's the default value of them?
            {"oy", PsbNumber.Zero },
            {"scc", PsbNull.Null },
            //{"src", "layout".ToPsbString() },
            {"sx", PsbNumber.Zero },
            {"sy", PsbNumber.Zero },
            {"ti", PsbNumber.Zero },
            {"zcc", PsbNull.Null },
            {"zx", 1.ToPsbNumber() },
            {"zy", 1.ToPsbNumber() },
        };

        /// <summary>
        /// frameList/[]/content/motion
        /// <para>need more info, only know `timeOffset` = 0</para>
        /// </summary>
        private static readonly PsbDictionary DefaultFrameListContent_Motion = new PsbDictionary
        {
            {"mdocmpl", PsbNumber.Zero },
            {"mdofst", PsbNumber.Zero }, //maybe timeOffset?
            {"mdt", 1.ToPsbNumber() },
            {"mdtgt", PsbString.Empty },
            {"mpac", PsbNumber.Zero },
            {"mpc", PsbNumber.Zero },
            {"mpf", PsbNumber.Zero },
            {"mpj", PsbNumber.Zero },
        };

        /// <summary>
        /// frameList/[]/content/
        /// </summary>
        private static readonly PsbDictionary DefaultFrameListContent_Stencil = new PsbDictionary
        {
            {"swpcc", PsbNull.Null },
            {"swpen", PsbNumber.Zero }, //maybe timeOffset?
            {"swpratio", new PsbNumber(0.5f) },
            {"swprv", PsbNumber.Zero },
            {"swpsoft", new PsbNumber(1.0f)},
        };

        /// <summary>
        /// when mbp != null and src.StartWith("blank/") //not correct
        /// </summary>
        private static readonly PsbList DefaultFrameListContent_Color = new PsbList(4)
        {
            (-8355712).ToPsbNumber(),(-8355712).ToPsbNumber(),(-8355712).ToPsbNumber(),(-8355712).ToPsbNumber()
        };

        /// <summary>
        /// Use Template
        /// </summary>
        /// <returns></returns>
        private static IPsbValue FillDefaultPreviewSize()
        {
            return new PsbDictionary(4)
            {
                {"height", 1080.ToPsbNumber() },
                {"width", 800.ToPsbNumber() },
                {"originX", PsbNumber.Zero },
                {"originY", PsbNumber.Zero },
            };
        }

        /// <summary>
        /// Use Template
        /// </summary>
        /// <returns></returns>
        private static IPsbValue FillDefaultTargetOwn()
        {
            return new PsbDictionary
            {
                {"nitro2d", new PsbDictionary
                {
                    {"baseFrameCount", 1.ToPsbNumber() }
                } },
                {"revo", new PsbDictionary
                {
                    {"directColorFormat", "5553".ToPsbString() }
                } }
            };
        }

        #endregion
    }
}
