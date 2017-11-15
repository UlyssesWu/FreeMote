using System;
using FreeMote.Psb;

namespace FreeMote.PsBuild.SpecConverters
{
    class Krkr2WinConverter : ISpecConverter
    {
        public void Convert(PSB psb)
        {
            throw new NotImplementedException();
        }

        public PsbPixelFormat TargetPixelFormat { get; set; } = PsbPixelFormat.WinRGBA8;
        public bool UseRL { get; set; } = false;
        public PsbSpec FromSpec { get; } = PsbSpec.krkr;
        public PsbSpec ToSpec { get; } = PsbSpec.win;
    }
}
