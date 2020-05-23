using System.Collections.Generic;
using System.ComponentModel.Composition;
using FreeMote.Psb;

namespace FreeMote.Plugins.Audio
{
    [Export(typeof(IPsbAudioFormatter))]
    [ExportMetadata("Name", "FreeMote.Opus")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "Opus support via VGAudio.")]
    class OpusFormatter : IPsbAudioFormatter
    {
        public List<string> Extensions { get; } = new List<string>{ ".opus" };
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
            return null;
        }

        public IArchData ToArchData(byte[] wave, string waveExt, Dictionary<string, object> context = null)
        {
            return null;
        }

        public bool TryGetArchData(PSB psb, PsbDictionary dic, out IArchData data)
        {
            data = null;
            if (dic.Count == 1 && dic["archData"] is PsbDictionary archDic && archDic["data"] is PsbResource aData && dic["ext"] is PsbString ext && ext.Value == Extensions[0] && archDic["sampleCount"] is PsbNumber sampleCount && dic["samprate"] is PsbNumber sampRate)
            {
                data = new OpusArchData
                {
                    Data = aData,
                    SampRate = sampRate.IntValue,
                    SampleCount = sampleCount.IntValue
                };

                return true;
            }

            return false;
        }
    }
}
