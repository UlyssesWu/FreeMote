using System.IO;

namespace FreeMote.Psb
{
    public class WavArchData : IArchData
    {
        public uint Index => Data.Index ?? uint.MaxValue;
        public string Extension => ".wav";
        public string WaveExtension { get; set; } = ".wav";
        public PsbAudioFormat Format => PsbAudioFormat.WAV;

        public PsbResource Data { get; set; }
        public PsbDictionary PsbArchData { get; set; }

        public IPsbValue ToPsbArchData()
        {
            return Data;
        }
    }

    public class XwmaArchData : IArchData
    {
        public PsbResource Data { get; set; }
        public PsbDictionary PsbArchData { get; set; }

        public IPsbValue ToPsbArchData()
        {
            return new PsbDictionary
            {
                {"data", Data},
                {"dpds", Dpds},
                {"fmt", Fmt},
                {"wav", Wav.ToPsbString()}
            };
        }

        public PsbResource Dpds { get; set; }
        public PsbResource Fmt { get; set; }

        public string Wav { get; set; }


        public uint Index => Data.Index ?? uint.MaxValue;
        public string Extension => Format.DefaultExtension();
        public string WaveExtension { get; set; } = ".wav";
        public PsbAudioFormat Format => PsbAudioFormat.XWMA;

        public byte[] ToXWMA()
        {
            using MemoryStream ms =
                new MemoryStream(20 + Data.Data.Length + Dpds.Data.Length + Fmt.Data.Length + 6);
            using BinaryWriter writer = new BinaryWriter(ms);
            writer.WriteUTF8("RIFF");
            writer.Write(0);
            writer.WriteUTF8("XWMA");

            writer.BaseStream.Position += writer.BaseStream.Position & 1;
            writer.WriteUTF8("fmt ");
            writer.Write(Fmt.Data.Length);
            writer.Write(Fmt.Data);

            writer.BaseStream.Position += writer.BaseStream.Position & 1;
            writer.WriteUTF8("dpds");
            writer.Write(Dpds.Data.Length);
            writer.Write(Dpds.Data);

            writer.BaseStream.Position += writer.BaseStream.Position & 1;
            writer.WriteUTF8("data");
            writer.Write(Data.Data.Length);
            writer.Write(Data.Data);

            var len = (uint)writer.BaseStream.Length;
            writer.Seek(4, SeekOrigin.Begin);
            writer.Write(len - 8);

            return ms.ToArray();
        }
    }

    public class OpusArchData : IArchData
    {
        public PsbResource Data { get; set; }
        public PsbDictionary PsbArchData { get; set; }

        public IPsbValue ToPsbArchData()
        {
            var archData = new PsbDictionary
            {
                {"channelCount", ChannelCount.ToPsbNumber()},
                {"ext", Extension.ToPsbString()},
                {"samprate", SampRate.ToPsbNumber()}
            };
            var body = new PsbDictionary
            {
                {"data", Data},
                {"sampleCount", SampleCount.ToPsbNumber()}
            };
            body.Parent = archData;
            archData.Add("body", body);
            return archData;
        }

        /// <summary>
        /// (Set after link)
        /// </summary>
        public int SampleCount { get; set; }

        public int ChannelCount { get; set; } = 1;
        public int SampRate { get; set; } = 48000;

        public uint Index => Data.Index ?? uint.MaxValue;
        public string Extension => Format.DefaultExtension();
        public string WaveExtension { get; set; } = ".wav";
        public PsbAudioFormat Format => PsbAudioFormat.OPUS;
    }

    public class Atrac9ArchData : IArchData
    {
        public PsbResource Data { get; set; }
        public PsbDictionary PsbArchData { get; set; }

        public IPsbValue ToPsbArchData()
        {
            return Data;
        }

        public uint Index => Data.Index ?? uint.MaxValue;
        public string Extension => Format.DefaultExtension();
        public string WaveExtension { get; set; } = ".wav";
        public PsbAudioFormat Format => PsbAudioFormat.Atrac9;
    }
}
