using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        public uint Index
        {
            get
            {
                if (ChannelList == null)
                {
                    return uint.MaxValue;
                }

                return ChannelList.Min(arch => arch.Index);
            }
        }

        public int Device { get; set; }
        public int Type { get; set; }
        public int Loop { get; set; }
        public PsbString LoopStr { get; set; }
        public int Quality { get; set; }
        /// <summary>
        /// File
        /// </summary>
        public string FileString { get; set; }

        public PsbAudioFormat AudioFormat => ChannelList.Count > 0 ? ChannelList[0].Format : PsbAudioFormat.None;
        public PsbSpec Spec { get; set; } = PsbSpec.other;

        public void Link(string fullPath, FreeMountContext context)
        {
            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            switch (ext)
            {
                case ".at9":
                    var arch = (Atrac9ArchData) ChannelList[0];
                    arch.Data.Data = File.ReadAllBytes(fullPath);
                    break;
                case ".wav":
                case ".ogg":
                    var newArch = context.WaveToArchData(ChannelList[0].Extension, File.ReadAllBytes(fullPath), ChannelList[0].WaveExtension);
                    if (newArch != null)
                    {
                        ChannelList[0] = newArch;
                    }
                    else
                    {
                        if (ChannelList[0].Extension == ext)
                        {
                            LoadFromRawFile(ChannelList[0], fullPath);
                        }
                        else
                        {
                            Console.WriteLine($"[WARN] There is no encoder for {ChannelList[0].Extension}! {fullPath} is not used.");
                        }
                    }
                    break;
                case ".bin":
                case ".raw":
                    default:
                    LoadFromRawFile(ChannelList[0], fullPath);
                    break;
            }
            
        }

        private void LoadFromRawFile(IArchData channel, string fullPath)
        {
            switch (channel)
            {
                case XwmaArchData xwma:
                    xwma.Data.Data = File.ReadAllBytes(fullPath);
                    var dpds = Path.ChangeExtension(fullPath, ".dpds");
                    if (File.Exists(dpds))
                    {
                        xwma.Dpds.Data = File.ReadAllBytes(dpds);
                    }
                    
                    var fmt = Path.ChangeExtension(fullPath, ".fmt");
                    if (File.Exists(fmt))
                    {
                        xwma.Fmt.Data = File.ReadAllBytes(fmt);
                    }
                    break;
                case Atrac9ArchData at9:
                    at9.Data.Data = File.ReadAllBytes(fullPath);
                    break;
                case OpusArchData opus:
                    opus.Data.Data = File.ReadAllBytes(fullPath);
                    break;
                default:
                    Console.WriteLine($"[WARN] {fullPath} is not used.");
                    break;
            }
        }

        public string GetFileName(string ext = ".wav")
        {
            if (string.IsNullOrWhiteSpace(ext))
            {
                ext = ".wav";
            }
            var nameWithExt = Name.EndsWith(ext) ? Name : Name + ext;
            return nameWithExt;

            //if (string.IsNullOrWhiteSpace(FileString))
            //{
            //    return Name;
            //}

            //var fileName = Path.GetFileName(FileString);
            //return string.IsNullOrEmpty(fileName) ? Name : fileName.EndsWith(ext) ? fileName : fileName + ext;
        }

        public bool TryToWave(FreeMountContext context, out List<byte[]> waveChannels)
        {
            waveChannels = null;
            if (context == null)
            {
                return false;
            }

            waveChannels = new List<byte[]>(ChannelList.Count);
            var result = true;
            foreach (var channel in ChannelList)
            {
                var bytes = channel.TryToWave(context);
                if (bytes == null)
                {
                    result = false;
                }
                else
                {
                    waveChannels.Add(bytes);
                }
            }

            return result;
        }

        public List<IArchData> ChannelList { get; set; } = new List<IArchData>();
    }

    public class WavArchData : IArchData
    {
        public uint Index => Data.Index ?? uint.MaxValue;
        public string Extension => ".wav";
        public string WaveExtension { get; set; } = ".wav";
        public PsbAudioFormat Format => PsbAudioFormat.WAV;

        public PsbResource Data { get; set; }
    }

    public class XwmaArchData : IArchData
    {
        public PsbResource Data { get; set; }
        public PsbResource Dpds { get; set; }
        public PsbResource Fmt { get; set; }

        public string Wav { get; set; }


        public uint Index => Data.Index ?? uint.MaxValue;
        public string Extension => Format.DefaultExtension();
        public string WaveExtension { get; set; } = ".wav";
        public PsbAudioFormat Format => PsbAudioFormat.XWMA;
    }

    public class OpusArchData : IArchData
    {
        public PsbResource Data { get; set; }
        public int SampleCount { get; set; }
        public int ChannelCount { get; set; }
        public int SampRate { get; set; }

        public uint Index => Data.Index ?? uint.MaxValue;
        public string Extension => Format.DefaultExtension();
        public string WaveExtension { get; set; } = ".ogg";
        public PsbAudioFormat Format => PsbAudioFormat.OPUS;
    }

    public class Atrac9ArchData : IArchData
    {
        public PsbResource Data { get; set; }

        public uint Index => Data.Index ?? uint.MaxValue;
        public string Extension => Format.DefaultExtension();
        public string WaveExtension { get; set; } = ".wav";
        public PsbAudioFormat Format => PsbAudioFormat.Atrac9;
    }
}
