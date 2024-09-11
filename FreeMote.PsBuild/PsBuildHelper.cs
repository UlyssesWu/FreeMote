using FreeMote.Psb;

namespace FreeMote.PsBuild
{
    public static class PsBuildHelper
    {
        public static PsbNumber ToPsbNumber(this MmoMarkerColor color) => ((int)color).ToPsbNumber();
    }
}
