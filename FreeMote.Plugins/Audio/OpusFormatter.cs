using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using FreeMote.Psb;
using VGAudio.Containers.Opus;
using VGAudio.Containers.Wave;

namespace FreeMote.Plugins.Audio
{
    [Export(typeof(IPsbAudioFormatter))]
    [ExportMetadata("Name", "FreeMote.NxOpus")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "NX Opus support via VGAudio.")]
    class OpusFormatter : IPsbAudioFormatter
    {
        public List<string> Extensions { get; } = new List<string> {".opus"};

        public bool CanToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            return archData is NxArchData;
        }

        public bool CanToArchData(byte[] wave, Dictionary<string, object> context = null)
        {
            return wave != null;
        }

        public byte[] ToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            NxOpusReader reader = new NxOpusReader();
            var data = reader.Read(archData.Data.Data);
            using MemoryStream oms = new MemoryStream();
            WaveWriter writer = new WaveWriter();
            writer.WriteToStream(data, oms, new WaveConfiguration {Codec = WaveCodec.Pcm16Bit}); //only 16Bit supported
            return oms.ToArray();
        }

        public IArchData ToArchData(AudioMetadata md, in byte[] wave, string fileName, string waveExt, Dictionary<string, object> context = null)
        {
            WaveReader reader = new WaveReader();
            var data = reader.Read(wave);
            using MemoryStream oms = new MemoryStream();
            NxOpusWriter writer = new NxOpusWriter();
            writer.WriteToStream(data, oms, new NxOpusConfiguration());
            NxArchData archData = new NxArchData {Data = new PsbResource {Data = oms.ToArray()}};
            var format = data.GetAllFormats().FirstOrDefault();
            if (format != null)
            {
                archData.SampleCount = format.SampleCount;
                archData.SampRate = format.SampleRate;
                archData.ChannelCount = format.ChannelCount;
            }

            return archData;
        }

        public bool TryGetArchData(PSB psb, PsbDictionary channel, out IArchData data, Dictionary<string, object> context = null)
        {
            data = null;
            //if (psb.Platform != PsbSpec.nx)
            //{
            //    return false;
            //}
            if (channel.Count != 1 || !(channel["archData"] is PsbDictionary archDic))
            {
                return false;
            }

            if (archDic["body"] is PsbDictionary body &&
                body["data"] is PsbResource aData && body["sampleCount"] is PsbNumber sampleCount
                && archDic["ext"] is PsbString ext && ext.Value == Extensions[0] &&
                archDic["samprate"] is PsbNumber sampRate)
            {
                data = new NxArchData
                {
                    Data = aData,
                    SampRate = sampRate.AsInt,
                    SampleCount = sampleCount.AsInt,
                    Format = PsbAudioFormat.OPUS
                };

                return true;
            }

            return false;
        }
    }
}