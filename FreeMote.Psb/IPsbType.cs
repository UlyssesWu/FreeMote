// ReSharper disable InconsistentNaming

using System.Collections.Generic;
using FreeMote.Plugins;
using FreeMote.Psb.Types;

namespace FreeMote.Psb
{
    public interface IPsbType
    {
        /// <summary>
        /// PSB type
        /// </summary>
        PsbType PsbType { get; }

        /// <summary>
        /// Check if <paramref name="psb"/> is this type
        /// </summary>
        /// <param name="psb"></param>
        /// <returns></returns>
        bool IsThisType(PSB psb);

        /// <summary>
        /// Collect Resources
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="deDuplication"></param>
        /// <returns></returns>
        List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T: IResourceMetadata;

        /// <summary>
        /// Link
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="context"></param>
        /// <param name="resPaths">(legacy) res.json style resource list</param>
        /// <param name="baseDir"></param>
        /// <param name="order"></param>
        void Link(PSB psb, FreeMountContext context, IList<string> resPaths, string baseDir = null, PsbLinkOrderBy order = PsbLinkOrderBy.Convention);

        /// <summary>
        /// Link
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="context"></param>
        /// <param name="resPaths"></param>
        /// <param name="baseDir"></param>
        void Link(PSB psb, FreeMountContext context, IDictionary<string, string> resPaths, string baseDir = null);

        /// <summary>
        /// Unlink to file
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="context"></param>
        /// <param name="name">resource folder name, could be PSB name itself, or with `-resource` suffix</param>
        /// <param name="dirPath">resource folder path</param>
        /// <param name="outputUnlinkedPsb">whether to save the PSB without texture</param>
        /// <param name="order"></param>
        /// <returns>unlinked PSB path</returns>
        void UnlinkToFile(PSB psb, FreeMountContext context, string name, string dirPath, bool outputUnlinkedPsb = true, PsbLinkOrderBy order = PsbLinkOrderBy.Name);

        /// <summary>
        /// Output resources
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="context"></param>
        /// <param name="name">resource folder name, could be PSB name itself, or with `-resource` suffix</param>
        /// <param name="dirPath">resource folder path</param>
        /// <param name="extractOption"></param>
        /// <returns>(FileName, RelativePath)</returns>
        Dictionary<string, string> OutputResources(PSB psb, FreeMountContext context, string name, string dirPath, PsbExtractOption extractOption = PsbExtractOption.Original);
    }

    public partial class PSB
    {
        internal static readonly Dictionary <PsbType, IPsbType> TypeHandlers = new Dictionary<PsbType, IPsbType>()
        {
            {PsbType.Motion, new MotionType()},
            {PsbType.Scn, new ScnType()},
            {PsbType.Tachie, new ImageType()},
            {PsbType.Pimg, new PimgType()},
            {PsbType.Mmo, new MmoType()},
            {PsbType.ArchiveInfo, new ArchiveType()},
            {PsbType.SoundArchive, new SoundArchiveType()},
            {PsbType.BmpFont, new FontType()},
            {PsbType.PSB, new MotionType()}, //assume as motion type by default, must put this after Motion
    };
        
    }
}
