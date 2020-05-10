namespace FreeMote.Psb.Types
{
    class ImageType : IPsbType
    {
        public PsbType PsbType => PsbType.Tachie;
        public bool IsThisType(PSB psb)
        {
            return psb.TypeId == "image"; //&& psb.Objects.ContainsKey("imageList")
        }
    }
}
