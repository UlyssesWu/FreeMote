using System.Linq;

namespace FreeMote.Psb.Types
{
    class PimgType : IPsbType
    {
        public PsbType PsbType => PsbType.Pimg;
        public bool IsThisType(PSB psb)
        {
            if (psb.Objects.ContainsKey("layers") && psb.Objects.ContainsKey("height") && psb.Objects.ContainsKey("width"))
            {
                return true;
            }

            if (psb.Objects.Any(k => k.Key.Contains(".") && k.Value is PsbResource))
            {
                return true;
            }

            return false;
        }
    }
}
