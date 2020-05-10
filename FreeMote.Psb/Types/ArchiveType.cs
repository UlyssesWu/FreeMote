namespace FreeMote.Psb.Types
{
    class ArchiveType : IPsbType
    {
        public PsbType PsbType => PsbType.ArchiveInfo;
        public bool IsThisType(PSB psb)
        {
            return psb.TypeId == "archive"; //&& psb.Objects.ContainsKey("file_info")
        }
    }
}
