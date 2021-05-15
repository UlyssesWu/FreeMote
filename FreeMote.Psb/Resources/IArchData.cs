// ArchData in PSB is a typical bad design. It does not describe the true audio format,
// and different formats (such as AT9 and VAG) may share exactly same ArchData structure.
// We have to keep this info in resx.json for re-compile.
// An ArchData may have multiple channels, a channel may have multiple resources,
// making it difficult to re-compile.

using System.Collections.Generic;

namespace FreeMote.Psb
{
    /// <summary>
    /// Audio Arch Data (usually a Channel)
    /// </summary>
    public interface IArchData
    {
        uint Index { get; }
        /// <summary>
        /// Unusual audio extension, such as .vag (must start with dot)
        /// </summary>
        string Extension { get; }
        /// <summary>
        /// Common wave type after decode, usually .wav (must start with dot)
        /// </summary>
        string WaveExtension { get; set; }
        PsbAudioFormat Format { get; }
        PsbAudioPan ChannelPan { get; }
        /// <summary>
        /// key "data" in PSB
        /// </summary>
        PsbResource Data { get; }
        IList<PsbResource> DataList { get; }

        /// <summary>
        /// PSB object "archData" value
        /// </summary>
        PsbDictionary PsbArchData { get; set; }

        /// <summary>
        /// Generate PSB object "archData" value
        /// </summary>
        /// <returns></returns>
        IPsbValue ToPsbArchData();
    }
}
