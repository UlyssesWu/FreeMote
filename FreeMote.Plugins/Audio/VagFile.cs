// HEVAG decoder, Originally by SilicaAndPina & daemon1, modified by UlyssesWu
// normal VAG (non-HE) is not supported for now
// References:
// https://github.com/vgmstream/vgmstream/blob/9e971af29239e79c387ea709ccebd9d72bb12ce9/src/meta/vag.c 
// https://bitbucket.org/SilicaAndPina/cxml-decompiler/raw/7a0e1a553bfdad369cf09957652b652ab1452d3f/AppInfoCli/VAG.cs

using System;
using System.IO;
using System.Linq;
using System.Text;

namespace FreeMote.Plugins.Audio
{
    public class VagFile
    {
        //In fact there are many variations with different signatures. If someday I have to support them but I get bored, I will make a wrapper for vgmstream
        private static readonly byte[] Signature = { (byte)'V', (byte)'A', (byte)'G', (byte)'p' }; 

        private static readonly int[,] HEVAGCoeffTable = {
            {      0,     0,     0,     0 },
            {   7680,     0,     0,     0 },
            {  14720, -6656,     0,     0 },
            {  12544, -7040,     0,     0 },
            {  15616, -7680,     0,     0 },
            {  14731, -7059,     0,     0 },
            {  14507, -7366,     0,     0 },
            {  13920, -7522,     0,     0 },
            {  13133, -7680,     0,     0 },
            {  12028, -7680,     0,     0 },
            {  10764, -7680,     0,     0 },
            {   9359, -7680,     0,     0 },
            {   7832, -7680,     0,     0 },
            {   6201, -7680,     0,     0 },
            {   4488, -7680,     0,     0 },
            {   2717, -7680,     0,     0 },
            {    910, -7680,     0,     0 },
            {   -910, -7680,     0,     0 },
            {  -2717, -7680,     0,     0 },
            {  -4488, -7680,     0,     0 },
            {  -6201, -7680,     0,     0 },
            {  -7832, -7680,     0,     0 },
            {  -9359, -7680,     0,     0 },
            { -10764, -7680,     0,     0 },
            { -12028, -7680,     0,     0 },
            { -13133, -7680,     0,     0 },
            { -13920, -7522,     0,     0 },
            { -14507, -7366,     0,     0 },
            { -14731, -7059,     0,     0 },
            {   5376, -9216,  3328, -3072 },
            {  -6400, -7168, -3328, -2304 },
            { -10496, -7424, -3584, -1024 },
            {   -167, -2722,  -494,  -541 },
            {  -7430, -2221, -2298,   424 },
            {  -8001, -3166, -2814,   289 },
            {   6018, -4750,  2649, -1298 },
            {   3798, -6946,  3875, -1216 },
            {  -8237, -2596, -2071,   227 },
            {   9199,  1982, -1382, -2316 },
            {  13021, -3044, -3792,  1267 },
            {  13112, -4487, -2250,  1665 },
            {  -1668, -3744, -6456,   840 },
            {   7819, -4328,  2111,  -506 },
            {   9571, -1336,  -757,   487 },
            {  10032, -2562,   300,   199 },
            {  -4745, -4122, -5486, -1493 },
            {  -5896,  2378, -4787, -6947 },
            {  -1193, -9117, -1237, -3114 },
            {   2783, -7108, -1575, -1447 },
            {  -7334, -2062, -2212,   446 },
            {   6127, -2577,  -315,   -18 },
            {   9457, -1858,   102,   258 },
            {   7876, -4483,  2126,  -538 },
            {  -7172, -1795, -2069,   482 },
            {  -7358, -2102, -2233,   440 },
            {  -9170, -3509, -2674,  -391 },
            {  -2638, -2647, -1929, -1637 },
            {   1873,  9183,  1860, -5746 },
            {   9214,  1859, -1124, -2427 },
            {  13204, -3012, -4139,  1370 },
            {  12437, -4792,  -256,   622 },
            {  -2653, -1144, -3182, -6878 },
            {   9331, -1048,  -828,   507 },
            {   1642,  -620,  -946, -4229 },
            {   4246, -7585,  -533, -2259 },
            {  -8988, -3891, -2807,    44 },
            {  -2562, -2735, -1730, -1899 },
            {   3182,  -483,  -714, -1421 },
            {   7937, -3844,  2821, -1019 },
            {  10069, -2609,   314,   195 },
            {   8400, -3297,  1551,  -155 },
            {  -8529, -2775, -2432,  -336 },
            {   9477, -1882,   108,   256 },
            {     75, -2241,  -298, -6937 },
            {  -9143, -4160, -2963,     5 },
            {  -7270, -1958, -2156,   460 },
            {  -2740,  3745,  5936, -1089 },
            {   8993,  1948,  -683, -2704 },
            {  13101, -2835, -3854,  1055 },
            {   9543, -1961,   130,   250 },
            {   5272, -4270,  3124, -3157 },
            {  -7696, -3383, -2907,  -456 },
            {   7309,  2523,   434, -2461 },
            {  10275, -2867,   391,   172 },
            {  10940, -3721,   665,    97 },
            {     24,  -310, -1262,   320 },
            {  -8122, -2411, -2311,  -271 },
            {  -8511, -3067, -2337,   163 },
            {    326, -3846,   419,  -933 },
            {   8895,  2194,  -541, -2880 },
            {  12073, -1876, -2017,  -601 },
            {   8729, -3423,  1674,  -169 },
            {  12950, -3847, -3007,  1946 },
            {  10038, -2570,   302,   198 },
            {   9385, -2757,  1008,    41 },
            {  -4720, -5006, -2852, -1161 },
            {   7869, -4326,  2135,  -501 },
            {   2450, -8597,  1299, -2780 },
            {  10192, -2763,   360,   181 },
            {  11313, -4213,   833,    53 },
            {  10154, -2716,   345,   185 },
            {   9638, -1417,  -737,   482 },
            {   3854, -4554,  2843, -3397 },
            {   6699, -5659,  2249, -1074 },
            {  11082, -3908,   728,    80 },
            {  -1026, -9810,  -805, -3462 },
            {  10396, -3746,  1367,   -96 },
            {  10287,   988, -1915, -1437 },
            {   7953,  3878,  -764, -3263 },
            {  12689, -3375, -3354,  2079 },
            {   6641,  3166,   231, -2089 },
            {  -2348, -7354, -1944, -4122 },
            {   9290, -4039,  1885,  -246 },
            {   4633, -6403,  1748, -1619 },
            {  11247, -4125,   802,    61 },
            {   9807, -2284,   219,   222 },
            {   9736, -1536,  -706,   473 },
            {   8440, -3436,  1562,  -176 },
            {   9307, -1021,  -835,   509 },
            {   1698, -9025,   688, -3037 },
            {  10214, -2791,   368,   179 },
            {   8390,  3248,  -758, -2989 },
            {   7201,  3316,    46, -2614 },
            {    -88, -7809,  -538, -4571 },
            {   6193, -5189,  2760, -1245 },
            {  12325, -1290, -3284,   253 },
            {  13064, -4075, -2824,  1877 },
            {   5333,  2999,   775, -1132 },
};

