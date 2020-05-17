using System.Collections.Generic;
using System.Diagnostics;
using FreeMote.Plugins;

namespace FreeMote.Psb
{
    /// <summary>
    /// Information for Audio Resource
    /// </summary>
    [DebuggerDisplay("{" + nameof(Name) + "}")]
    public class AudioMetadata : IResourceMetadata
    {
        public string Name { get; set; }

        /// <summary>
        /// Index is a value for tracking resource when compiling.
        /// </summary>
        public uint Index
        {
            get => Resource.Index ?? uint.MaxValue;
            set
            {
                if (Resource != null)
                {
                    Resource.Index = value;
                }
            }
        }

        public int Loop { get; set; }
        public PsbString LoopStr { get; set; }
        public int Quality { get; set; }
        public string File { get; set; }
        public PsbAudioFormat AudioFormat { get; set; }
        public PsbSpec Spec { get; set; } = PsbSpec.other;
        public PsbResource Resource { get; set; }

        public byte[] Link(string fullPath, FreeMountContext context)
        {
            throw new System.NotImplementedException();
        }

        public List<IArchData> ChannelList { get; set; }
    }

    public interface IArchData
    {
        PsbAudioFormat Format { get; }
        bool CanEncode { get; }
        bool CanDecode { get; }
    }

    public class Atrac9ArchData : IArchData
    {
        public PsbResource Data { get; set; }
        public PsbResource Dpds { get; set; }
        public PsbResource Fmt { get; set; }

        public string Wav { get; set; }


        public PsbAudioFormat Format => PsbAudioFormat.Atrac9;
        public bool CanEncode => false;
        public bool CanDecode => true;
    }

    public class OpusArchData : IArchData
    {
        public PsbResource Data { get; set; }
        public int SampleCount { get; set; }
        public int ChannelCount { get; set; }
        public int SampRate { get; set; }

        public PsbAudioFormat Format => PsbAudioFormat.OPUS;
        public bool CanEncode { get; }
        public bool CanDecode { get; }
    }
}
