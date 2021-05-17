using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FreeMote.Psb
{
    public class WavArchData : IArchData
    {
        public uint Index => Data.Index ?? uint.MaxValue;
        public string Extension => ".wav";
        public string WaveExtension { get; set; } = ".wav";
        public PsbAudioFormat Format => PsbAudioFormat.WAV;
        public PsbAudioPan ChannelPan => PsbAudioPan.Mono;


        private PsbResource _fmt;
        private PsbResource _data;

        public PsbResource Data
        {
            get => _data;
            set => _data = value;
        }

        public IList<PsbResource> DataList => new List<PsbResource> { _data };

        public PsbResource Fmt
        {
            get => _fmt;
            set => _fmt = value;
        }

        public string Wav { get; set; }
        public PsbList Loop { get; set; }

        public PsbDictionary PsbArchData { get; set; }

        public IPsbValue ToPsbArchData()
        {
            return new PsbDictionary
            {
                {"data", Data},
                {"fmt", Fmt},
                {"loop", Loop},
                {"wav", Wav.ToPsbString()}
            };
        }

        public byte[] ToWav()
        {
            using MemoryStream ms = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(ms);
            writer.WriteUTF8("RIFF");
            writer.Write(0);

            writer.WriteUTF8("WAVE");

            writer.WriteUTF8("fmt ");
            writer.Write(Fmt.Data.Length);
            writer.Write(Fmt.Data);

            writer.WriteUTF8("data");
            writer.Write(Data.Data.Length);
            writer.Write(Data.Data);

            //Write length at position 4
            var len = (uint)writer.BaseStream.Length;
            writer.Seek(4, SeekOrigin.Begin);
            writer.Write(len - 8);

            return ms.ToArray();
        }

        /// <summary>
        /// This will close the stream
        /// </summary>
        /// <param name="ms"></param>
        public void ReadFromWav(Stream ms)
        {
            using BinaryReader br = new BinaryReader(ms, Encoding.ASCII, false);

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
            byte[] fmt = null, data = null;

            while (br.BaseStream.Position < br.BaseStream.Length && (fmt == null || data == null))
            {
                sig = new string(br.ReadChars(4));
                var chunkSize = br.ReadInt32();
                var chunk = br.ReadBytes(chunkSize);
                switch (sig)
                {
                    case "fmt ":
                        fmt = chunk;
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
        }


        private static void Apply(ref PsbResource res, byte[] resData)
        {
            if (resData == null) return;
            if (res == null)
            {
                res = new PsbResource { Data = resData };
            }
            else
            {
                res.Data = resData;
            }
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
        public IList<PsbResource> DataList => new List<PsbResource> { _data };


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
        public PsbAudioPan ChannelPan => PsbAudioPan.Mono;

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

            var len = (uint)writer.BaseStream.Length;
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
            using BinaryReader br = new BinaryReader(ms, Encoding.ASCII, false);
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
        }

        private static void Apply(ref PsbResource res, byte[] resData)
        {
            if (resData == null) return;
            if (res == null)
            {
                res = new PsbResource { Data = resData };
            }
            else
            {
                res.Data = resData;
            }
        }
    }

    /// <summary>
    /// Clip (parts of a channel) used in NX OPUS
    /// </summary>
    internal class ChannelClip
    {
        public string Name { get; set; }
        public PsbResource Data { get; set; }
        public int SampleCount { get; set; } = 0;
        public int SkipSampleCount { get; set; } = 0;

        public PsbDictionary ToPsbArchData()
        {
            var body = new PsbDictionary
            {
                {"data", Data},
                {"sampleCount", SampleCount.ToPsbNumber()}
            };

            if (SkipSampleCount != 0)
            {
                body["skipSampleCount"] = SkipSampleCount.ToPsbNumber();
            }

            return body;
        }
    }

    /// <summary>
    /// NX ADPCM
    /// </summary>
    public class AdpcmArchData : IArchData
    {
        public PsbResource Data { get; set; }

        public IList<PsbResource> DataList => new List<PsbResource> { Data };


        public PsbDictionary PsbArchData { get; set; }

        public IPsbValue ToPsbArchData()
        {
            var archData = new PsbDictionary
            {
                {"data", Data},
                {"ext", Extension.ToPsbString()},
                {"samprate", SampRate.ToPsbNumber()}
            };

            return archData;
        }

        public int SampRate { get; set; } = 48000;

        public uint Index => Data.Index ?? uint.MaxValue;
        public string Extension => Format.DefaultExtension();
        public string WaveExtension { get; set; } = ".wav";
        public PsbAudioFormat Format { get; set; } = PsbAudioFormat.ADPCM;

        public PsbAudioPan ChannelPan { get; set; } = PsbAudioPan.IntroBody;

        public bool Loop { get; set; } = false;
        public PsbList Pan { get; set; }
    }

    /// <summary>
    /// NX OPUS (one channel may have 2 resource)
    /// </summary>
    public class OpusArchData : IArchData
    {
        internal ChannelClip Body { get; set; }
        internal ChannelClip Intro { get; set; }

        public PsbResource Data { get; set; }

        public IList<PsbResource> DataList => new List<PsbResource> { Body?.Data, Intro?.Data };


        public PsbDictionary PsbArchData { get; set; }

        public IPsbValue ToPsbArchData()
        {
            var archData = new PsbDictionary
            {
                {"channelCount", ChannelCount.ToPsbNumber()},
                {"ext", Extension.ToPsbString()},
                {"samprate", SampRate.ToPsbNumber()}
            };

            if (Format == PsbAudioFormat.OPUS)
            {
                if (Body != null)
                {
                    var dict = Body.ToPsbArchData();
                    dict.Parent = archData;
                    archData.Add("body", dict);
                }

                if (Intro != null)
                {
                    var dict = Intro.ToPsbArchData();
                    dict.Parent = archData;
                    archData.Add("intro", dict);
                }
            }

            return archData;
        }

        //public int ChannelCount { get; set; } = 1; //WTF M2? You put ChannelCount in a Channel??
        public int ChannelCount //WTF M2? You put ChannelCount in a Channel??
        {
            get
            {
                if (Body != null && Intro != null)
                {
                    return 2;
                }

                if (Body == null && Intro == null)
                {
                    return 0;
                }
                
                return 1;
            }
        } 

        public int SampRate { get; set; } = 48000;

        public int BodySkipSampleCount { get; set; } = 0;
        public int IntroSkipSampleCount { get; set; } = 0;

        public uint Index => Data.Index ?? uint.MaxValue;
        public string Extension => Format.DefaultExtension();
        public string WaveExtension { get; set; } = ".wav";
        public PsbAudioFormat Format { get; set; } = PsbAudioFormat.OPUS;

        //public PsbAudioPan ChannelPan { get; set; } = PsbAudioPan.IntroBody;
        public PsbAudioPan ChannelPan
        {
            get
            {
                if (Body != null && Intro != null)
                {
                    return PsbAudioPan.IntroBody;
                }

                if (Body == null && Intro == null)
                {
                    return PsbAudioPan.IntroBody;
                }

                return Body == null ? PsbAudioPan.Intro : PsbAudioPan.Body;
            }
        }

        public bool Loop { get; set; } = false;
    }

    ///// <summary>
    ///// PS AT9
    ///// </summary>
    //public class Atrac9ArchData : IArchData
    //{
    //    public PsbResource Data { get; set; }
    //    public PsbDictionary PsbArchData { get; set; }

    //    public IPsbValue ToPsbArchData()
    //    {
    //        return Data;
    //    }

    //    public uint Index => Data.Index ?? uint.MaxValue;
    //    public string Extension => Format.DefaultExtension();
    //    public string WaveExtension { get; set; } = ".wav";
    //    public PsbAudioFormat Format => PsbAudioFormat.Atrac9;
    //}

    ///// <summary>
    ///// PS VAG
    ///// </summary>
    //public class VagArchData : IArchData
    //{
    //    public PsbResource Data { get; set; }
    //    public PsbDictionary PsbArchData { get; set; }

    //    public IPsbValue ToPsbArchData()
    //    {
    //        return Data;
    //    }

    //    public uint Index => Data.Index ?? uint.MaxValue;
    //    public string Extension => Format.DefaultExtension();
    //    public string WaveExtension { get; set; } = ".wav";
    //    public PsbAudioFormat Format => PsbAudioFormat.VAG;
    //}

    /// <summary>
    /// PS Base (VAG / AT9)
    /// </summary>
    public class PsArchData : IArchData
    {
        public PsbResource Data { get; set; }
        public IList<PsbResource> DataList => new List<PsbResource> { Data };
        public PsbDictionary PsbArchData { get; set; }

        public IPsbValue ToPsbArchData()
        {
            return Data;
        }

        public uint Index => Data.Index ?? uint.MaxValue;
        public string Extension => Format.DefaultExtension();
        public string WaveExtension { get; set; } = ".wav";
        public PsbAudioFormat Format { get; set; } = PsbAudioFormat.Unknown;
        public PsbAudioPan ChannelPan => PsbAudioPan.Mono;
    }
}