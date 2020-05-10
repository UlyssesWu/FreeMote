namespace FreeMote.Psb.Types
{
    class SoundArchiveType : IPsbType
    {
        public PsbType PsbType => PsbType.SoundArchive;
        public bool IsThisType(PSB psb)
        {
            throw new System.NotImplementedException();
        }
    }
}
