using System.Collections.Generic;

namespace FreeMote.Psb.Types
{
    /// <summary>
    /// Tile Map
    /// </summary>
    class MapType : BaseImageType, IPsbType
    {
        public PsbType PsbType => PsbType.Map;
        public bool IsThisType(PSB psb)
        {
            return psb.TypeId == "map";
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : IResourceMetadata
        {
            return new List<T>();
        }
    }
}
