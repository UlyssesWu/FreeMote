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
        IArchData ToArchData(in byte[] wave, string fileName, string waveExt, Dictionary<string, object> context = null);

        /// <summary>
        /// Used when collecting audio resource, the data could be null when compiling
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="dic"></param>
        /// <param name="data"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        bool TryGetArchData(PSB psb, PsbDictionary dic, out IArchData data, Dictionary<string, object> context = null);
    }
}
