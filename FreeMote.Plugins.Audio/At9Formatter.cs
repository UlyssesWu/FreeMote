using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using FreeMote.Psb;
using VGAudio.Containers.At9;
using VGAudio.Containers.Wave;

namespace FreeMote.Plugins.Audio
{
    [Export(typeof(IPsbAudioFormatter))]
    [ExportMetadata("Name", "FreeMote.At9")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "At9 support via VGAudio.")]
    public class At9Formatter : IPsbAudioFormatter
    {
        public List<string> Extensions { get; } = new List<string> {".at9"};

        public bool CanToArchData(byte[] wave, Dictionary<string, object> context = null)
        {
            return false;
        }

        public bool CanToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            if (archData is Atrac9ArchData)
            {
                return true;
            }

            return false;
        }

        public IArchData ToArchData(byte[] wave, string waveExt, Dictionary<string, object> context = null)
        {
            throw new NotSupportedException("AT9 encode is not supported. Use at9tool manually.");
            return null;
        }

        public bool TryGetArchData(PSB psb, PsbDictionary dic, out IArchData data)
        {
            data = null;
            if (psb.Platform == PsbSpec.ps4 || psb.Platform == PsbSpec.vita)
            {
                if (dic.Count == 1 && dic["archData"] is PsbResource res)
                {
                    data = new Atrac9ArchData
                    {
                        Data = res
                    };

                    return true;
                }

                return false;
            }

            return false;
        }

        public byte[] ToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            var at9Arch = (Atrac9ArchData) archData;
            At9Reader reader = new At9Reader();
            //var format = reader.ReadFormat();
            using MemoryStream ms = new MemoryStream(at9Arch.Data.Data);
            using MemoryStream oms = new MemoryStream();
            var data = reader.Read(ms);
            WaveWriter writer = new WaveWriter();
            writer.WriteToStream(data, oms, new WaveConfiguration {Codec = WaveCodec.Pcm16Bit});

            return oms.ToArray();
        }
    }
}