using System;
using System.Collections.Generic;
using FreeMote.Psb;

namespace FreeMote.PsBuild.Converters
{
    /// <summary>
    /// Useless
    /// </summary>
    class CommonWinConverter : ISpecConverter
    {
        public CommonWinConverter(bool commonToWin = true)
        {
            CommonToWin = commonToWin;
        }
        /// <summary>
        /// false: Win -> common; true: common -> win
        /// </summary>
        public bool CommonToWin { get; set; }
        public SpecConvertOption ConvertOption { get; set; }
        /// <summary>
        /// Won't be used in this conversion
        /// </summary>
        public PsbPixelFormat TargetPixelFormat { get; set; }
        public bool UseRL { get; set; } = false;
        public IList<PsbSpec> FromSpec { get; } = new List<PsbSpec> {PsbSpec.win, PsbSpec.common};
        public IList<PsbSpec> ToSpec { get; } = new List<PsbSpec> {PsbSpec.krkr, PsbSpec.win};
        public void Convert(PSB psb)
        {
            throw new NotImplementedException();
        }
    }
}
