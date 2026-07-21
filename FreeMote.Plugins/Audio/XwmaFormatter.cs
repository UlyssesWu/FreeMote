using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using FreeMote.Psb;

//REF: https://wiki.multimedia.cx/index.php/Microsoft_xWMA

namespace FreeMote.Plugins.Audio
{
    [Export(typeof(IPsbAudioFormatter))]
    [ExportMetadata("Name", "FreeMote.Xwma")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "XWMA support.")]
    class XwmaFormatter : IPsbAudioFormatter
    {
        public List<string> Extensions { get; } = new List<string> {".xwma", ".xwm"};

        private const string EncoderTool = "xWMAEncode.exe";
        private static int _useToolNotice;
        private static int _missingToolNotice;

        public string ToolPath { get; set; } = null;

        public XwmaFormatter()
        {
            var toolPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? "", "Tools", EncoderTool);
            if (File.Exists(toolPath))
            {
                ToolPath = toolPath;
            }
        }

        public bool CanToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            return archData is XwmaArchData;
        }

        public bool CanToArchData(byte[] wave, Dictionary<string, object> context = null)
        {
            if (!File.Exists(ToolPath))
            {
                LogMissingToolNotice();
                return false;
            }

            return wave != null;
        }

        public byte[] ToWave(AudioMetadata md, IArchData archData, string fileName = null, Dictionary<string, object> context = null)
        {
            if (!File.Exists(ToolPath))
            {
                LogMissingToolNotice();
                archData.WaveExtension = Extensions[0];
                return ((XwmaArchData)archData).ToXwma();
            }

            LogUseToolNotice();
            archData.WaveExtension = ".wav";
            var xwmaBytes = ((XwmaArchData) archData).ToXwma();
            var tempFile = Path.GetTempFileName();
            File.WriteAllBytes(tempFile, xwmaBytes);
            var tempOutFile = Path.GetTempFileName();

            byte[] outBytes = null;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(ToolPath, $"\"{tempFile}\" \"{tempOutFile}\"")
                {
                    WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true
                };
                Process process = Process.Start(info);
                process?.WaitForExit();

                outBytes = File.ReadAllBytes(tempOutFile);
                File.Delete(tempFile);
                File.Delete(tempOutFile);
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
                

            return outBytes;
        }
        
        public bool ToArchData(AudioMetadata md, IArchData archData, in byte[] wave, string fileName, string waveExt, Dictionary<string, object> context = null)
        {
            if (!File.Exists(ToolPath))
            {
                LogMissingToolNotice();
                return false;
            }

            if (archData is not XwmaArchData xwma)
            {
                return false;
            }

            LogUseToolNotice();
            var tempFile = Path.GetTempFileName();
            File.WriteAllBytes(tempFile, wave);
            var tempOutFile = Path.GetTempFileName();
            MemoryStream oms = null;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(ToolPath, $"\"{tempFile}\" \"{tempOutFile}\"")
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                Process process = Process.Start(info);
                process?.WaitForExit();
                
                var fs = File.OpenRead(tempOutFile);
                oms = new MemoryStream((int)fs.Length);
                fs.CopyTo(oms);
                oms.Position = 0;

                File.Delete(tempFile);
                File.Delete(tempOutFile);
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }

            if (oms == null)
            {
                return false;
            }
            
            xwma.ReadFromXwma(oms);
            oms.Dispose();
            
            return true;
        }

        private static void LogUseToolNotice()
        {
            if (Interlocked.Exchange(ref _useToolNotice, 1) == 0)
            {
                Logger.LogHint("[XWMA] Try using xWMAEncode to encode/decode...");
            }
        }

        private static void LogMissingToolNotice()
        {
            if (Interlocked.Exchange(ref _missingToolNotice, 1) == 0)
            {
                Logger.LogWarn("[XWMA] xWMAEncode was not found. Decoding will export xWMA data, which may not be directly playable and may require manual conversion; PCM WAV cannot be encoded automatically.");
            }
        }

        public bool TryGetArchData(AudioMetadata md, PsbDictionary channel, out IArchData data, Dictionary<string, object> context = null)
        {
            data = null;
            if (md.Spec == PsbSpec.win)
            {
                if (channel.Count == 1 && channel["archData"] is PsbDictionary archDic && archDic["data"] is PsbResource aData && archDic["dpds"] is PsbResource aDpds && archDic["fmt"] is PsbResource aFmt && archDic["wav"] is PsbString aWav)
                {
                    data = new XwmaArchData()
                    {
                        Data = aData,
                        Fmt = aFmt,
                        Dpds = aDpds,
                        Wav = aWav.Value
                    };

                    return true;
                }

                return false;
            }

            return false;
        }
    }
}
