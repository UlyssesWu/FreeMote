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
                if (ChannelList == null || ChannelList.Count == 0)
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

        public PsbAudioFormat AudioFormat => ChannelList.Count > 0 ? ChannelList[0].Format : PsbAudioFormat.Unknown;
        public PsbSpec Spec { get; set; } = PsbSpec.other;

        public PsbAudioPan Pan
        {
            get
            {
                if (ChannelList == null || ChannelList.Count == 0)
                {
                    return PsbAudioPan.Mono;
                }

                if (ChannelList.Count == 1)
                {
                    if (ChannelList[0].ChannelPan == PsbAudioPan.Stereo)
                    {
                        return PsbAudioPan.Stereo;
                    }

                    return PsbAudioPan.Mono;
                }

                if (ChannelList.Count == 2)
                {
                    if (ChannelList[0].ChannelPan == PsbAudioPan.Left && ChannelList[1].ChannelPan == PsbAudioPan.Right)
                    {
                        return PsbAudioPan.LeftRight;
                    }

                    if (ChannelList[0].ChannelPan == PsbAudioPan.Right && ChannelList[1].ChannelPan == PsbAudioPan.Left)
                    {
                        ////switch
                        //var temp = ChannelList[1];
                        //ChannelList[1] = ChannelList[0];
                        //ChannelList[0] = temp;
                        return PsbAudioPan.LeftRight;
                    }

                    return PsbAudioPan.Multiple;
                }

                return PsbAudioPan.Multiple;
            }
        }

        public void Link(string fullPath, FreeMountContext context)
        {
            if (ChannelList.Count > 1 && AudioFormat != PsbAudioFormat.Unknown && AudioFormat != PsbAudioFormat.ADPCM)
            {
                Console.WriteLine("[WARN] Audio with multiple channels is not supported. Send me the sample for research.");
            }
            
            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            switch (ext)
            {
                case ".at9":
                    var at9Arch = (PsArchData) ChannelList[0];
                    at9Arch.Format = PsbAudioFormat.Atrac9;
                    at9Arch.Data.Data = File.ReadAllBytes(fullPath);
                    break;
                case ".vag":
                    var vagArch = (PsArchData) ChannelList[0];
                    vagArch.Format = PsbAudioFormat.VAG;
                    vagArch.Data.Data = File.ReadAllBytes(fullPath);
                    break;
                case ".xwma":
                    var xwmaArch = (XwmaArchData) ChannelList[0];
                    xwmaArch.ReadFromXwma(File.OpenRead(fullPath));
                    break;
                case ".wav":
                case ".ogg":
                    //fullPath: audio.vag.wav
                    var realExt = ChannelList[0].Extension; //.vag
                    var fileName = Path.GetFileNameWithoutExtension(fullPath); //audio.vag
                    var secondExt = Path.GetExtension(fileName).ToLowerInvariant(); //.vag
                    if (string.IsNullOrEmpty(realExt) && !string.IsNullOrEmpty(secondExt)) //use extension from file - .vag
                    {
                        realExt = secondExt;
                        var secondFileName = Path.GetFileNameWithoutExtension(fileName); //audio
                        if (!string.IsNullOrEmpty(secondFileName))
                        {
                            fileName = secondFileName;
                        }
                    }
                    
                    var newArch = context.WaveToArchData(this, realExt, File.ReadAllBytes(fullPath), fileName,
                        ChannelList[0].WaveExtension);
                    if (newArch != null)
                    {
                        ChannelList[0].SetPsbArchData(newArch.ToPsbArchData());
                    }
                    else
                    {
                        if (ChannelList[0].Extension == ext)
                        {
                            LoadFromRawFile(ChannelList[0], fullPath);
                        }
                        else
                        {
                            Console.WriteLine(
                                $"[WARN] There is no encoder for {ChannelList[0].Extension}! {fullPath} is not used.");
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
                case PsArchData ps:
                    ps.Data.Data = File.ReadAllBytes(fullPath);
                    break;
                case NxArchData opus:
                    opus.Data.Data = File.ReadAllBytes(fullPath);
                    break;
                default:
                    channel.Data.Data = File.ReadAllBytes(fullPath);
                    //Console.WriteLine($"[WARN] {fullPath} is not used.");
                    break;
            }
            channel.SetPsbArchData(channel.ToPsbArchData());
        }

        /// <summary>
        /// Get Audio FileName for save
        /// </summary>
        /// <param name="ext"></param>
        /// <returns></returns>
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


}