using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using FreeMote.Psb;
using VGAudio.Containers.At9;
using VGAudio.Containers.Wave;

namespace FreeMote.Plugins.Audio
{
    [Export(typeof(IPsbAudioFormatter))]
    [ExportMetadata("Name", "FreeMote.At9")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "At9 support via VGAudio.")]
    public class At9Formatter : IPsbAudioFormatter
    {
        public const string At9BitRate = "At9BitRate";

        public List<string> Extensions { get; } = new List<string> {".at9"};

        private const string EncoderTool = "at9tool.exe";

        public string ToolPath { get; set; } = null;

        public At9Formatter()
        {
            var toolPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? "", "Tools", EncoderTool);
            if (File.Exists(toolPath))
            {
                ToolPath = toolPath;
            }
        }

        public bool CanToArchData(byte[] wave, Dictionary<string, object> context = null)
        {
            if (!File.Exists(ToolPath))
            {
                Console.WriteLine($"[WARN] Cannot convert without {EncoderTool}");
                return false;
            }

            return true;
        }

        public bool CanToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            if (archData is PsArchData psArch)
            {
                if (psArch.Data?.Data != null && psArch.Data.Data.Length > 4)
                {
                    if (psArch.Data.Data.AsciiEqual("RIFF"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool ToArchData(AudioMetadata md, IArchData archData, in byte[] wave, string fileName, string waveExt, Dictionary<string, object> context = null)
        {
            if (!File.Exists(ToolPath))
            {
                return false;
            }

            if (archData is not PsArchData data)
            {
                return false;
            }

            var tempFile = Path.GetTempFileName();
            File.WriteAllBytes(tempFile, wave);
            var tempOutFile = Path.GetTempFileName();

            byte[] outBytes = null;
            try
            {
                int bitRate = 96;
                if (context != null)
                {
                    if (context.ContainsKey(At9BitRate) && context[At9BitRate] is int br)
                    {
                        bitRate = br;
                    }
                    else
                    {
                        context[At9BitRate] = bitRate;
                    }
                }
                //br 96 for 1 channel, br 192 for 2 channels (need 2ch sample!)
                ProcessStartInfo info = new ProcessStartInfo(ToolPath, $"-e -br {bitRate} \"{tempFile}\" \"{tempOutFile}\"")
                {
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                Process process = Process.Start(info);
                process?.WaitForExit();

                outBytes = File.ReadAllBytes(tempOutFile);
                if (outBytes.Length == 0)
                {
                    Console.WriteLine("[WARN] AT9 encoder output length is 0");
                }

                File.Delete(tempFile);
                File.Delete(tempOutFile);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            if (data.Data == null)
            {
                data.Data = new PsbResource { Data = outBytes };
            }
            else
            {
                data.Data.Data = outBytes;
            }
            data.Format = PsbAudioFormat.Atrac9;

            return true;
        }

        public bool TryGetArchData(AudioMetadata md, PsbDictionary channel, out IArchData data, Dictionary<string, object> context = null)
        {
            data = null;
            if (md.Spec == PsbSpec.ps4 || md.Spec == PsbSpec.vita)
            {
                if (channel.Count == 1 && channel["archData"] is PsbResource res)
                {
                    if (res.Data != null && res.Data.Length > 0) //res data exists
                    {
                        if (res.Data.AsciiEqual("RIFF"))
                        {
                            data = new PsArchData
                            {
                                Data = res,
                                Format = PsbAudioFormat.Atrac9
                            };
                            return true;
                        }

                        //...but the data is other format (vag)
                        return false;
                    }

                    //res data is null, maybe linking
                    data = new PsArchData
                    {
                        Data = res
                    };
                    return true;
                }

                return false;
            }

            return false;
        }

        public byte[] ToWave(AudioMetadata md, IArchData archData, string fileName = null, Dictionary<string, object> context = null)
        {
            At9Reader reader = new At9Reader();
            //var format = reader.ReadFormat();
            var data = reader.Read(archData.Data.Data);
            using MemoryStream oms = new MemoryStream();
            WaveWriter writer = new WaveWriter();
            writer.WriteToStream(data, oms, new WaveConfiguration {Codec = WaveCodec.Pcm16Bit}); //only 16Bit supported
            return oms.ToArray();
        }
    }
}