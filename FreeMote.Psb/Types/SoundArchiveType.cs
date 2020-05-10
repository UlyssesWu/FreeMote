namespace FreeMote.Psb.Types
{
    class SoundArchiveType : IPsbType
    {
        public PsbType PsbType => PsbType.SoundArchive;
        public bool IsThisType(PSB psb)
        {
            return psb.TypeId == "sound_archive";
        }
    }
}
