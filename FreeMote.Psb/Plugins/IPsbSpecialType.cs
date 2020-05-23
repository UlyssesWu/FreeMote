using FreeMote.Psb;

namespace FreeMote.Plugins
{
    public interface IPsbSpecialType : IPsbType, IPsbPlugin
    {
        string TypeId { get; }
    }
}
