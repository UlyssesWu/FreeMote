﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
            return true;
        }

        public bool CanToArchData(byte[] wave, Dictionary<string, object> context = null)
        {
            if (File.Exists(ToolPath))
            {
                return true;
            }
            return false;
        }

        public byte[] ToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            if (string.IsNullOrEmpty(ToolPath))
            {
                archData.WaveExtension = Extensions[0];
                return ((XwmaArchData)archData).ToXwma();
            }

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
                Console.WriteLine(e);
            }
                

            return outBytes;
        }
        
        public IArchData ToArchData(in byte[] wave, string fileName, string waveExt, Dictionary<string, object> context = null)
        {
            if (!File.Exists(ToolPath))
            {
                return null;
            }

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
                Console.WriteLine(e);
            }

            if (oms == null)
            {
                return null;
            }
            
            XwmaArchData data = new XwmaArchData();
            data.ReadFromXwma(oms);
            oms.Dispose();
            
            return data;
        }

        public bool TryGetArchData(PSB psb, PsbDictionary dic, out IArchData data, Dictionary<string, object> context = null)
        {
            data = null;
            if (psb.Platform == PsbSpec.win)
            {
                if (dic.Count == 1 && dic["archData"] is PsbDictionary archDic && archDic["data"] is PsbResource aData && archDic["dpds"] is PsbResource aDpds && archDic["fmt"] is PsbResource aFmt && archDic["wav"] is PsbString aWav)
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