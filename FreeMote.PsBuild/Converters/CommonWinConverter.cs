using System;
using System.Collections.Generic;
using System.Linq;
using FreeMote.Psb;

namespace FreeMote.PsBuild.Converters
{
    /// <summary>
    /// Common/Ems-Win Converter
    /// </summary>
    class CommonWinConverter : ISpecConverter
    {
        /// <summary>
        /// Won't be used in this conversion
        /// </summary>
        public SpecConvertOption ConvertOption { get; set; }
        /// <summary>
        /// Won't be used in this conversion
        /// </summary>
        public PsbPixelFormat TargetPixelFormat { get; set; }
        public bool UseRL { get; set; } = false;
        /// <summary>
        /// If true, it is an EmsWinConverter
        /// </summary>
        public bool EmsAsCommon { get; set; } = false;
        public IList<PsbSpec> FromSpec { get; } = new List<PsbSpec> { PsbSpec.win, PsbSpec.common, PsbSpec.ems };
        public IList<PsbSpec> ToSpec { get; } = new List<PsbSpec> { PsbSpec.common, PsbSpec.win, PsbSpec.ems };
        public void Convert(PSB psb)
        {
            if (!FromSpec.Contains(psb.Platform))
            {
                throw new FormatException("Can not convert Spec for this PSB");
            }

            var asSpec = EmsAsCommon ? PsbSpec.ems : PsbSpec.common;
            var toSpec = psb.Platform == PsbSpec.win ? asSpec : PsbSpec.win;
            var toPixelFormat = toSpec == asSpec ? PsbPixelFormat.BeRGBA8 : PsbPixelFormat.LeRGBA8;
            var resList = psb.CollectResources<ImageMetadata>(false);
            // Type strings and resources can be shared by multiple texture entries. Capture every source
            // format before changing any shared PsbString, and convert each backing resource only once.
            var conversions = resList.Select(resMd => (Metadata: resMd, SourcePixelFormat: resMd.PixelFormat)).ToList();
            var convertedResources = new HashSet<PsbResource>();
            foreach (var conversion in conversions)
            {
                var resMd = conversion.Metadata;
                var sourcePixelFormat = conversion.SourcePixelFormat;
                var resourceData = resMd.Resource.Data;
                if (resMd.TypeString != null)
                {
                    // This must also happen when resources have not been linked yet. It lets a PNG be
                    // encoded directly in the target format instead of source-format -> target-format.
                    resMd.TypeString.Value = toPixelFormat.ToStringForPsb();
                }

                if (!convertedResources.Add(resMd.Resource) || resourceData == null || resourceData.Length == 0)
                {
                    continue;
                }

                var useRl = resMd.Compress == PsbCompressType.RL;
                if (resMd.Compress == PsbCompressType.RL)
                {
                    resourceData = RL.Decompress(resourceData);
                }

                if (sourcePixelFormat is PsbPixelFormat.LeRGBA8 or PsbPixelFormat.BeRGBA8)
                {
                    RL.Switch_0_2(ref resourceData);
                }
                else
                {
                    using var image = RL.ConvertToImage(resourceData, resMd.PalData, resMd.Width, resMd.Height,
                        sourcePixelFormat, resMd.PalettePixelFormat);
                    resourceData = RL.GetPixelBytesFromImage(image, toPixelFormat);
                }

                if (useRl)
                {
                    resourceData = RL.Compress(resourceData);
                }

                resMd.Resource.Data = resourceData;
            }
            psb.Platform = toSpec;
        }
    }
}