        public string FilePath { get; set; }
        public byte ChannelCount { get; set; }
        public uint WaveformDataSize { get; set; }
        public uint SampleRate { get; set; }
        public bool IsStereo => ChannelCount > 1;
        public string FileName { get; set; }
        public bool IsHEVAG { get; set; } = true;

        public byte[] PcmData { get; set; }

        /* version used to create the file:
         * - 00000000 = v1.8 PC,
         * - 00000002 = v1.3 Mac (used?)
         * - 00000003 = v1.6+ Mac
         * - 00000020 = v2.0 PC (most common)
         * - 00000004 = ? (later games)
         * - 00000006 = ? (vagconv)
         * - 00020001 = v2.1 (vagconv2)
         * - 00030000 = v3.0 (vagconv2) */
        public uint Version { get; set; }

        public VagFile(string path)
        {
            FilePath = path;
        }

        public VagFile(){}
        
        public bool LoadFromStreamLegacy(Stream stream)
        {
            using BinaryReader br = new BinaryReader(stream);
            if (!br.ReadBytes(4).SequenceEqual(Signature)) //0x00
            {
                //throw new Exception("Not a VAG file.");
                return false;
            }

            var fileSize = br.BaseStream.Length;

            Version = br.ReadUInt32BE(); //0x04
            br.ReadBytes(4); //0x08, reserved
            WaveformDataSize = br.ReadUInt32BE(); //0x0c
            SampleRate = br.ReadUInt32BE(); //0x10
            br.ReadBytes(10); //0x14-0x1D, reserved
            ChannelCount = br.ReadByte(); //0x1E, reserved
            br.ReadByte(); //0x1F, reserved
            FileName = Encoding.ASCII.GetString(br.ReadBytes(16)); //0x20-0x30, file name
            br.ReadBytes(16); //0x30-0x40, usually 0

            if (IsStereo)
            {
                Console.WriteLine("[WARN] Stereo VAG is not supported. Please provide the sample for research.");
            }

            // Get PCM data
            int Hist = 0;
            int Hist2 = 0;
            int Hist3 = 0;
            int Hist4 = 0;

            using MemoryStream PCMStream = new MemoryStream();
            using BinaryWriter PCMWriter = new BinaryWriter(PCMStream);

            while (br.BaseStream.Position < fileSize)
            {
                byte DecodingCoefficent = br.ReadByte();
                int ShiftBy = DecodingCoefficent & 0xf;
                int PredictNr = DecodingCoefficent >> 0x4;
                byte LoopData = br.ReadByte();
                PredictNr |= LoopData & 0xF0;
                int LoopFlag = LoopData & 0xf;
                if (LoopFlag == 0x7)
                {
                    br.BaseStream.Seek(14, SeekOrigin.Current);
                    Hist = 0;
                    Hist2 = 0;
                    Hist3 = 0;
                    Hist4 = 0;
                }
                else
                {

                    for (int i = 0; i < 14; i++)
                    {
                        byte ADPCMData = br.ReadByte();
                        int SampleFlags = ADPCMData & 0xF;
                        int Coefficent;
                        short Sample;

                        for (int ii = 0; ii <= 1; ii++)
                        {
                            if (SampleFlags > 7)
                            {
                                SampleFlags -= 16;
                            }

                            if (PredictNr < 128)
                            {
                                Coefficent = Hist * HEVAGCoeffTable[PredictNr, 0] + Hist2 * HEVAGCoeffTable[PredictNr, 1] + Hist3 * HEVAGCoeffTable[PredictNr, 2] + Hist4 * HEVAGCoeffTable[PredictNr, 3];
                            }
                            else
                            {
                                Coefficent = 0;
                            }

                            Sample = (short)(Coefficent / 32 + (SampleFlags << 20 - ShiftBy) + 128 >> 8);

                            PCMWriter.Write(Sample);


                            Hist4 = Hist3;
                            Hist3 = Hist2;
                            Hist2 = Hist;
                            Hist = Sample;

                            SampleFlags = ADPCMData >> 4;
                        }
                    }
                }

                /* TODO:
                 * Arg im mad because i know how to get left/right channels
                 * But i have no idea how to combine them
                 * So lets just get one and call it a day.
                 */

                if (IsStereo)
                {
                    br.BaseStream.Seek(16, SeekOrigin.Current);
                }
            }


            PCMStream.Seek(0x00, SeekOrigin.Begin);
            PcmData = PCMStream.ToArray();
            return true;
        }

