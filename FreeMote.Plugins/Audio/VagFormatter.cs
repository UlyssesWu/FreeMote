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
            if (archData is VagArchData)
            {
                return true;
            }

            return false;
        }

        public bool CanToArchData(byte[] wave, Dictionary<string, object> context = null)
        {
            if (!File.Exists(ToolPath))
            {
                return false;
            }

            return true;
        }

        public byte[] ToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            VagFile vag = new VagFile();
            if (vag.LoadFromStream(new MemoryStream(archData.Data.Data)))
            {
                return vag.ToWave().ToArray();
            }

            return null;
        }

        public IArchData ToArchData(byte[] wave, string waveExt, Dictionary<string, object> context = null)
        {
            if (!File.Exists(ToolPath))
            {
                return null;
            }

            var tempFile = Path.GetTempFileName();
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
                    return null;
                }

                outBytes = File.ReadAllBytes(tempOutFile);
                File.Delete(tempFile);
                File.Delete(tempOutFile);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            var arch = new VagArchData
            {
                Data = new PsbResource { Data = outBytes }
            };

            return arch;
        }

        public bool TryGetArchData(PSB psb, PsbDictionary dic, out IArchData data)
        {
            data = null;
            if (psb.Platform == PsbSpec.ps4 || psb.Platform == PsbSpec.vita)
            {
                if (dic.Count == 1 && dic["archData"] is PsbResource res)
                {
                    if (res.Data.AsciiEqual("VAGp"))
                    {
                        data = new VagArchData
                        {
                            Data = res
                        };

                        return true;
                    }
                }

                return false;
            }

            return false;
        }
    }
}
