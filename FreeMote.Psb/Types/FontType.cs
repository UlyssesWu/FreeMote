namespace FreeMote.Psb.Types
{
    class FontType : IPsbType
    {
        public PsbType PsbType => PsbType.BmpFont;
        public bool IsThisType(PSB psb)
        {
            return psb.TypeId == "font"; //&& psb.Objects.ContainsKey("code");
        }
    }
}
