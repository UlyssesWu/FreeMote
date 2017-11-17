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
            psb.Platform = PsbSpec.krkr;
        }

        private void SplitTexture(PSB psb)
        {
            
        }

        private void Remove(PSB psb)
        {
            //Remove `easing`
            psb.Objects.Remove("easing");

            //Remove `/object/*/motion/*/bounds`
            //Remove `/object/*/motion/*/layerIndexMap`
            var obj = (PsbDictionary) psb.Objects["object"];
            foreach (var o in obj)
            {
                var name = o.Key;
                foreach (var m in (PsbDictionary)((PsbDictionary)o.Value)["motion"])
                {
                    if (m.Value is PsbDictionary mDic)
                    {
                        mDic.Remove("bounds");
                        mDic.Remove("layerIndexMap");
                    }
                }
                
            }
        }

        private void Travel(IPsbCollection collection)
        {
            if (collection is PsbDictionary dic)
            {
                //dic
            }

        }

        private void Add(PSB psb)
        {

        }
    }
}
