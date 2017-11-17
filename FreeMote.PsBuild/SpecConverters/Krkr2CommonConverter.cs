using System;
using FreeMote.Psb;

namespace FreeMote.PsBuild.SpecConverters
{
    class Krkr2CommonConverter : ISpecConverter
    {
        public void Convert(PSB psb)
        {
            throw new NotImplementedException();
        }

        public SpecConvertOption ConvertOption { get; set; } = SpecConvertOption.Default;

        public PsbPixelFormat TargetPixelFormat { get; set; } = PsbPixelFormat.WinRGBA8;
        public bool UseRL { get; set; } = false;
        public PsbSpec FromSpec { get; } = PsbSpec.krkr;
        public PsbSpec ToSpec { get; } = PsbSpec.win;
    }
}
