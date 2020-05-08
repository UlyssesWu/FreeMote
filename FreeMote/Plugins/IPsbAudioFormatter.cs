using System.Collections.Generic;

namespace FreeMote.Plugins
{
    public interface IPsbAudioFormatter : IPsbPlugin
    {
        /// <summary>
        /// Target Extension (if have) e.g. ".wav"
        /// </summary>
        List<string> Extensions { get; }
        bool CanToWave(in byte[] data, Dictionary<string, object> context = null);
        bool CanToBytes(byte[] wave, Dictionary<string, object> context = null);
        byte[] ToWave(in byte[] data, Dictionary<string, object> context = null);
        byte[] ToBytes(byte[] bitmap, Dictionary<string, object> context = null);
    }
}
