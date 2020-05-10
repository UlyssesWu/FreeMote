// ReSharper disable InconsistentNaming

using System.Collections.Generic;
using FreeMote.Psb.Types;

namespace FreeMote.Psb
{
    public interface IPsbType
    {
        PsbType PsbType { get; }

        bool IsThisType(PSB psb);
    }

    public partial class PSB
    {
        internal static Dictionary <PsbType, IPsbType> TypeHandlers = new Dictionary<PsbType, IPsbType>()
        {
            {PsbType.Motion, new MotionType()},
            {PsbType.Scn, new ScnType()},
            {PsbType.Tachie, new ImageType()},
            {PsbType.Pimg, new PimgType()},
            {PsbType.Mmo, new MmoType()},
            {PsbType.ArchiveInfo, new ArchiveType()},
            {PsbType.SoundArchive, new SoundArchiveType()},
            {PsbType.BmpFont, new FontType()}
        };
        
    }
}
