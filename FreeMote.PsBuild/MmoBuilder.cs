using System.Collections.Generic;
using FreeMote.Psb;

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
                    PsbCollection parameter = (PsbCollection)motionItem["parameter"];
                    objectChildrenItem["parameterize"] = motionItem["parameterize"] == null
                        ? PsbNull.Null
                        : parameter[((PsbNumber) motionItem["parameterize"]).IntValue];
                    objectChildrenItem["layerChildren"] = BuildLayerChildren((PsbCollection)motionItem["layer"], parameter);
                    //objectChildrenItem["uniqId"] = ;
                    //TODO:

                    objectChildren_children.Add(objectChildrenItem);
                }

                return objectChildren_children;
            }

            PsbCollection BuildLayerChildren(PsbCollection col, PsbCollection parameter)
            {
                return null; //TODO:
            }

            return objectChildren;
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
