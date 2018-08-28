using System;
using System.Collections;
using System.Collections.Generic;
using FreeMote.Psb;
// ReSharper disable InconsistentNaming

namespace FreeMote.PsBuild
{
    public enum MmoMarkerColor
    {
        /// <summary>
        /// なし
        /// </summary>
        None = 0,
        /// <summary>
        /// 赤
        /// </summary>
        Red = 1,
        /// <summary>
        /// 绿
        /// </summary>
        Green = 2,
        /// <summary>
        /// 青
        /// </summary>
        Blue = 3,
        /// <summary>
        /// 橙
        /// </summary>
        Orange = 4,
        /// <summary>
        /// 紫
        /// </summary>
        Purple = 5,
        /// <summary>
        /// 桃
        /// </summary>
        Pink = 6,
        /// <summary>
        /// 灰
        /// </summary>
        Gray = 7,
    }
    public enum MmoItemClass
    {
        ObjLayerItem = 0, //CharaItem, MotionItem
        //"objClipping": 0,
        //"objMaskThresholdOpacity": 64,
        //"objTriPriority": 2,
        //TextLayer is always hold by a ObjLayer with "#text00000" label and "src/#font00000/#text00000" frameList/content/src
        ShapeLayerItem = 1,
        //"shape": "point" (psb: 0) | "circle" (psb: 1) | "rect" (psb: 2) | "quad" (psb: 3)
        LayoutLayerItem = 2,
        MotionLayerItem = 3,
        /*
          "motionClipping": 0,
          "motionIndependentLayerInherit": 0,
          "motionMaskThresholdOpacity": 64,
         */
        ParticleLayerItem = 4,
        //"particle": "point" (psb: 0) | "ellipse" (psb: 1) | "quad" (psb: 2) 
        /*                                 
          "particleAccelRatio": 1.0,
          "particleApplyZoomToVelocity": 0,
          "particleDeleteOutsideScreen": 0,
          "particleFlyDirection": 0,
          "particleInheritAngle": 0,
          "particleInheritOpacity": 1,
          "particleInheritVelocity": 0,
          "particleMaxNum": 20,
          "particleMotionList": [],
          "particleTriVolume": 0,
         */

        CameraLayerItem = 5,
        ClipLayerItem = 7, //nothing special
        TextLayerItem = 8,
        /*
        "fontParams": {
        "antiAlias": 1,
        "bold": 0,
        "brushColor1": -16777216,
        "brushColor2": -16777216,
        "depth": 1,
        "name": "ＭＳ ゴシック",
        "penColor": -16777216,
        "penSize": 0,
        "rev": 1,
        "size": 16
        },
        "textParams": {
        "alignment": 0,
        "colSpace": 0,
        "defaultVertexColor": -1,
        "originAlignment": 1,
        "rasterlize": 2,
        "rowSpace": 0,
        "text": "Built by FreeMote"
        },
         */
        MeshLayerItem = 11, //nothing special, take care of meshXXX
        StencilLayerItem = 12,
        /*
          "stencilCompositeMaskLayerList": [],
          "stencilMaskThresholdOpacity": 64,
          "stencilType": 1,
         */
    }

    /// <summary>
    /// Build MMO from Emote PSB
    /// <para>Current Ver: 3.12</para>
    /// </summary>
    class MmoBuilder
    {
        //public PSB Mmo { get; private set; }

