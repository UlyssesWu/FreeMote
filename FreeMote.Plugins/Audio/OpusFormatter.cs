using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using FreeMote.Psb;
using VGAudio.Containers.Opus;
using VGAudio.Containers.Wave;

namespace FreeMote.Plugins.Audio
{
    //This is the worst design for sound_archive PSB
    //2 channels set in 1 archData? What's the meaning of "channelList"?
    [Export(typeof(IPsbAudioFormatter))]
    [ExportMetadata("Name", "FreeMote.NxOpus")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "NX Opus support via VGAudio.")]
    class OpusFormatter : IPsbAudioFormatter
    {
        public List<string> Extensions { get; } = new List<string> {".opus"};

        public bool CanToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            return archData is OpusArchData;
        }

        public bool CanToArchData(byte[] wave, Dictionary<string, object> context = null)
        {
            return wave != null;
        }

        public byte[] ToWave(AudioMetadata md, IArchData archData, string fileName = null, Dictionary<string, object> context = null)
        {
            NxOpusReader reader = new NxOpusReader();
            byte[] rawData = archData.Data?.Data;

            if (archData is OpusArchData data)
            {
                if (fileName == ".intro")
                {
                    rawData = data.Intro.Data.Data;
                }
                else if (fileName == ".body")
                {
                    rawData = data.Body.Data.Data;
                }
            }

            if (rawData == null)
            {
                return null;
            }

            var audioData = reader.Read(rawData);
            using MemoryStream oms = new MemoryStream();
            WaveWriter writer = new WaveWriter();
            writer.WriteToStream(audioData, oms, new WaveConfiguration {Codec = WaveCodec.Pcm16Bit}); //only 16Bit supported
            return oms.ToArray();
        }

        public bool ToArchData(AudioMetadata md, IArchData archData, in byte[] wave, string fileName, string waveExt, Dictionary<string, object> context = null)
        {
            if (archData is not OpusArchData data)
            {
                return false;
            }
            WaveReader reader = new WaveReader();
            var rawData = reader.Read(wave);
            using MemoryStream oms = new MemoryStream();
            NxOpusWriter writer = new NxOpusWriter();
            writer.WriteToStream(rawData, oms, new NxOpusConfiguration());

            ChannelClip clip = null;

            if (fileName == ".intro")
            {
                clip = data.Intro;

            }
            else if (fileName == ".body")
            {
                clip = data.Body;
            }
            else
            {
                return false;
            }

            if (clip.Data != null)
            {
                clip.Data.Data = oms.ToArray();
            }
            else
            {
                clip.Data = new PsbResource { Data = oms.ToArray() };
            }

            //OpusArchData archData = new OpusArchData {Data = new PsbResource {Data = oms.ToArray()}};
            var format = rawData.GetAllFormats().FirstOrDefault();
            if (format != null)
            {
                clip.SampleCount = format.SampleCount;
                data.SampRate = format.SampleRate;
                //data.ChannelCount = format.ChannelCount; //good, M2, now tell me where should I put this
            }

            return true;
        }

        public bool TryGetArchData(AudioMetadata md, PsbDictionary channel, out IArchData data, Dictionary<string, object> context = null)
        {
            data = null;
            //if (md.Spec != PsbSpec.nx)
            //{
            //    return false;
            //}
            if (channel.Count != 1 || channel["archData"] is not PsbDictionary archDic || !(archDic["ext"] is PsbString ext && ext.Value == Extensions[0]))
            {
                return false;
            }

            var opus = new OpusArchData()
            {
                Format = PsbAudioFormat.OPUS
                //ChannelPan = PsbAudioPan.IntroBody
            };

            bool hasBody = false, hasIntro = false;

            if (archDic["body"] is PsbDictionary body &&
                body["data"] is PsbResource bData && body["sampleCount"] is PsbNumber bSampleCount)
            {
                int skipSampleCount = 0;
                if (body["skipSampleCount"] is PsbNumber bSkipSampleCount)
                {
                    skipSampleCount = bSkipSampleCount.AsInt;
                }

                opus.Body = new ChannelClip {Data = bData, Name = md.Name + ".body", SampleCount = bSampleCount.AsInt, SkipSampleCount = skipSampleCount};
                hasBody = true;
            }

            if (archDic["intro"] is PsbDictionary intro &&
                intro["data"] is PsbResource iData && intro["sampleCount"] is PsbNumber iSampleCount)
            {
                int skipSampleCount = 0;
                if (intro["skipSampleCount"] is PsbNumber iSkipSampleCount)
                {
                    skipSampleCount = iSkipSampleCount.AsInt;
                }

                opus.Intro = new ChannelClip { Data = iData, Name = md.Name + ".body", SampleCount = iSampleCount.AsInt, SkipSampleCount = skipSampleCount };
                hasIntro = true;
            }

            //if (opus.Body != null && opus.Intro == null)
            //{
            //    opus.Data = opus.Body.Data;
            //}

            if (archDic["samprate"] is PsbNumber sampRate)
            {
                opus.SampRate = sampRate.AsInt;
            }

            data = opus;
            return true;
        }
    }
}