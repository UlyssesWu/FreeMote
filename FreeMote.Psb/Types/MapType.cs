using System.Collections.Generic;
using System.Linq;

namespace FreeMote.Psb.Types
{
    /// <summary>
    /// Tile Map
    /// </summary>
    class MapType : BaseImageType, IPsbType
    {
        public const string Source = "layer";
        public PsbType PsbType => PsbType.Map;
        public bool IsThisType(PSB psb)
        {
            return psb.TypeId == "map";
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : IResourceMetadata
        {
            var resourceList = FindTileResources(psb, deDuplication).Cast<T>().ToList();

            return resourceList;
        }

        private List<ImageMetadata> FindTileResources(PSB psb, bool deDuplication)
        {
            List<ImageMetadata> resList = new List<ImageMetadata>(psb.Resources.Count);

            if (psb.Objects == null || !psb.Objects.ContainsKey(Source) || psb.Objects[Source] is not PsbList list)
            {
                return resList;
            }

            foreach (var item in list)
            {
                if (item is not PsbDictionary obj || !obj.ContainsKey("image") || obj["image"] is not PsbDictionary image)
                {
                    continue;
                }

                var md = PsbResHelper.GenerateImageMetadata(image, null);
                md.PsbType = PsbType.Map;
                md.Spec = psb.Platform;
                resList.Add(md);
            }

            return resList;
        }
    }
}