        public static PSB Build(PSB psb)
        {
            PSB mmo = new PSB();
            mmo.Type = PsbType.Mmo;

            mmo.Objects = new PsbDictionary();
            mmo.Objects["bgChildren"] = BuildBackground();
            mmo.Objects["comment"] = psb.Objects["comment"] ?? "Built by FreeMote".ToPsbString();
            mmo.Objects["defaultFPS"] = 60.ToPsbNumber();
            mmo.Objects["fontInfoIdCount"] = PsbNull.Null;
            mmo.Objects["fontInfoList"] = new PsbCollection(0);
            mmo.Objects["forceRepack"] = 1.ToPsbNumber();
            mmo.Objects["ignoreMotionPanel"] = PsbNumber.Zero;
            mmo.Objects["keepSourceIconName"] = 1.ToPsbNumber();
            mmo.Objects["label"] = "template".ToPsbString();
            mmo.Objects["marker"] = PsbNumber.Zero;
            mmo.Objects["maxTextureSize"] = BuildMaxTextureSize(psb);
            mmo.Objects["metadata"] = BuildMetadata(psb);
            mmo.Objects["metaformat"] = BuildMetaFormat(psb);
            mmo.Objects["modelScale"] = 32.ToPsbNumber();
            mmo.Objects["newScrapbookCellHeight"] = 8.ToPsbNumber();
            mmo.Objects["newScrapbookCellWidth"] = 8.ToPsbNumber();
            mmo.Objects["newTextureCellHeight"] = 8.ToPsbNumber();
            mmo.Objects["newTextureCellWidth"] = 8.ToPsbNumber();
            mmo.Objects["objectChildren"] = BuildObjects(psb);
            mmo.Objects["optimizeMargin"] = 1.ToPsbNumber();
            mmo.Objects["outputDepth"] = PsbNumber.Zero;
            mmo.Objects["previewSize"] = FillDefaultPreviewSize();
            mmo.Objects["projectType"] = PsbNumber.Zero;
            mmo.Objects["saveFormat"] = PsbNumber.Zero;
            mmo.Objects["sourceChildren"] = BuildSources(psb);
            mmo.Objects["stereovisionProfile"] = psb.Objects["stereovisionProfile"];
            mmo.Objects["targetOwn"] = FillDefaultTargetOwn();
            mmo.Objects["unifyTexture"] = 1.ToPsbNumber();
            mmo.Objects["uniqId"] = 114514.ToPsbNumber();
            mmo.Objects["version"] = new PsbNumber(3.12f);

            return mmo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="psb"></param>
        /// <returns></returns>
        private static IPsbValue BuildSources(PSB psb)
        {
            return new PsbCollection(); //TODO:
        }

        /// <summary>
        /// Should be able to build from PSB's `object`
        /// </summary>
        /// <param name="psb"></param>
        /// <returns></returns>
        private static IPsbValue BuildObjects(PSB psb)
        {
            PsbCollection objectChildren = new PsbCollection();
            foreach (var motionItemKv in (PsbDictionary)psb.Objects["object"])
            {
                PsbDictionary motionItem = (PsbDictionary)motionItemKv.Value;
                PsbDictionary objectChildrenItem = new PsbDictionary();
                objectChildrenItem["label"] = motionItemKv.Key.ToPsbString();
                objectChildrenItem["className"] = "CharaItem".ToPsbString();
                objectChildrenItem["comment"] = PsbString.Empty;
                objectChildrenItem["defaultCoordinate"] = PsbNumber.Zero;
                objectChildrenItem["marker"] = PsbNumber.Zero;
                objectChildrenItem["metadata"] = motionItem["metadata"] is PsbNull ? FillDefaultMetadata() : motionItem["metadata"];
                objectChildrenItem["children"] = BuildChildrenFromMotion((PsbDictionary)motionItem["motion"]);
                objectChildrenItem["templateReferenceChara"] = PsbString.Empty;
                objectChildrenItem["templateSourceMap"] = new PsbDictionary(0);
                //objectChildrenItem["uniqId"] = 4396;

                objectChildren.Add(objectChildrenItem);
            }

            PsbCollection BuildChildrenFromMotion(PsbDictionary dic)
            {
                PsbCollection objectChildren_children = new PsbCollection();
                foreach (var motionItemKv in dic)
                {
                    PsbDictionary motionItem = (PsbDictionary)motionItemKv.Value;
                    PsbDictionary objectChildrenItem = new PsbDictionary();
                    objectChildrenItem["className"] = "MotionItem".ToPsbString();
                    objectChildrenItem["comment"] = PsbString.Empty;
                    objectChildrenItem["exportBounds"] = PsbNumber.Zero;
                    objectChildrenItem["exportSelf"] = 1.ToPsbNumber();
                    objectChildrenItem["forcePreviewLoop"] = PsbNumber.Zero;
                    objectChildrenItem["fps"] = 60.ToPsbNumber();
                    objectChildrenItem["isDelivered"] = PsbNumber.Zero;
                    objectChildrenItem["label"] = motionItemKv.Key.ToPsbString();
                    objectChildrenItem["lastTime"] = motionItem["lastTime"]; //TODO: WARN: should reduce 61 to 60
                    objectChildrenItem["loopBeginTime"] = motionItem["loopTime"]; //TODO: loop
                    objectChildrenItem["loopEndTime"] = motionItem["loopTime"]; //currently begin = end = -1
                    objectChildrenItem["marker"] = PsbNumber.Zero;
                    objectChildrenItem["metadata"] = motionItem["metadata"] is PsbNull ? FillDefaultMetadata() : motionItem["metadata"]; //TODO: should we set all to default?
                    PsbCollection parameter = (PsbCollection)motionItem["parameter"];
                    objectChildrenItem["parameterize"] = motionItem["parameterize"] is PsbNull
                        ? FillDefaultParameterize()
                        : parameter[((PsbNumber)motionItem["parameterize"]).IntValue];
                    objectChildrenItem["priorityFrameList"] = BuildPriorityFrameList((PsbCollection)motionItem["priority"]);
                    objectChildrenItem["referenceModelFileList"] = motionItem["referenceModelFileList"];
                    objectChildrenItem["referenceProjectFileList"] = motionItem["referenceProjectFileList"];
                    objectChildrenItem["streamed"] = PsbNumber.Zero;
                    objectChildrenItem["tagFrameList"] = motionItem["tag"];
                    //objectChildrenItem["uniqId"] = 1551;
                    objectChildrenItem["variableChildren"] = motionItem["variable"];

                    BuildLayerChildren((PsbCollection)motionItem["layer"], parameter);
                    objectChildrenItem["layerChildren"] = motionItem["layer"];

                    objectChildren_children.Add(objectChildrenItem);
                }

                return objectChildren_children;
            }

            void BuildLayerChildren(IPsbCollection child, PsbCollection parameter)
            {
                if (child is PsbCollection col)
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
                    string className = "ObjLayerItem";
                    if (typeNum != null)
                    {
                        className = ((MmoItemClass)typeNum.IntValue).ToString();
                    }

                    dic["className"] = className.ToPsbString();
                    dic["comment"] = PsbString.Empty;

                    if (dic["frameList"] is PsbCollection frameList)
                    {
                        BuildFrameList(frameList);
                    }

                    //parameterize: find from psb table amd expand
                    if (dic["parameterize"] is PsbNumber parameterize && parameterize.IntValue >= 0)
                    {
                        dic["parameterize"] = parameter[parameterize.IntValue];
                    }

                    //stencilType conversion: 5 (psb) -> 1 (mmo)
                    if (dic["stencilType"] is PsbNumber stencilType)
                    {
                        if (stencilType.IntValue == 5)
                        {
                            dic["stencilType"] = 1.ToPsbNumber();
                        }
                    }

                    if (dic["children"] is PsbCollection children)
                    {
                        BuildLayerChildren(children, parameter);
                    }

                    if (dic["metadata"] is PsbNull)
                    {
                        dic["metadata"] = new PsbDictionary(2) //metadata: data is string => type0; data is null?dictionary => type1
                        {
                            {"data", PsbString.Empty },
                            {"type", PsbNumber.Zero },
                        };
                    }

                    //Expand meshSyncChildMask
                    if (dic["meshSyncChildMask"] is PsbNumber number)
                    {
                        dic["meshSyncChildShape"] = (number.IntValue & 8) == 8 ? 1.ToPsbNumber() : PsbNumber.Zero;

                        if ((number.IntValue & 4) == 4)
                        {
                            Console.WriteLine("[WARN] unknown meshSyncChildMask! Please provide sample for research.");
                        }

                        if ((number.IntValue & 2) == 2)
                        {
                            Console.WriteLine("[WARN] unknown meshSyncChildMask! Please provide sample for research.");
                        }

                        dic["meshSyncChildCoord"] = (number.IntValue & 1) == 1 ? 1.ToPsbNumber() : PsbNumber.Zero;
                    }
                    else
                    {
                        dic["meshSyncChildShape"] = PsbNumber.Zero;
                        dic["meshSyncChildCoord"] = PsbNumber.Zero;
                    }

                    if (!dic.ContainsKey("meshSyncChildAngle"))
                    {
                        dic["meshSyncChildAngle"] = PsbNumber.Zero;
                    }
                    if (!dic.ContainsKey("meshSyncChildZoom"))
                    {
                        dic["meshSyncChildZoom"] = PsbNumber.Zero;
                    }

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
                    //"marker": just different color marks
                    //other
                    FillDefaultsIntoChildren(dic);
                }

            }

            return objectChildren;
        }

