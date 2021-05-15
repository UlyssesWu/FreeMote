using System.Collections.Generic;
using FreeMote.Psb;

namespace FreeMote.Plugins
{
    /// <summary>
    /// Handle audio conversion
    /// </summary>
    public interface IPsbAudioFormatter : IPsbPlugin
    {
        /// <summary>
        /// Target Extension (if have) e.g. ".wav"
        /// </summary>
        List<string> Extensions { get; }

        /// <summary>
        /// Check if <see cref="ToWave"/> is available
        /// </summary>
        /// <param name="archData"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        bool CanToWave(IArchData archData, Dictionary<string, object> context = null);

        /// <summary>
        /// Check if <see cref="ToArchData"/> is available
        /// </summary>
        /// <param name="wave"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        bool CanToArchData(byte[] wave, Dictionary<string, object> context = null);

        /// <summary>
        /// Convert <see cref="IArchData"/> to wave bytes
        /// </summary>
        /// <param name="md"></param>
        /// <param name="archData"></param>
        /// <param name="fileName"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        byte[] ToWave(AudioMetadata md, IArchData archData, string fileName = null, Dictionary<string, object> context = null);

        /// <summary>
        /// Convert wave bytes to <see cref="IArchData"/>
        /// </summary>
        /// <param name="md"></param>
        /// <param name="archData">the archData to be filled</param>
        /// <param name="wave"></param>
        /// <param name="fileName"></param>
        /// <param name="waveExt"></param>
        /// <param name="context"></param>
        /// <returns>true if filled.</returns>
        bool ToArchData(AudioMetadata md, IArchData archData, in byte[] wave, string fileName, string waveExt, Dictionary<string, object> context = null);

        /// <summary>
        /// Used when collecting audio resource, the data could be null when compiling
        /// </summary>
        /// <param name="md"><see cref="AudioMetadata.ChannelList"/> is still null at this time</param>
        /// <param name="channel">an object in [channelList]</param>
        /// <param name="data"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        bool TryGetArchData(AudioMetadata md, PsbDictionary channel, out IArchData data, Dictionary<string, object> context = null);
    }
}