        public bool LoadFromStream(Stream stream)
        {
            using BinaryReader br = new BinaryReader(stream);
            if (!br.ReadBytes(4).SequenceEqual(Signature)) //0x00
            {
                //throw new Exception("Not a VAG file.");
                return false;
            }

            var fileSize = br.BaseStream.Length;

            Version = br.ReadUInt32BE(); //0x04
            br.ReadBytes(4); //0x08, reserved
            WaveformDataSize = br.ReadUInt32BE(); //0x0c
            SampleRate = br.ReadUInt32BE(); //0x10
            br.ReadBytes(10); //0x14-0x1D, reserved
            ChannelCount = br.ReadByte(); //0x1E, reserved
            br.ReadByte(); //0x1F, reserved
            FileName = Encoding.ASCII.GetString(br.ReadBytes(16)); //0x20-0x30, file name
            //br.ReadBytes(16); //0x30-0x40, usually 0

            if (IsStereo)
            {
                Console.WriteLine("[WARN] Stereo VAG is not supported. Please provide the sample for research.");
            }

            // Get PCM data
            int Hist = 0;
            int Hist2 = 0;
            int Hist3 = 0;
            int Hist4 = 0;

            /* external interleave (fixed size), mono */
            var bytesPerFrame = 0x10;
            var samplesPerFrame = (bytesPerFrame - 0x02) * 2; /* always 28 */
            
            using MemoryStream PCMStream = new MemoryStream();
            using BinaryWriter PCMWriter = new BinaryWriter(PCMStream);

            while (br.BaseStream.Position < fileSize)
            {
                byte decodingCoefficent = br.ReadByte();
                int shiftFactor = decodingCoefficent & 0xf;
                int coefIndex = (decodingCoefficent >> 0x4) & 0xf;
                byte loopData = br.ReadByte();
                coefIndex |= loopData & 0xF0;
                int loopFlag = loopData & 0xf;

                if (coefIndex > 127)
                {
                    coefIndex = 127;
                }

                if (shiftFactor > 12)
                {
                    shiftFactor = 9;
                }

                shiftFactor = 20 - shiftFactor;

                if (loopFlag == 0x7)
                {
                    br.BaseStream.Seek(14, SeekOrigin.Current);
                    for (int i = 0; i < 28; i++)
                    {
                        PCMWriter.Write((short)0);
                    }
                    //Hist = 0;
                    //Hist2 = 0;
                    //Hist3 = 0;
                    //Hist4 = 0;
                }
                else
                {
                    for (int i = 0; i < 14; i++)
                    {
                        byte adpcmData = br.ReadByte();

                        for (int j = 0; j < 2; j++)
                        {
                            var coefficent = Hist * HEVAGCoeffTable[coefIndex, 0] + Hist2 * HEVAGCoeffTable[coefIndex, 1] + Hist3 * HEVAGCoeffTable[coefIndex, 2] + Hist4 * HEVAGCoeffTable[coefIndex, 3];
                            var sample = ((j & 1) != 0 ? GetHighNibbleSigned(adpcmData) : GetLowNibbleSigned(adpcmData)) << shiftFactor;
                            sample = (coefficent >> 5) + sample;
                            sample >>= 8;

                            PCMWriter.Write(Clamp16(sample));

                            Hist4 = Hist3;
                            Hist3 = Hist2;
                            Hist2 = Hist;
                            Hist = sample;
                        }
                    }
                }

                /* TODO:
                 * Arg im mad because i know how to get left/right channels
                 * But i have no idea how to combine them
                 * So lets just get one and call it a day.
                 */

                if (IsStereo)
                {
                    br.BaseStream.Seek(16, SeekOrigin.Current);
                }
            }


            PCMStream.Seek(0x00, SeekOrigin.Begin);
            PcmData = PCMStream.ToArray();
            return true;
        }

