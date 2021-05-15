using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using FreeMote.Psb;

namespace FreeMote.Plugins.Audio
{
    [Export(typeof(IPsbAudioFormatter))]
    [ExportMetadata("Name", "FreeMote.Vag")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "VAG support.")]
    class VagFormatter : IPsbAudioFormatter
    {
        public List<string> Extensions { get; } = new List<string> { ".vag" };

        private const string EncoderTool = "vagconv2.exe";
        private const string DecoderTool = "vgmstream.exe";

        public string ToolPath { get; set; } = null;

        public VagFormatter()
        {
            var toolPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? "", "Tools", EncoderTool);
            if (File.Exists(toolPath))
            {
                ToolPath = toolPath;
            }
        }
        public bool CanToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            if (archData is PsArchData psArch)
            {
                if (psArch.Data?.Data != null && psArch.Data.Data.Length > 4)
                {
                    if (psArch.Data.Data.AsciiEqual("VAGp"))
                    {
                        return true;
                    }
                }
            }

            return false;
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

        public byte[] ToWave(AudioMetadata md, IArchData archData, string fileName = null, Dictionary<string, object> context = null)
        {
            VagFile vag = new VagFile();
            if (vag.LoadFromStream(new MemoryStream(archData.Data.Data)))
            {
                return vag.ToWave().ToArray();
            }

            return null;
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

            var tempPath = Path.GetTempPath();
            var tempFile = Path.Combine(tempPath, fileName);
            if (tempFile.EndsWith(".vag"))
            {
                tempFile = Path.ChangeExtension(tempFile, "");
            }
            File.WriteAllBytes(tempFile, wave);
            var tempOutFile = Path.ChangeExtension(tempFile, ".vag");

            byte[] outBytes = null;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(ToolPath, $"\"{tempFile}\"")
                {
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                Process process = Process.Start(info);
                process?.WaitForExit();
                if (!File.Exists(tempOutFile) || process?.ExitCode != 0)
                {
                    Console.WriteLine("[ERROR] VAG convert failed.");
                    return false;
                }

                outBytes = File.ReadAllBytes(tempOutFile);
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
            data.Format = PsbAudioFormat.VAG;

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
                        if (res.Data.AsciiEqual("VAGp"))
                        {
                            data = new PsArchData
                            {
                                Data = res,
                                Format = PsbAudioFormat.VAG
                            };
                            return true;
                        }

                        //...but the data is other format (at9)
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
    }
}
