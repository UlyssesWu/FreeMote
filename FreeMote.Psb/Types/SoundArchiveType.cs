using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FreeMote.Plugins;

namespace FreeMote.Psb.Types
{
    class SoundArchiveType : IPsbType
    {
        public const string VoiceResourceKey = "voice";
        public PsbType PsbType => PsbType.SoundArchive;
        public bool IsThisType(PSB psb)
        {
            return psb.TypeId == "sound_archive";
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : IResourceMetadata
        {
            List<T> resourceList = psb.Resources == null
                ? new List<T>()
                : new List<T>(psb.Resources.Count);

            if (psb.Objects[VoiceResourceKey] is PsbDictionary voiceDic)
            {
                var context = FreeMount.CreateContext();
                foreach (var voice in voiceDic)
                {
                    if (voice.Value is PsbDictionary voiceValue)
                    {
                        resourceList.Add((T)(IResourceMetadata)GenerateAudioMetadata(psb, voice.Key, voiceValue, context));
                    }
                }
            }
            else
            {
                return resourceList;
            }

            //Set Spec
            resourceList.ForEach(r => r.Spec = psb.Platform);
            //resourceList.Sort((md1, md2) => (int)(md1.Index - md2.Index));

            return resourceList;
        }


        internal AudioMetadata GenerateAudioMetadata(PSB psb, string name, PsbDictionary voice, FreeMountContext context)
        {
            var md = new AudioMetadata {Name = name};

            if (voice["file"] is PsbString fileStr)
            {
                md.FileString = fileStr;
            }

            if (voice["loopstr"] is PsbString loopstr) //Another bad design. channel[L]ist vs loop[s]tr. WTF M2??
            {
                md.LoopStr = loopstr;
            }
            else if(voice["loopStr"] is PsbString loopStr) 
            {
                md.LoopStr = loopStr;
            }

            if (md.LoopStr != null)
            {
                context.Context["loopstr"] = md.LoopStr.Value;
            }

            if (voice["loop"] is PsbNumber loopNum)
            {
                md.Loop = loopNum.IntValue;
            }

            if (voice["quality"] is PsbNumber qualityNum)
            {
                md.Quality = qualityNum.IntValue;
            }

            if (voice["type"] is PsbNumber typeNum)
            {
                md.Type = typeNum.IntValue;
            }

            if (voice["device"] is PsbNumber deviceNum)
            {
                md.Device = deviceNum.IntValue;
            }

            if (voice["channelList"] is PsbList channelList)
            {
                foreach (var channel in channelList)
                {
                    if (channel is PsbDictionary channelDic)
                    {
                        if (context.TryGetArchData(psb, channelDic, out var archData))
                        {
                            archData.PsbArchData = channelDic;
                            md.ChannelList.Add(archData);
                        }
                    }
                }
            }

            return md;
        }

        public void Link(PSB psb, FreeMountContext context, IList<string> resPaths, string baseDir = null,
            PsbLinkOrderBy order = PsbLinkOrderBy.Convention)
        {
            var rawResList = psb.CollectResources<AudioMetadata>();
            if (order == PsbLinkOrderBy.Order)
            {
                for (int i = 0; i < rawResList.Count; i++)
                {
                    var resMd = rawResList[i];
                    var fullPath = Path.Combine(baseDir ?? "", resPaths[i]);
                    resMd.Link(fullPath, context);
                }

                return;
            }

            foreach (var resPath in resPaths)
            {
                var resMd = rawResList.FirstOrDefault(r => r.Name == Path.GetFileNameWithoutExtension(resPath));
                if (resMd == null)
                {
                    Console.WriteLine($"[WARN] {resPath} is not used.");
                    continue;
                }

                var fullPath = Path.Combine(baseDir ?? "", resPath);
                resMd.Link(fullPath, context);
            }
        }
        
        public void Link(PSB psb, FreeMountContext context, IDictionary<string, string> resPaths, string baseDir = null)
        {
            var rawResList = psb.CollectResources<AudioMetadata>();

            foreach (var resPath in resPaths)
            {
                var fullPath = Path.Combine(baseDir ?? "", resPath.Value);
                var resMd = rawResList.FirstOrDefault(r => r.Name == resPath.Key);
                if (resMd == null)
                {
                    if (uint.TryParse(resPath.Key, out var idx))
                    {
                        var resource = psb.Resources.FirstOrDefault(r => r.Index == idx);
                        if (resource != null)
                        {
                            resource.Data = File.ReadAllBytes(fullPath);
                        }
                        else
                        {
                            Console.WriteLine($"[WARN] {resPath.Key} is not used.");
                        }
                    }
                }
                else
                {
                    resMd.Link(fullPath, context);
                }
            }
        }

        public void UnlinkToFile(PSB psb, FreeMountContext context, string name, string dirPath, bool outputUnlinkedPsb = true,
            PsbLinkOrderBy order = PsbLinkOrderBy.Name)
        {
            //TODO:
        }

        public Dictionary<string, string> OutputResources(PSB psb, FreeMountContext context, string name, string dirPath,
            PsbExtractOption extractOption = PsbExtractOption.Original)
        {
            var resources = psb.CollectResources<AudioMetadata>();
            Dictionary<string, string> resDictionary = new Dictionary<string, string>();

            if (extractOption == PsbExtractOption.Original)
            {
                for (int i = 0; i < psb.Resources.Count; i++)
                {
                    var relativePath = psb.Resources[i].Index == null ? $"#{i}.raw" : $"{psb.Resources[i].Index}.raw";

                    File.WriteAllBytes(
                        Path.Combine(dirPath, relativePath),
                        psb.Resources[i].Data);
                    resDictionary.Add(Path.GetFileNameWithoutExtension(relativePath), $"{name}/{relativePath}");
                }
            }
            else
            {
                foreach (var resource in resources)
                {
                    if (resource.ChannelList.Count == 1)
                    {
                        var bts = resource.ChannelList[0].TryToWave(context);
                        var relativePath = resource.GetFileName(resource.ChannelList[0].Extension + resource.ChannelList[0].WaveExtension); //WaveExtension may change after ToWave
                        if (bts != null)
                        {
                            File.WriteAllBytes(Path.Combine(dirPath, relativePath), bts);
                            resDictionary.Add(resource.Name, $"{name}/{relativePath}");
                        }
                    }
                    else if (resource.ChannelList.Count > 1)
                    {
                        if (resource.Pan == PsbAudioPan.LeftRight) //load audio.vag.l.wav & audio.vag.r.wav
                        {
                            var left = resource.GetLeftChannel();
                            var relativePathL = resource.GetFileName($"{left.Extension}.l{left.WaveExtension}");
                            var btsL = left.TryToWave(context);
                            if (btsL != null)
                            {
                                File.WriteAllBytes(Path.Combine(dirPath, relativePathL), btsL);
                            }
                            else
                            {
                                relativePathL = resource.GetFileName($"{left.Extension}.l{left.Extension}");
                                File.WriteAllBytes(Path.Combine(dirPath, relativePathL), left.Data.Data);
                                resDictionary.Add(resource.Name, $"{name}/{relativePathL}");
                            }

                            var right = resource.GetRightChannel();
                            var relativePathR = resource.GetFileName($"{right.Extension}.r{right.WaveExtension}");
                            var btsR = right.TryToWave(context);
                            if (btsR != null)
                            {
                                File.WriteAllBytes(Path.Combine(dirPath, relativePathR), btsR);
                            }
                            else
                            {
                                relativePathR = resource.GetFileName($"{right.Extension}.r{right.Extension}");
                                File.WriteAllBytes(Path.Combine(dirPath, relativePathR), right.Data.Data);
                                resDictionary.Add(resource.Name, $"{name}/{relativePathR}");
                            }

                            if (btsL != null && btsR != null)
                            {
                                var relativePath = resource.GetFileName($"{left.Extension}{left.WaveExtension}");
                                resDictionary.Add(resource.Name, $"{name}/{relativePath}"); //a virtual file path
                            }
                        }
                        else //not LeftRight
                        {
                            for (var j = 0; j < resource.ChannelList.Count; j++) //load audio.vag.1.wav etc.
                            {
                                var waveChannel = resource.ChannelList[j];
                                if (waveChannel.Data.Index == null)
                                {
                                    Console.WriteLine($"[WARN] Channel {j} is not linked with a Resource.");
                                    continue;
                                }
                                var bts = waveChannel.TryToWave(context);
                                var noStr = waveChannel.Data.Index == null ? $".@{j}" : $".#{waveChannel.Data.Index.Value}"; //TODO: handle @ - channel internal No.
                                var relativePath = resource.GetFileName($"{waveChannel.Extension}{noStr}{waveChannel.WaveExtension}");
                                if (bts != null)
                                {
                                    File.WriteAllBytes(Path.Combine(dirPath, relativePath), bts);
                                    resDictionary.Add(resource.Name, $"{name}/{relativePath}");
                                }
                            }
                        }
                    }
                }
            }

            return resDictionary;
        }
    }
}
