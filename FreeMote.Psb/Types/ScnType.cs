using System;
using System.Collections.Generic;
using System.Linq;
using FreeMote.Plugins;

namespace FreeMote.Psb.Types
{
    class ScnType : BaseImageType, IPsbType
    {
        public PsbType PsbType => PsbType.Scn;
        public bool IsThisType(PSB psb)
        {
            if (psb.Objects.ContainsKey("scenes") && psb.Objects.ContainsKey("name"))
            {
                return true;
            }

            if (psb.Objects.ContainsKey("list") && psb.Objects.ContainsKey("map") && psb.Resources?.Count == 0)
            {
                return true;
            }

            return false;
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : IResourceMetadata
        {
            List<T> resourceList = psb.Resources == null
                ? new List<T>()
                : new List<T>(psb.Resources.Count);

            resourceList.AddRange(psb.Objects.Where(k => k.Value is PsbResource).Select(k =>
                new ImageMetadata()
                {
                    Name = k.Key,
                    Resource = k.Value as PsbResource,
                    Compress = k.Key.EndsWith(".tlg", true, null) ? PsbCompressType.Tlg : PsbCompressType.ByName
                }).Cast<T>());

            return resourceList;
        }
        
        public Dictionary<string, string> OutputResources(PSB psb, FreeMountContext context, string filePath,
            PsbExtractOption extractOption = PsbExtractOption.Original)
        {
            throw new NotImplementedException();
        }
    }
}
