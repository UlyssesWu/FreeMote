using System;
using FreeMote.Psb;

namespace FreeMote.PsBuild.SpecConverters
{
    class Win2KrkrConverter : ISpecConverter
    {
        public PsbPixelFormat TargetPixelFormat { get; set; } = PsbPixelFormat.CommonRGBA8;
        public bool UseRL { get; set; } = true;

        public PsbSpec FromSpec { get; } = PsbSpec.win;
        public PsbSpec ToSpec { get; } = PsbSpec.krkr;

        public void Convert(PSB psb)
        {
            throw new NotImplementedException();
        }

        private void SplitTexture()
        {
            
        }

        private void Remove(PSB psb)
        {
            
        }

        private void Add(PSB psb)
        {

        }
    }
}
