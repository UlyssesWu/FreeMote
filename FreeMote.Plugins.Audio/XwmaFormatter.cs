using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
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
            return false;
        }

        public byte[] ToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            if (string.IsNullOrEmpty(ToolPath))
            {
                archData.WaveExtension = Extensions[0];
                return ((XwmaArchData)archData).ToXWMA();
            }
            else
            {
                return null; //TODO
            }
        }
        
        public IArchData ToArchData(byte[] wave, string waveExt, Dictionary<string, object> context = null)
        {
            throw new NotSupportedException($"XWMA encode is not supported. Use {EncoderTool} manually.");
        }

        public bool TryGetArchData(PSB psb, PsbDictionary dic, out IArchData data)
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