        private static PsbCollection BuildPriorityFrameList(PsbCollection fl)
        {
            for (int i = 0; i < fl.Count; i++)
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

        private static void BuildFrameList(PsbCollection frameList)
        {
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
                        }

                        bool hasMotion = false;

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

                        FillDefaultsIntoFrameListContent(content, hasMotion);
                    }
                }
            }
        }

        /// <summary>
        /// Can be null?
        /// </summary>
        /// <param name="psb"></param>
        /// <returns></returns>
        private static IPsbValue BuildMetaFormat(PSB psb)
        {
            return PsbNull.Null;
        }

        /// <summary>
        /// Metadata: fetch from PSB
        /// </summary>
        /// <param name="psb"></param>
        /// <returns></returns>
        private static IPsbValue BuildMetadata(PSB psb)
        {
            PsbDictionary metadata = new PsbDictionary(2)
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
        private static IPsbValue BuildMaxTextureSize(PSB psb)
        {
            return 4096.ToPsbNumber();
        }

        /// <summary>
        /// Can be null
        /// </summary>
        /// <returns></returns>
        private static IPsbValue BuildBackground()
        {
            return new PsbCollection(0);
        }

        #region Fill Defaults
        private static void FillDefaultsIntoFrameListContent(PsbDictionary content, bool hasMotion = false)
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

        private static PsbDictionary FillDefaultMetadata(int type = 1)
        {
            return new PsbDictionary(2)
            {
                {"data", type == 0 ? (IPsbValue)PsbString.Empty : PsbNull.Null },
                {"type", type.ToPsbNumber() },
            };
        }

        private static void FillDefaultsIntoChildren(PsbDictionary dic)
        {
            if (!dic.ContainsKey("objClipping"))
            {
                dic["objClipping"] = PsbNumber.Zero;
            }

            if (!dic.ContainsKey("objMaskThresholdOpacity"))
            {
                dic["objMaskThresholdOpacity"] = 64.ToPsbNumber();
            }

            if (!dic.ContainsKey("marker"))
            {
                dic["marker"] = PsbNumber.Zero;
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
            {"coord", new PsbCollection(3){PsbNumber.Zero, PsbNumber.Zero, PsbNumber.Zero } },
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
