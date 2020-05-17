using System.Collections.Generic;
using System.Linq;
using FreeMote.Plugins;

namespace FreeMote.Psb.Types
{
    class SoundArchiveType : IPsbType
    {
        public PsbType PsbType => PsbType.SoundArchive;
        public bool IsThisType(PSB psb)
        {
            return psb.TypeId == "sound_archive";
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : IResourceMetadata
        {
            List<T> resourceList = psb.Resources == null
                ? new List<T>()
                : new List<T>(psb.Resources.Count);

            if (psb.Resources != null)
                resourceList.AddRange(psb.Resources.Select(r => new AudioMetadata() { Resource = r }).Cast<T>());

            return resourceList;
        }

        public void Link(PSB psb, FreeMountContext context, IList<string> resPaths, string baseDir = null,
            PsbLinkOrderBy order = PsbLinkOrderBy.Convention)
        {
            
        }

        public void Link(PSB psb, FreeMountContext context, IDictionary<string, string> resPaths, string baseDir = null)
        {
            
        }

        public void UnlinkToFile(PSB psb, FreeMountContext context, string name, string dirPath, bool outputUnlinkedPsb = true,
            PsbLinkOrderBy order = PsbLinkOrderBy.Name)
        {
            throw new System.NotImplementedException();
        }

        public Dictionary<string, string> OutputResources(PSB psb, FreeMountContext context, string name, string dirPath,
            PsbExtractOption extractOption = PsbExtractOption.Original)
        {
            throw new System.NotImplementedException();
        }
    }
}
