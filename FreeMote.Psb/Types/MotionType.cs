namespace FreeMote.Psb.Types
{
    class MotionType : IPsbType
    {
        public PsbType PsbType => PsbType.Motion;
        public bool IsThisType(PSB psb)
        {
            throw new System.NotImplementedException();
        }
    }
}
