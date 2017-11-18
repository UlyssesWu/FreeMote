using System;
using System.Collections.Generic;
using FreeMote.Psb;

namespace FreeMote.PsBuild.Converters
{
    /// <summary>
    /// Useless
    /// </summary>
    class Common2WinConverter : ISpecConverter
    {
        public SpecConvertOption ConvertOption { get; set; }


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