        /* signed nibbles come up a lot */
        static int[] _nibbleToInt = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, -8, -7, -6, -5, -4, -3, -2, -1 };

        static int GetHighNibbleSigned(byte n)
        { 
            /*return ((n&0x70)-(n&0x80))>>4;*/
            return _nibbleToInt[n >> 4];
        }
        
        static int GetLowNibbleSigned(byte n)
        {
            /*return (n&7)-(n&8);*/
            return _nibbleToInt[n & 0xf];
        }

        static short Clamp16(int val)
        {
            if (val > 32767) return 32767;
            if (val < -32768) return -32768;
            else return (short)val;
        }

        public bool Load()
        {
            if (!File.Exists(FilePath))
            {
                return false;
            }

            return LoadFromStream(File.OpenRead(FilePath));
        }

        public void WriteToWavFile(string path)
        {
            var stream = ToWave();
            if (stream == null)
            {
                return;
            }

            File.WriteAllBytes(path, stream.ToArray());
        }

        public MemoryStream ToWave()
        {
            if (PcmData == null)
            {
                return null;
            }

            MemoryStream ms = new MemoryStream();
            int fileSize = PcmData.Length;
            BinaryWriter bw = new BinaryWriter(ms);

            bw.WriteUTF8("RIFF");
            bw.Write(fileSize + 36);

            bw.WriteUTF8("WAVE");
            bw.WriteUTF8("fmt ");
            bw.Write(16); //SubChunk Size, usually 0x12 ?

            bw.Write((short)1); //Audio Format, 1=PCM loss-less
            var channelCount = ChannelCount > 1 ? 2 : 1; 
            bw.Write((short)channelCount); //Channel Count
            bw.Write(SampleRate); //Sample Rate
            bw.Write((uint) ((SampleRate * 16 * channelCount) / 8)); //Byte Rate, 4 bytes, must convert
            bw.Write((short)(16 * channelCount / 8)); //BlockAlign, ref: https://blog.csdn.net/xcgspring/article/details/4671221
            bw.Write((short)16); //Bits Per Sample, can be 8,16,32

            bw.WriteUTF8("data");
            bw.Write(fileSize);
            ms.Write(PcmData, 0x00, fileSize);

            ms.Seek(0x00, SeekOrigin.Begin);
            return ms;
        }
    }
}
