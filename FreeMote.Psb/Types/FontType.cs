using System.Collections.Generic;
using System.Linq;
using FreeMote.Plugins;

namespace FreeMote.Psb.Types
{
    class FontType : BaseImageType, IPsbType
    {
        public PsbType PsbType => PsbType.BmpFont;
        public bool IsThisType(PSB psb)
        {
            return psb.TypeId == "font"; //&& psb.Objects.ContainsKey("code");
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : IResourceMetadata
        {
            List<T> resourceList = psb.Resources == null
                ? new List<T>()
                : new List<T>(psb.Resources.Count);

            if (psb.Resources != null)
                resourceList.AddRange(psb.Resources.Select(r => new ImageMetadata { Resource = r }).Cast<T>());

            return resourceList;
        }
    }
}
