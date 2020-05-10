using System;

namespace FreeMote.Psb.Types
{
    class ScnType : IPsbType
    {
        public PsbType PsbType => PsbType.Scn;
        public bool IsThisType(PSB psb)
        {
            if (psb.Objects.ContainsKey("scenes") && psb.Objects.ContainsKey("name"))
            {
                return true;
            }

            if (psb.Objects.ContainsKey("list") && psb.Objects.ContainsKey("map") && psb.Resources?.Count == 0)
            {
                return true;
            }

            return false;
        }
    }
}
