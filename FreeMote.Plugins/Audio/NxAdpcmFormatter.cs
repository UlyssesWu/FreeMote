using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using FreeMote.Psb;
using VGAudio.Containers.Dsp;
using VGAudio.Containers.Wave;
using VGAudio.Utilities;

//REF: https://www.metroid2002.com/retromodding/wiki/DSP_(File_Format)

namespace FreeMote.Plugins.Audio
{
    [Export(typeof(IPsbAudioFormatter))]
    [ExportMetadata("Name", "FreeMote.NxAdpcm")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "NX ADPCM support via VGAudio.")]
    class NxAdpcmFormatter : IPsbAudioFormatter
    {
        public List<string> Extensions { get; } = new List<string> { ".adpcm" };
        public bool CanToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            return archData is AdpcmArchData;
        }

        public bool CanToArchData(byte[] wave, Dictionary<string, object> context = null)
        {
            return wave != null;
        }

        public byte[] ToWave(AudioMetadata md, IArchData archData, string fileName = null, Dictionary<string, object> context = null)
        {
            DspReader reader = new DspReader();
            var data = reader.Read(archData.Data.Data);
            using MemoryStream oms = new MemoryStream();
            WaveWriter writer = new WaveWriter();
            writer.WriteToStream(data, oms, new WaveConfiguration { Codec = WaveCodec.Pcm16Bit }); //only 16Bit supported
            return oms.ToArray();
        }

        public bool ToArchData(AudioMetadata md, IArchData archData, in byte[] wave, string fileName, string waveExt, Dictionary<string, object> context = null)
        {
            if (archData is not AdpcmArchData data)
            {
                return false;
            }
            WaveReader reader = new WaveReader();
            var rawData = reader.Read(wave);
            using MemoryStream oms = new MemoryStream();
            DspWriter writer = new DspWriter();
            writer.WriteToStream(rawData, oms, new DspConfiguration {Endianness = Endianness.LittleEndian});
            data.Data = new PsbResource {Data = oms.ToArray()};
            var format = rawData.GetAllFormats().FirstOrDefault();
            if (format != null)
            {
                data.SampRate = format.SampleRate;
            }

            return true;
        }

        public bool TryGetArchData(AudioMetadata md, PsbDictionary channel, out IArchData data, Dictionary<string, object> context = null)
        {
            data = null;
            //if (psb.Platform != PsbSpec.nx)
            //{
            //    return false;
            //}
            if (!(channel["archData"] is PsbDictionary archDic))
            {
                return false;
            }

            if (!archDic.ContainsKey("body") &&
                archDic["data"] is PsbResource aData
                && archDic["ext"] is PsbString ext && ext.Value == Extensions[0] &&
                archDic["samprate"] is PsbNumber sampRate)
            {
                var newData = new AdpcmArchData
                {
                    Data = aData,
                    SampRate = sampRate.AsInt,
                    Format = PsbAudioFormat.ADPCM
                };

                if (channel["pan"] is PsbList panList)
                {
                    newData.Pan = panList;

                    if (panList.Count == 2)
                    {
                        var left = panList[0].GetFloat();
                        var right = panList[1].GetFloat();
                        if (left == 1.0f && right == 0.0f)
                        {
                            newData.ChannelPan = PsbAudioPan.Left;
                        }
                        else if (left == 0.0f && right == 1.0f)
                        {
                            newData.ChannelPan = PsbAudioPan.Right;
                        }
                    }
                }

                data = newData;
                return true;
            }

            return false;
        }
    }
}
