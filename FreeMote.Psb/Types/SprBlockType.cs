using System;
using System.Collections.Generic;

namespace FreeMote.Psb.Types
{
    class SprBlockType : BaseImageType, IPsbType
    {
        public PsbType PsbType => PsbType.SprBlock;
        public bool IsThisType(PSB psb)
        {
            return psb.Objects != null && psb.Objects.Count == 3 && psb.Objects.ContainsKey("w") && psb.Objects.ContainsKey("h") && psb.Objects.ContainsKey("image");
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : class, IResourceMetadata
        {
            //8bit (1 byte 1 pixel)
            if (psb.Objects["image"] is PsbResource res)
            {
                ImageMetadata md = new ImageMetadata()
                {
                    PsbType = PsbType,
                    Resource = res,
                    Width = psb.Objects["w"].GetInt(),
                    Height = psb.Objects["h"].GetInt(),
                    Spec = PsbSpec.none,
                    TypeString = PsbPixelFormat.A8.ToStringForPsb().ToPsbString()
                };
                return [md as T];
            }

            return new List<T>();
        }
    }
}
