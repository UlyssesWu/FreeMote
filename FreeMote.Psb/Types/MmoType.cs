namespace FreeMote.Psb.Types
{
    class MmoType : IPsbType
    {
        public PsbType PsbType => PsbType.Mmo;
        public bool IsThisType(PSB psb)
        {
            return psb.Objects.ContainsKey("objectChildren") && psb.Objects.ContainsKey("sourceChildren");
        }
    }
}
