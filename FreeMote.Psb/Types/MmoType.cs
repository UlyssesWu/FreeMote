namespace FreeMote.Psb.Types
{
    class MmoType : IPsbType
    {
        public PsbType PsbType => PsbType.Mmo;
        public bool IsThisType(PSB psb)
        {
            throw new System.NotImplementedException();
        }
    }
}
