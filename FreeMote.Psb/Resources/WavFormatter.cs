using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using FreeMote.Plugins;

namespace FreeMote.Psb
{
    [Export(typeof(IPsbAudioFormatter))]
    [ExportMetadata("Name", "FreeMote.Wav")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "Wav support.")]
    class WavFormatter : IPsbAudioFormatter
    {
        public List<string> Extensions { get; } = new List<string> { ".wav" };
        public bool CanToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            return archData is WavArchData;
        }

        public bool CanToArchData(byte[] wave, Dictionary<string, object> context = null)
        {
            return wave != null;
        }

        public byte[] ToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            WavArchData arch = archData as WavArchData;
            return arch?.ToWav();
        }

        public IArchData ToArchData(AudioMetadata md, in byte[] wave, string fileName, string waveExt, Dictionary<string, object> context = null)
        {
            WavArchData arch = new WavArchData();

            using var oms = new MemoryStream(wave);
            arch.ReadFromWav(oms);
            if (md != null && md.LoopStr != null)
            {
                arch.Loop = PsbResHelper.ParseLoopStr(md.LoopStr.Value);
            }

            return arch;
        }

        public bool TryGetArchData(PSB psb, PsbDictionary channel, out IArchData data, Dictionary<string, object> context = null)
        {
            data = null;
            if (psb.Platform == PsbSpec.win)
            {
                if (channel.Count == 1 && channel["archData"] is PsbDictionary archDic && !archDic.ContainsKey("dpds") && archDic["data"] is PsbResource aData && archDic["fmt"] is PsbResource aFmt && archDic["wav"] is PsbString aWav)
                {
                    var newData = new WavArchData
                    {
                        Data = aData,
                        Fmt = aFmt,
                        Wav = aWav.Value
                    };

                    if (archDic["loop"] is PsbList aLoop)
                    {
                        newData.Loop = aLoop;
                    }

                    data = newData;
                    return true;
                }

                return false;
            }

            return false;
        }
    
    }
}
