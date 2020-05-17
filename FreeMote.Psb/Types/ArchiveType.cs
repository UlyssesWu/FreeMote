using System.Collections.Generic;
using FreeMote.Plugins;

namespace FreeMote.Psb.Types
{
    class ArchiveType : IPsbType
    {
        public PsbType PsbType => PsbType.ArchiveInfo;
        public bool IsThisType(PSB psb)
        {
            return psb.TypeId == "archive"; //&& psb.Objects.ContainsKey("file_info")
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : IResourceMetadata
        {
            return new List<T>();
        }
        
        public void Link(PSB psb, FreeMountContext context, IList<string> resPaths, string baseDir = null, PsbLinkOrderBy order = PsbLinkOrderBy.Convention)
        {
        }

        public void Link(PSB psb, FreeMountContext context, IDictionary<string, string> resPaths, string baseDir = null)
        {
        }

        public void UnlinkToFile(PSB psb, FreeMountContext context, string name, string dirPath, bool outputUnlinkedPsb = true,
            PsbLinkOrderBy order = PsbLinkOrderBy.Name)
        {
        }

        public Dictionary<string, string> OutputResources(PSB psb, FreeMountContext context, string name, string dirPath,
            PsbExtractOption extractOption = PsbExtractOption.Original)
        {
            return null;
        }
    }
}
