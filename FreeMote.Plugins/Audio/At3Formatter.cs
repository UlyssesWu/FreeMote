using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using FreeMote.Psb;
using LightCodec;

namespace FreeMote.Plugins.Audio
{
    [Export(typeof(IPsbAudioFormatter))]
    [ExportMetadata("Name", "FreeMote.At3")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "PS3 ATRAC3/ATRAC3plus support via LightCodec and at3tool.")]
    public class At3Formatter : IPsbAudioFormatter
    {
        public const string At3BitRate = "At3BitRate";

        private const string ConverterTool = "at3tool.exe";
        private const int LightCodecAtrac3PlusDelay = 368;
        private static readonly byte[] Atrac3PlusSubFormat =
        {
            0xBF, 0xAA, 0x23, 0xE9, 0x58, 0xCB, 0x71, 0x44,
            0xA1, 0x19, 0xFF, 0xFA, 0x01, 0xE4, 0xCE, 0x62
        };

        public List<string> Extensions { get; } = new List<string> { ".at3" };

        public string ToolPath { get; set; }

        public At3Formatter()
        {
            var toolPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? "", "Tools",
                ConverterTool);
            if (File.Exists(toolPath))
            {
                ToolPath = toolPath;
            }
        }

        public bool CanToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            return archData is PsArchData psArchData && IsAtrac3Wave(psArchData.Data?.Data);
        }

        public bool CanToArchData(byte[] wave, Dictionary<string, object> context = null)
        {
            if (IsAtrac3Wave(wave))
            {
                return true;
            }

            if (!File.Exists(ToolPath))
            {
                Logger.LogWarn($"[WARN] External tool missing: {ConverterTool}");
                return false;
            }

            return IsRiffWave(wave);
        }

        public byte[] ToWave(AudioMetadata md, IArchData archData, string fileName = null,
            Dictionary<string, object> context = null)
        {
            if (archData is not PsArchData psArchData || !IsAtrac3Wave(psArchData.Data?.Data))
            {
                return null;
            }

            psArchData.WaveExtension = ".wav";
            var result = DecodeWithLightCodec(psArchData.Data.Data);
            if (result != null)
            {
                return result;
            }

            Logger.LogWarn($"[WARN] LightCodec ATRAC3 decode failed for {fileName ?? "<unknown>"}; trying at3tool.");
            if (!File.Exists(ToolPath))
            {
                Logger.LogWarn($"[WARN] External tool missing: {ConverterTool}. Output original ATRAC3 data.");
                return psArchData.Data.Data;
            }

            result = RunTool("-d -repeat 1", psArchData.Data.Data);
            if (result != null)
            {
                return result;
            }

            Logger.LogWarn("[WARN] ATRAC3 decode failed. Output original ATRAC3 data.");
            return psArchData.Data.Data;
        }

        public bool ToArchData(AudioMetadata md, IArchData archData, in byte[] wave, string fileName,
            string waveExt, Dictionary<string, object> context = null)
        {
            if (archData is not PsArchData psArchData)
            {
                return false;
            }

            if (IsAtrac3Wave(wave))
            {
                SetData(psArchData, wave);
                return true;
            }

            if (!File.Exists(ToolPath) || !IsRiffWave(wave))
            {
                return false;
            }

            var bitRate = 128;
            if (context != null)
            {
                if (context.TryGetValue(At3BitRate, out var value) && value is int configuredBitRate)
                {
                    bitRate = configuredBitRate;
                }
                else
                {
                    context[At3BitRate] = bitRate;
                }
            }

            var options = $"-e -br {bitRate}";
            if (TryGetLoopPoints(md, out var loopStart, out var loopEnd))
            {
                options += $" -loop {loopStart} {loopEnd}";
            }

            var result = RunTool(options, wave);
            if (result == null || !IsAtrac3Wave(result))
            {
                Logger.LogWarn("[WARN] ATRAC3 encode failed.");
                return false;
            }

            SetData(psArchData, result);
            return true;
        }

        public bool TryGetArchData(AudioMetadata md, PsbDictionary channel, out IArchData data,
            Dictionary<string, object> context = null)
        {
            data = null;
            if (md.Spec != PsbSpec.ps3 || channel.Count != 1 ||
                channel["archData"] is not PsbResource resource)
            {
                return false;
            }

            if (resource.Data != null && resource.Data.Length > 0 && !IsAtrac3Wave(resource.Data))
            {
                return false;
            }

            data = new PsArchData
            {
                Data = resource,
                Format = PsbAudioFormat.Atrac3Plus
            };
            return true;
        }

        internal static bool IsAtrac3Wave(byte[] data)
        {
            if (!TryGetWaveFormat(data, out var formatTag, out var subFormatOffset))
            {
                return false;
            }

            if (formatTag == 0x0270) // ATRAC3
            {
                return true;
            }

            if (formatTag != 0xFFFE || subFormatOffset < 0 ||
                subFormatOffset + Atrac3PlusSubFormat.Length > data.Length)
            {
                return false;
            }

            for (var i = 0; i < Atrac3PlusSubFormat.Length; i++)
            {
                if (data[subFormatOffset + i] != Atrac3PlusSubFormat[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsRiffWave(byte[] data)
        {
            return data != null && data.Length >= 12 &&
                   data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' &&
                   data[8] == 'W' && data[9] == 'A' && data[10] == 'V' && data[11] == 'E';
        }

        private static bool TryGetWaveFormat(byte[] data, out ushort formatTag, out int subFormatOffset)
        {
            formatTag = 0;
            subFormatOffset = -1;
            if (!IsRiffWave(data))
            {
                return false;
            }

            var position = 12;
            while (position + 8 <= data.Length)
            {
                var chunkSize = BitConverter.ToInt32(data, position + 4);
                if (chunkSize < 0)
                {
                    return false;
                }

                var chunkDataOffset = position + 8;
                if (data[position] == 'f' && data[position + 1] == 'm' && data[position + 2] == 't' &&
                    data[position + 3] == ' ')
                {
                    if (chunkSize < 16 || chunkDataOffset + chunkSize > data.Length)
                    {
                        return false;
                    }

                    formatTag = BitConverter.ToUInt16(data, chunkDataOffset);
                    if (chunkSize >= 40)
                    {
                        subFormatOffset = chunkDataOffset + 24;
                    }

                    return true;
                }

                var nextPosition = (long)chunkDataOffset + chunkSize + (chunkSize & 1);
                if (nextPosition > data.Length || nextPosition > int.MaxValue)
                {
                    return false;
                }

                position = (int)nextPosition;
            }

            return false;
        }

        private static unsafe byte[] DecodeWithLightCodec(byte[] data)
        {
            if (!TryGetAtracWaveInfo(data, out var info))
            {
                return null;
            }

            try
            {
                var codecType = info.FormatTag == 0x0270 ? AudioCodec.AT3 : AudioCodec.AT3plus;
                var codec = CodecFactory.Get(codecType);
                if (codec.init(info.BlockSize, info.Channels, info.Channels, info.CodingMode) < 0)
                {
                    return null;
                }

                var frame = new byte[info.BlockSize];
                var decodedSamples = new short[codec.NumberOfSamples * info.Channels];
                var decodedBytes = new byte[decodedSamples.Length * sizeof(short)];
                using var pcm = new MemoryStream();

                var dataEnd = info.DataOffset + info.DataLength;
                for (var position = info.DataOffset; position + info.BlockSize <= dataEnd;
                     position += info.BlockSize)
                {
                    Buffer.BlockCopy(data, position, frame, 0, frame.Length);
                    int consumed;
                    int outputLength;
                    fixed (byte* input = frame)
                    fixed (short* output = decodedSamples)
                    {
                        consumed = codec.decode(input, frame.Length, output, out outputLength);
                    }

                    if (consumed <= 0 || outputLength <= 0 || outputLength > decodedBytes.Length)
                    {
                        return null;
                    }

                    Buffer.BlockCopy(decodedSamples, 0, decodedBytes, 0, outputLength);
                    pcm.Write(decodedBytes, 0, outputLength);
                }

                var bytesPerSampleFrame = info.Channels * sizeof(short);
                var availableSamples = pcm.Length / bytesPerSampleFrame;
                // LightCodec exposes the ATRAC3+ synthesis delay, while at3tool removes it.
                // Compensate it in addition to the container's encoder delay.
                var firstSample = info.SampleOffset +
                                  (codecType == AudioCodec.AT3plus ? LightCodecAtrac3PlusDelay : 0);
                var sampleCount = info.SampleCount > 0 ? info.SampleCount : availableSamples - firstSample;
                if (firstSample < 0 || sampleCount <= 0 || firstSample + sampleCount > availableSamples)
                {
                    return null;
                }

                var pcmOffset = checked((int)(firstSample * bytesPerSampleFrame));
                var pcmLength = checked((int)(sampleCount * bytesPerSampleFrame));
                return BuildPcmWave(pcm.GetBuffer(), pcmOffset, pcmLength, info.SampleRate, info.Channels);
            }
            catch (Exception e)
            {
                Logger.Log(e);
                return null;
            }
        }

        private static bool TryGetAtracWaveInfo(byte[] data, out AtracWaveInfo info)
        {
            info = null;
            if (!IsAtrac3Wave(data))
            {
                return false;
            }

            var result = new AtracWaveInfo();
            var position = 12;
            while (position + 8 <= data.Length)
            {
                var chunkSize = BitConverter.ToInt32(data, position + 4);
                if (chunkSize < 0)
                {
                    return false;
                }

                var chunkOffset = position + 8;
                if ((long)chunkOffset + chunkSize > data.Length)
                {
                    return false;
                }

                if (ChunkEquals(data, position, "fmt "))
                {
                    if (chunkSize < 16)
                    {
                        return false;
                    }

                    result.FormatTag = BitConverter.ToUInt16(data, chunkOffset);
                    result.Channels = BitConverter.ToUInt16(data, chunkOffset + 2);
                    var sampleRate = BitConverter.ToUInt32(data, chunkOffset + 4);
                    if (sampleRate > int.MaxValue)
                    {
                        return false;
                    }

                    result.SampleRate = (int)sampleRate;
                    result.BlockSize = BitConverter.ToUInt16(data, chunkOffset + 12);
                    if (result.FormatTag == 0x0270 && chunkSize >= 26)
                    {
                        result.CodingMode = BitConverter.ToUInt16(data, chunkOffset + 24);
                    }
                }
                else if (ChunkEquals(data, position, "fact"))
                {
                    if (chunkSize >= 4)
                    {
                        result.SampleCount = BitConverter.ToUInt32(data, chunkOffset);
                    }

                    if (chunkSize >= 8)
                    {
                        result.SampleOffset = BitConverter.ToUInt32(data, chunkOffset + 4);
                    }
                }
                else if (ChunkEquals(data, position, "data"))
                {
                    result.DataOffset = chunkOffset;
                    result.DataLength = chunkSize;
                }

                var nextPosition = (long)chunkOffset + chunkSize + (chunkSize & 1);
                if (nextPosition > data.Length || nextPosition > int.MaxValue)
                {
                    return false;
                }

                position = (int)nextPosition;
            }

            if ((result.FormatTag != 0x0270 && result.FormatTag != 0xFFFE) ||
                result.Channels < 1 || result.Channels > 2 || result.SampleRate <= 0 ||
                result.BlockSize <= 0 || result.DataOffset <= 0 || result.DataLength < result.BlockSize)
            {
                return false;
            }

            info = result;
            return true;
        }

        private static bool ChunkEquals(byte[] data, int offset, string chunk)
        {
            return data[offset] == chunk[0] && data[offset + 1] == chunk[1] &&
                   data[offset + 2] == chunk[2] && data[offset + 3] == chunk[3];
        }

        private static byte[] BuildPcmWave(byte[] pcm, int pcmOffset, int pcmLength, int sampleRate,
            int channels)
        {
            using var output = new MemoryStream(44 + pcmLength);
            using (var writer = new BinaryWriter(output, Encoding.ASCII, true))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + pcmLength);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((ushort)1);
                writer.Write((ushort)channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * sizeof(short));
                writer.Write((ushort)(channels * sizeof(short)));
                writer.Write((ushort)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(pcmLength);
                writer.Write(pcm, pcmOffset, pcmLength);
            }

            return output.ToArray();
        }

        private sealed class AtracWaveInfo
        {
            public ushort FormatTag { get; set; }
            public int Channels { get; set; }
            public int SampleRate { get; set; }
            public int BlockSize { get; set; }
            public int CodingMode { get; set; }
            public long SampleCount { get; set; }
            public long SampleOffset { get; set; }
            public int DataOffset { get; set; }
            public int DataLength { get; set; }
        }

        private static bool TryGetLoopPoints(AudioMetadata md, out int start, out int end)
        {
            start = 0;
            end = 0;
            var loop = PsbResHelper.ParseLoopStr(md?.LoopStr?.Value);
            if (loop == null || loop.Count < 2 || loop[0] is not PsbNumber startNumber ||
                loop[1] is not PsbNumber endNumber)
            {
                return false;
            }

            start = startNumber.AsInt;
            end = endNumber.AsInt;
            return start >= 0 && end - start >= 6143;
        }

        private void SetData(PsArchData archData, byte[] data)
        {
            if (archData.Data == null)
            {
                archData.Data = new PsbResource { Data = data };
            }
            else
            {
                archData.Data.Data = data;
            }

            archData.Format = PsbAudioFormat.Atrac3Plus;
        }

        private byte[] RunTool(string options, byte[] input)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"FreeMote.At3.{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var inputPath = Path.Combine(tempDir, "input.wav");
            var outputPath = Path.Combine(tempDir, "output.wav");

            try
            {
                File.WriteAllBytes(inputPath, input);
                var info = new ProcessStartInfo(ToolPath,
                    $"{options} \"{inputPath}\" \"{outputPath}\"")
                {
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                using var process = Process.Start(info);
                process?.WaitForExit();

                if (process == null || process.ExitCode != 0 || !File.Exists(outputPath))
                {
                    return null;
                }

                var result = File.ReadAllBytes(outputPath);
                return result.Length > 0 ? result : null;
            }
            catch (Exception e)
            {
                Logger.Log(e);
                return null;
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
