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

        public List<IArchData> ChannelList { get; set; } = new List<IArchData>();

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
        public PsbType PsbType { get; set; } = PsbType.SoundArchive;

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

                    if (ChannelList[0].ChannelPan == PsbAudioPan.IntroBody)
                    {
                        return PsbAudioPan.IntroBody;
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

        /// <summary>
        /// Link an audio file into PSB
        /// <para>Have special handling for multiple channels</para>
        /// </summary>
        /// <param name="fullPath"></param>
        /// <param name="context"></param>
        public void Link(string fullPath, FreeMountContext context)
        {
            if (ChannelList == null || ChannelList.Count == 0)
            {
                return;
            }
            if (ChannelList.Count > 1 && AudioFormat != PsbAudioFormat.Unknown && AudioFormat != PsbAudioFormat.ADPCM)
            {
                Console.WriteLine("[WARN] Audio with multiple channels is not supported. Send me the sample for research.");
            }
            //audio.vag.l.wav
            var ext = Path.GetExtension(fullPath).ToLowerInvariant(); //.wav
            var fileName = Path.GetFileNameWithoutExtension(fullPath); //audio.vag
            //var secondExt = Path.GetExtension(fileName).ToLowerInvariant();
            var secondExt = fullPath.GetSecondExtension(); //.vag
            var targetChannel = ChannelList[0];
            if (!string.IsNullOrEmpty(secondExt))
            {
                if (secondExt == ".l")
                {
                    targetChannel = GetLeftChannel();
                }
                else if (secondExt == ".r")
                {
                    targetChannel = GetRightChannel();
                }
                else if (secondExt.StartsWith(".#"))
                {
                    var no = secondExt.Substring(2);
                    if (int.TryParse(no, out var n))
                    {
                        if (ChannelList.Count > n)
                        {
                            Console.WriteLine("[WARN] Channel index is out of range.");
                        }
                        targetChannel = ChannelList[n];
                    }
                    else
                    {
                        Console.WriteLine("[WARN] Cannot parse channel index.");
                    }
                }
            }
            switch (ext)
            {
                case ".at9":
                    var at9Arch = (PsArchData)targetChannel;
                    at9Arch.Format = PsbAudioFormat.Atrac9;
                    at9Arch.Data.Data = File.ReadAllBytes(fullPath);
                    break;
                case ".vag":
                    var vagArch = (PsArchData)targetChannel;
                    vagArch.Format = PsbAudioFormat.VAG;
                    vagArch.Data.Data = File.ReadAllBytes(fullPath);
                    break;
                case ".xwma":
                    var xwmaArch = (XwmaArchData)targetChannel;
                    xwmaArch.ReadFromXwma(File.OpenRead(fullPath));
                    break;
                case ".adpcm":
                    var adpcmArch = (OpusArchData)targetChannel;
                    adpcmArch.Data.Data = File.ReadAllBytes(fullPath);
                    break;
                case ".wav":
                case ".ogg":
                    var realExt = targetChannel.Extension; //.vag
                    if (string.IsNullOrEmpty(realExt) && !string.IsNullOrEmpty(secondExt)) //use extension from file - .vag
                    {
                        realExt = secondExt;
                        var secondFileName = Path.GetFileNameWithoutExtension(fileName); //audio
                        if (!string.IsNullOrEmpty(secondFileName))
                        {
                            fileName = secondFileName;
                        }
                    }

                    if (File.Exists(fullPath))
                    {
                        LoadFileToChannel(targetChannel, fullPath, fileName, ext, realExt, context);
                    }
                    else
                    {
                        if (Pan == PsbAudioPan.LeftRight) //maybe left and right...
                        {
                            var leftWav = Path.ChangeExtension(fullPath, ".l" + ext);
                            if (File.Exists(leftWav))
                            {
                                LoadFileToChannel(GetLeftChannel(), leftWav, fileName, ext, realExt, context);
                            }

                            var rightWav = Path.ChangeExtension(fullPath, ".r" + ext);
                            if (File.Exists(rightWav))
                            {
                                LoadFileToChannel(GetRightChannel(), rightWav, fileName, ext, realExt, context);
                            }
                        }

                        if (Pan is PsbAudioPan.Body or PsbAudioPan.IntroBody)
                        {
                            var bodyWav = Path.ChangeExtension(fullPath, ".body" + ext);
                            if (File.Exists(bodyWav))
                            {
                                LoadFileToChannel(ChannelList[0], bodyWav, ".body", ext, realExt, context);
                            }
                        }

                        if (Pan is PsbAudioPan.Intro or PsbAudioPan.IntroBody)
                        {
                            var introWav = Path.ChangeExtension(fullPath, ".intro" + ext);
                            if (File.Exists(introWav))
                            {
                                LoadFileToChannel(ChannelList[0], introWav, ".intro", ext, realExt, context);
                            }
                        }

                        if (Pan == PsbAudioPan.Multiple) //maybe multi channel...
                        {
                            foreach (var channel in ChannelList)
                            {
                                if (channel.Data.Index == null)
                                {
                                    Console.WriteLine($"[WARN] Channel is not loaded: Channel resource don't have a Index.");
                                    continue;
                                }

                                var channelFileName = Path.ChangeExtension(fullPath, $".#{channel.Data.Index.Value}{ext}");
                                if (!File.Exists(channelFileName))
                                {
                                    Console.WriteLine(
                                        $"[WARN] Channel is not loaded: Failed to find Channel resource from {channelFileName}");
                                    continue;
                                }

                                LoadFileToChannel(channel, channelFileName, fileName, ext, realExt, context);
                            }
                        }
                    }

                    break;
                case ".bin":
                case ".raw":
                default:
                    LoadFromRawFile(targetChannel, fullPath);
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
                case OpusArchData opus:
                    if (opus.ChannelPan == PsbAudioPan.IntroBody)
                    {
                        throw new FormatException("Cannot load OPUS with body+intro channels");
                    }
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
        /// Load a common audio file into a channel
        /// </summary>
        /// <param name="channel">target channel</param>
        /// <param name="fullPath">path to load audio file</param>
        /// <param name="fileName">used in some audio types to keep file name</param>
        /// <param name="fileExt">common audio extension like "wav"</param>
        /// <param name="encodeExt">encode audio extension like "vag"</param>
        /// <param name="context"></param>
        private void LoadFileToChannel(IArchData channel, string fullPath, string fileName, string fileExt, string encodeExt, FreeMountContext context)
        {
            var result = context.WaveToArchData(this, channel, encodeExt, File.ReadAllBytes(fullPath), fileName,
                channel.WaveExtension);
            if (result)
            {
                channel.SetPsbArchData(channel.ToPsbArchData());
            }
            else
            {
                if (channel.Extension == fileExt)
                {
                    LoadFromRawFile(channel, fullPath);
                }
                else
                {
                    Console.WriteLine(
                        $"[WARN] There is no encoder for {channel.Extension}! {fullPath} is not used.");
                }
            }
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

        //public bool TryToWave(FreeMountContext context, out List<byte[]> waveChannels)
        //{
        //    waveChannels = null;
        //    if (context == null)
        //    {
        //        return false;
        //    }

        //    waveChannels = new List<byte[]>(ChannelList.Count);
        //    var result = true;
        //    foreach (var channel in ChannelList)
        //    {
        //        var bytes = channel.TryToWave(context);
        //        if (bytes == null)
        //        {
        //            result = false;
        //        }
        //        else
        //        {
        //            waveChannels.Add(bytes);
        //        }
        //    }

        //    return result;
        //}

        internal IArchData GetLeftChannel()
        {
            if (Pan != PsbAudioPan.LeftRight)
            {
                return null;
            }

            return ChannelList.First(c => c.ChannelPan == PsbAudioPan.Left);
        }

        internal IArchData GetRightChannel()
        {
            if (Pan != PsbAudioPan.LeftRight)
            {
                return null;
            }

            return ChannelList.First(c => c.ChannelPan == PsbAudioPan.Right);
        }
    }


}