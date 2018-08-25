using FreeMote.Psb;

namespace FreeMote.PsBuild
{
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

        private static IPsbValue BuildObjects(PSB psb)
        {
            return new PsbDictionary();
        }

        private static IPsbValue BuildMetaFormat(PSB psb)
        {
            return new PsbDictionary();
        }

        private static IPsbValue BuildMetadata(PSB psb)
        {
            PsbDictionary metadata = new PsbDictionary(2);
            metadata["type"] = 1.ToPsbNumber();
            metadata["data"] = new PsbDictionary();
            return metadata;
        }

        private static IPsbValue BuildMaxTextureSize(PSB psb)
        {
            return 4096.ToPsbNumber();
        }

        private static IPsbValue BuildBackground()
        {
            return new PsbCollection(0);
        }
    }
}
