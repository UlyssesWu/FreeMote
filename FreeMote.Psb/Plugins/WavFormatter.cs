using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using FreeMote.Psb;
// ReSharper disable CheckNamespace

namespace FreeMote.Plugins
{
    [Export(typeof(IPsbAudioFormatter))]
    [ExportMetadata("Name", "FreeMote.Wav")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "Wav/P16 support.")]
    class WavFormatter : IPsbAudioFormatter
    {
        public List<string> Extensions { get; } = new List<string> { ".wav", ".p16" };
        public bool CanToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            return archData is WavArchData || archData is P16ArchData;
        }

        public bool CanToArchData(byte[] wave, Dictionary<string, object> context = null)
        {
            return wave != null;
        }

        public byte[] ToWave(AudioMetadata md, IArchData archData, string fileName = null, Dictionary<string, object> context = null)
        {
            if (archData is WavArchData arch)
            {
                return arch.ToWav();
            }

            if (archData is P16ArchData p16)
            {
                return p16.ToWav();
            }

            return null;
        }

        public bool ToArchData(AudioMetadata md, IArchData archData, in byte[] wave, string fileName, string waveExt, Dictionary<string, object> context = null)
        {
            if (archData is WavArchData data)
            {
                WavArchData arch = data;

                using var oms = new MemoryStream(wave);
                arch.ReadFromWav(oms);
                if (md != null && md.LoopStr != null)
                {
                    arch.Loop = PsbResHelper.ParseLoopStr(md.LoopStr.Value);
                }

                return true;
            }

            if (archData is P16ArchData p16)
            {
                P16ArchData arch = p16;

                using var oms = new MemoryStream(wave);
                arch.ReadFromWav(oms);

                return true;
                
            }

            return false;
        }

        public bool TryGetArchData(AudioMetadata md, PsbDictionary channel, out IArchData data, Dictionary<string, object> context = null)
        {
            data = null;
            if (md.Spec == PsbSpec.win)
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
            }

            if (channel.Count == 1 && channel["archData"] is PsbDictionary p16Arch && p16Arch["filetype"] is PsbString fileType && fileType == "p16" && p16Arch["data"] is PsbResource p16Data && p16Arch["samprate"] is PsbNumber sampRate)
            {
                var newData = new P16ArchData
                {
                    Data = p16Data,
                    SampleRate = sampRate.AsInt
                };
                data = newData;
                return true;
            }

            return false;
        }
    
    }
}
