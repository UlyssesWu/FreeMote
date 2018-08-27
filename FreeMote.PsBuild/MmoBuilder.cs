using System.Collections.Generic;
using FreeMote.Psb;
// ReSharper disable InconsistentNaming

namespace FreeMote.PsBuild
{
    public enum MmoClassName
    {
        //ObjLayerItem = 0, CharaItem, MotionItem
        LayoutLayerItem = 2,
        MotionLayerItem = 3,
        StencilLayerItem = 12,
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

            mmo.Objects["bgChildren"] = BuildBackground();
            mmo.Objects["comment"] = psb.Objects["comment"] ?? "Built by FreeMote".ToPsbString();
            mmo.Objects["defaultFPS"] = 60.ToPsbNumber();
            mmo.Objects["fontInfoIdCount"] = PsbNull.Null;
            mmo.Objects["fontInfoList"] = new PsbCollection(0);
            mmo.Objects["forceRepack"] = 1.ToPsbNumber();
            mmo.Objects["ignoreMotionPanel"] = 0.ToPsbNumber();
            mmo.Objects["keepSourceIconName"] = 1.ToPsbNumber();
            mmo.Objects["label"] = "template".ToPsbString();
            mmo.Objects["marker"] = 0.ToPsbNumber();
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
            mmo.Objects["outputDepth"] = 0.ToPsbNumber();
            mmo.Objects["previewSize"] = BuildPreviewSize(psb);
            mmo.Objects["projectType"] = 0.ToPsbNumber();
            mmo.Objects["saveFormat"] = 0.ToPsbNumber();
            mmo.Objects["sourceChildren"] = BuildSources(psb);
            mmo.Objects["stereovisionProfile"] = psb.Objects["stereovisionProfile"];
            mmo.Objects["targetOwn"] = BuildTargetOwn();
            mmo.Objects["unifyTexture"] = 1.ToPsbNumber();
            mmo.Objects["uniqId"] = 114514.ToPsbNumber();
            mmo.Objects["version"] = new PsbNumber(3.12f);

            return mmo;
        }

        /// <summary>
        /// Use Template
        /// </summary>
        /// <returns></returns>
        private static IPsbValue BuildTargetOwn()
        {
            return new PsbDictionary()
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

        private static IPsbValue BuildSources(PSB psb)
        {
            return new PsbCollection();
        }

        /// <summary>
        /// Use Template
        /// </summary>
        /// <param name="psb"></param>
        /// <returns></returns>
        private static IPsbValue BuildPreviewSize(PSB psb)
        {
            return new PsbDictionary(4)
            {
                {"height", 1080.ToPsbNumber() },
                {"width", 800.ToPsbNumber() },
                {"originX", 0.ToPsbNumber() },
                {"originY", 0.ToPsbNumber() },
            };
        }

        /// <summary>
        /// Should be able to build from PSB's `object`
        /// </summary>
        /// <param name="psb"></param>
        /// <returns></returns>
        private static IPsbValue BuildObjects(PSB psb)
        {
            PsbCollection objectChildren = new PsbCollection();
            foreach (KeyValuePair<string, IPsbValue> motionItemKv in (PsbDictionary)psb.Objects["objects"])
            {
                PsbDictionary motionItem = (PsbDictionary)motionItemKv.Value;
                PsbDictionary objectChildrenItem = new PsbDictionary();
                objectChildrenItem["label"] = motionItemKv.Key.ToPsbString();
                objectChildrenItem["className"] = "CharaItem".ToPsbString();
                objectChildrenItem["comment"] = "".ToPsbString();
                objectChildrenItem["defaultCoordinate"] = 0.ToPsbNumber();
                objectChildrenItem["marker"] = 0.ToPsbNumber();
                objectChildrenItem["metadata"] = motionItem["metadata"];
                objectChildrenItem["children"] = BuildChildrenFromMotion((PsbDictionary)motionItem["motion"]);
                //objectChildrenItem["uniqId"] = ;

                objectChildren.Add(objectChildrenItem);
            }

            PsbCollection BuildChildrenFromMotion(PsbDictionary dic)
            {
                PsbCollection objectChildren_children = new PsbCollection();
                foreach (KeyValuePair<string, IPsbValue> motionItemKv in dic)
                {
                    PsbDictionary motionItem = (PsbDictionary)motionItemKv.Value;
                    PsbDictionary objectChildrenItem = new PsbDictionary();
                    objectChildrenItem["label"] = motionItemKv.Key.ToPsbString();
                    objectChildrenItem["className"] = "MotionItem".ToPsbString();
                    objectChildrenItem["comment"] = "".ToPsbString();
                    objectChildrenItem["exportSelf"] = 1.ToPsbNumber();
                    objectChildrenItem["marker"] = 0.ToPsbNumber();
                    objectChildrenItem["metadata"] = motionItem["metadata"];
                    objectChildrenItem["priorityFrameList"] = motionItem["priority"];
                    objectChildrenItem["lastTime"] = motionItem["lastTime"];
                    objectChildrenItem["loopBeginTime"] = motionItem["loopTime"]; //TODO: loop
                    objectChildrenItem["loopEndTime"] = motionItem["loopTime"]; //currently begin = end = -1
                    objectChildrenItem["variableChildren"] = motionItem["variable"];
                    objectChildrenItem["tagFrameList"] = motionItem["tag"];
                    objectChildrenItem["referenceModelFileList"] = motionItem["referenceModelFileList"];
                    objectChildrenItem["referenceProjectFileList"] = motionItem["referenceProjectFileList"];
                    PsbCollection parameter = (PsbCollection)motionItem["parameter"];
                    objectChildrenItem["parameterize"] = motionItem["parameterize"] == null
                        ? PsbNull.Null
                        : parameter[((PsbNumber)motionItem["parameterize"]).IntValue];
                    BuildLayerChildren((PsbCollection)motionItem["layer"], parameter);
                    objectChildrenItem["layerChildren"] = motionItem["layer"];
                    //objectChildrenItem["uniqId"] = ;

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
                        switch (typeNum.IntValue)
                        {
                            case 0:
                                className = "ObjLayerItem";
                                break;
                            case 2:
                                className = "LayoutLayerItem";
                                break;
                            case 3:
                                className = "MotionLayerItem";
                                break;
                            case 12:
                                className = "StencilLayerItem";
                                break;
                        }
                    }

                    dic["className"] = className.ToPsbString();
                    dic["comment"] = "".ToPsbString();

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

                    //other
                    FillDefaultsIntoChildren(dic);
                }

            }

            return objectChildren;
        }

        private static void FillDefaultsIntoChildren(PsbDictionary dic)
        {
            return;
        }

        private static void BuildFrameList(PsbCollection frameList)
        {
            foreach (var fl in frameList)
            {
                if (fl is PsbDictionary dic)
                {
                    if (dic["content"] is PsbDictionary content)
                    {
                        content["mbp"] = content["mesh"].Children("bp");
                        content["mcc"] = content["mesh"].Children("cc");
                        FillDefaultsIntoFrameList(dic);
                    }
                    else
                    {
                        dic["content"] = PsbNull.Null;
                    }
                }
            }
        }

        private static void FillDefaultsIntoFrameList(PsbDictionary fl)
        {
            return;
        }

        /// <summary>
        /// Can be null
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
            PsbDictionary metadata = new PsbDictionary(2);
            metadata["type"] = 1.ToPsbNumber();
            metadata["data"] = psb.Objects["metadata"];
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
    }
}
