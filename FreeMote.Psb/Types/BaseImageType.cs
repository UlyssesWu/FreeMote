using System.Collections.Generic;
using System.Drawing;
using FreeMote.Plugins;

namespace FreeMote.Psb.Types
{
    public abstract class BaseImageType
    {
        public virtual void Link(PSB psb, FreeMountContext context, IList<string> resPaths, string baseDir = null, PsbLinkOrderBy order = PsbLinkOrderBy.Convention)
        {
            PsbResHelper.LinkImages(psb, context, resPaths, baseDir, order);
        }

        public virtual void Link(PSB psb, FreeMountContext context, IDictionary<string, string> resPaths, string baseDir = null)
        {
            PsbResHelper.LinkImages(psb, context, resPaths, baseDir);
        }

        public virtual List<Bitmap> Unlink(PSB psb, PsbLinkOrderBy order = PsbLinkOrderBy.Name, bool disposeResInPsb = true)
        {
            return PsbResHelper.UnlinkImages(psb, order, disposeResInPsb);
        }

        public virtual void UnlinkToFile(PSB psb, FreeMountContext context, string name, string dirPath, bool outputUnlinkedPsb = true,
            PsbLinkOrderBy order = PsbLinkOrderBy.Name)
        {
            PsbResHelper.UnlinkImagesToFile(psb, context, name, dirPath, outputUnlinkedPsb, order);
        }

        public virtual Dictionary<string, string> OutputResources(PSB psb, FreeMountContext context, string name, string dirPath,
            PsbExtractOption extractOption = PsbExtractOption.Original)
        {
            return PsbResHelper.OutputImageResources(psb, context, name, dirPath, extractOption);
        }
    }
}
