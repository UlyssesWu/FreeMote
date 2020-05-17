using System.Collections.Generic;
using FreeMote.Psb;

namespace FreeMote.Plugins
{
    public interface IPsbAudioFormatter : IPsbPlugin
    {
        /// <summary>
        /// Target Extension (if have) e.g. ".wav"
        /// </summary>
        List<string> Extensions { get; }
        bool CanToWave(IArchData archData, Dictionary<string, object> context = null);
        bool CanToArchData(byte[] wave, Dictionary<string, object> context = null);
        byte[] ToWave(IArchData archData, Dictionary<string, object> context = null);
        IArchData ToArchData(byte[] wave, Dictionary<string, object> context = null);
    }
}
