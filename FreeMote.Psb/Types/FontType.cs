using System.Collections.Generic;
using System.Linq;

namespace FreeMote.Psb.Types
{
    class FontType : BaseImageType, IPsbType
    {
        public const string Source = "source";
        public PsbType PsbType => PsbType.BmpFont;
        public bool IsThisType(PSB psb)
        {
            return psb.TypeId == "font"; //&& psb.Objects.ContainsKey("code");
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : IResourceMetadata
        {
            //List<T> resourceList = psb.Resources == null
            //    ? new List<T>()
            //    : new List<T>(psb.Resources.Count);

            //if (psb.Resources != null)
            //    resourceList.AddRange(psb.Resources.Select(r => new ImageMetadata { Resource = r }).Cast<T>());

            var resourceList = FindFontResources(psb, deDuplication).Cast<T>().ToList();

            return resourceList;
        }

        public List<ImageMetadata> FindFontResources(PSB psb, bool deDuplication = true)
        {
            List<ImageMetadata> resList = new List<ImageMetadata>(psb.Resources.Count);

            if (psb.Objects == null || !psb.Objects.ContainsKey(Source) || !(psb.Objects[Source] is PsbList list))
            {
                return resList;
            }

            foreach (var item in list)
            {
                if (item is not PsbDictionary obj)
                {
                    continue;
                }

                //TODO: deDuplication for resource (besides pal)
                var md = PsbResHelper.GenerateImageMetadata(obj, null);
                md.PsbType = PsbType.BmpFont;
                md.Spec = psb.Platform;
                resList.Add(md);
            }

            return resList;
        }
    }
}
