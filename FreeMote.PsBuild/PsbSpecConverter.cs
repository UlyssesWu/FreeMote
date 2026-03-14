using FreeMote.Psb;
using FreeMote.PsBuild.Converters;

namespace FreeMote.PsBuild
{
    public static class PsbSpecConverter
    {
        /// <summary>
        /// Enable resolution support (may scale images, quality is not guaranteed)
        /// </summary>
        public static bool EnableResolution = false;

        /// <summary>
        /// Try to switch Spec
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="targetSpec"></param>
        /// <param name="pixelFormat"></param>
        public static void SwitchSpec(this PSB psb, PsbSpec targetSpec,
            PsbPixelFormat pixelFormat = PsbPixelFormat.None)
        {
            if (targetSpec is PsbSpec.other or PsbSpec.none)
            {
                return;
            }

            //Alternative //TODO: Alternative table?
            bool isAlternative = false;
            var realTargetSpec = PsbSpec.common;

            var original = psb.Platform;
            if (original == PsbSpec.ems)
            {
                original = PsbSpec.common;
            }

            if (targetSpec == PsbSpec.ems)
            {
                isAlternative = true;
                realTargetSpec = targetSpec;
                targetSpec = PsbSpec.common;
            }

            if (targetSpec == PsbSpec.krkr) //krkr can not select pixel format
            {
                switch (original)
                {
                    case PsbSpec.win:
                    {
                        Common2KrkrConverter winKrkr = new Common2KrkrConverter();
                        winKrkr.Convert(psb);
                        break;
                    }

                    case PsbSpec.common:
                    {
                        Common2KrkrConverter commonKrkr = new Common2KrkrConverter();
                        commonKrkr.Convert(psb);
                        break;
                    }

                    default:
                        psb.Platform = targetSpec;
                        break;
                }
            }

            else if (targetSpec == PsbSpec.win)
            {
                switch (original)
                {
                    case PsbSpec.krkr:
                        Krkr2CommonConverter krkr2Win = new Krkr2CommonConverter(true) {EnableResolution = EnableResolution};
                        krkr2Win.Convert(psb);
                        break;
                    case PsbSpec.common:
                        CommonWinConverter winCommon = new CommonWinConverter();
                        winCommon.Convert(psb);
                        break;
                    default:
                        psb.Platform = targetSpec;
                        break;
                }
            }

            else if (targetSpec is PsbSpec.common or PsbSpec.ems)
            {
                switch (original)
                {
                    case PsbSpec.krkr:
                        Krkr2CommonConverter krkr2Common = new Krkr2CommonConverter() {EnableResolution = EnableResolution};
                        krkr2Common.Convert(psb);
                        break;
                    case PsbSpec.win:
                        CommonWinConverter commonWin = new CommonWinConverter();
                        commonWin.Convert(psb);
                        break;
                    case PsbSpec.common:
                    case PsbSpec.ems:
                        psb.Platform = targetSpec;
                        break;
                    default:
                        psb.Platform = targetSpec;
                        break;
                }
            }

            else
            {
                psb.Platform = targetSpec;
            }

            if (isAlternative)
            {
                psb.Platform = realTargetSpec;
            }
        }
    }
}