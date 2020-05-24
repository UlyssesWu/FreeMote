using System.IO;
using System.Text;

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

    /// <summary>
    /// XWMA
    /// </summary>
    public class XwmaArchData : IArchData
    {
        private PsbResource _fmt;
        private PsbResource _dpds;
        private PsbResource _data;

        public PsbResource Data
        {
            get => _data;
            set => _data = value;
        }

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

        public PsbResource Dpds
        {
            get => _dpds;
            set => _dpds = value;
        }

        public PsbResource Fmt
        {
            get => _fmt;
            set => _fmt = value;
        }

        public string Wav { get; set; }


        public uint Index => Data.Index ?? uint.MaxValue;
        public string Extension => Format.DefaultExtension();
        public string WaveExtension { get; set; } = ".wav";
        public PsbAudioFormat Format => PsbAudioFormat.XWMA;

        public byte[] ToXwma()
        {
            using MemoryStream ms =
                new MemoryStream(20 + Data.Data.Length + Dpds.Data.Length + Fmt.Data.Length + 6); //leave some space for padding
            using BinaryWriter writer = new BinaryWriter(ms);
            writer.WriteUTF8("RIFF");
            writer.Write(0);
            writer.WriteUTF8("XWMA");

            //writer.BaseStream.Position += writer.BaseStream.Position & 1;
            writer.WriteUTF8("fmt ");
            writer.Write(Fmt.Data.Length);
            writer.Write(Fmt.Data);

            //writer.BaseStream.Position += writer.BaseStream.Position & 1;
            writer.WriteUTF8("dpds");
            writer.Write(Dpds.Data.Length);
            writer.Write(Dpds.Data);

            //writer.BaseStream.Position += writer.BaseStream.Position & 1;
            writer.WriteUTF8("data");
            writer.Write(Data.Data.Length);
            writer.Write(Data.Data);

            var len = (uint) writer.BaseStream.Length;
            writer.Seek(4, SeekOrigin.Begin);
            writer.Write(len - 8);

            return ms.ToArray();
        }

        /// <summary>
        /// This will close the stream
        /// </summary>
        /// <param name="ms"></param>
        public void ReadFromXwma(Stream ms)
        {
            BinaryReader br = new BinaryReader(ms, Encoding.ASCII, false);
            var sig = new string(br.ReadChars(4));
            if (sig != "RIFF")
            {
                return;
            }

            var totalChunkSize = br.ReadUInt32();
            sig = new string(br.ReadChars(4));
            //if (sig != "XWMA" && sig != "xWMA")
            //{
            //    return;
            //}
            byte[] fmt = null, dpds = null, data = null;

            while (br.BaseStream.Position < br.BaseStream.Length && (fmt == null || dpds == null || data == null))
            {
                sig = new string(br.ReadChars(4));
                var chunkSize = br.ReadInt32();
                var chunk = br.ReadBytes(chunkSize);
                switch (sig)
                {
                    case "fmt ":
                        fmt = chunk;
                        break;
                    case "dpds":
                        dpds = chunk;
                        break;
                    case "data":
                        data = chunk;
                        break;
                    default:
                        continue;
                }
            }

            Apply(ref _data, data);
            Apply(ref _fmt, fmt);
            Apply(ref _dpds, dpds);

            static void Apply(ref PsbResource res, byte[] resData)
            {
                if (resData == null) return;
                if (res == null)
                {
                    res = new PsbResource {Data = resData};
                }
                else
                {
                    res.Data = resData;
                }
            }
        }
    }

    /// <summary>
    /// NX OPUS
    /// </summary>
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

    /// <summary>
    /// PS AT9
    /// </summary>
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