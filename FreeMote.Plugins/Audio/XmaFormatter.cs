using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using FreeMote.Psb;

namespace FreeMote.Plugins.Audio
{
    [Export(typeof(IPsbAudioFormatter))]
    [ExportMetadata("Name", "FreeMote.Xma")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "Xbox XMA2 support.")]
    public class XmaFormatter : IPsbAudioFormatter
    {
        private const string EncoderTool = "xma2encode.exe";
        private static int _useToolNotice;
        private static int _missingToolNotice;
        private static readonly SemaphoreSlim ToolSemaphore =
            new SemaphoreSlim(Math.Max(1, Math.Min(Environment.ProcessorCount, 4)));
        private static readonly uint[] SupportedSampleRates =
            {8000, 11025, 12000, 16000, 22050, 24000, 32000, 44100, 48000};

        public List<string> Extensions { get; } = new List<string> {".xma"};
        public string ToolPath { get; set; }

        public XmaFormatter()
        {
            var toolPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? "", "Tools",
                EncoderTool);
            if (File.Exists(toolPath))
            {
                ToolPath = toolPath;
            }
        }

        public bool CanToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            return archData is XmaArchData xma && XmaArchData.IsXma2Format(xma.Fmt?.Data) &&
                   xma.Data?.Data != null;
        }

        public bool CanToArchData(byte[] wave, Dictionary<string, object> context = null)
        {
            if (IsXma2Wave(wave))
            {
                return true;
            }

            if (!IsWave(wave))
            {
                return false;
            }

            if (!File.Exists(ToolPath))
            {
                LogMissingToolNotice();
                return false;
            }

            return true;
        }

        public byte[] ToWave(AudioMetadata md, IArchData archData, string fileName = null,
            Dictionary<string, object> context = null)
        {
            if (archData is not XmaArchData xma)
            {
                return null;
            }

            xma.WaveExtension = ".wav";
            var xmaWave = xma.ToXmaWave();
            if (!File.Exists(ToolPath))
            {
                LogMissingToolNotice();
                return xmaWave;
            }

            LogUseToolNotice();
            var pcm = RunTool(xmaWave, true);
            if (pcm != null)
            {
                return NormalizeDecodedPcm(pcm);
            }

            Logger.LogWarn("[WARN] XMA2 decode failed; exporting the encoded XMA2 wave instead.");
            return xmaWave;
        }

        public bool ToArchData(AudioMetadata md, IArchData archData, in byte[] wave, string fileName,
            string waveExt, Dictionary<string, object> context = null)
        {
            if (archData is not XmaArchData xma)
            {
                return false;
            }

            var xmaWave = wave;
            if (!IsXma2Wave(xmaWave))
            {
                if (!File.Exists(ToolPath) || !IsWave(wave))
                {
                    if (!File.Exists(ToolPath))
                    {
                        LogMissingToolNotice();
                    }
                    return false;
                }

                LogUseToolNotice();
                xmaWave = RunTool(wave, false);
                if (xmaWave == null || !IsXma2Wave(xmaWave))
                {
                    Logger.LogWarn("[WARN] XMA2 encode failed; the audio resource was not changed.");
                    return false;
                }
            }

            using var stream = new MemoryStream(xmaWave, false);
            return xma.ReadFromXmaWave(stream);
        }

        public bool TryGetArchData(AudioMetadata md, PsbDictionary channel, out IArchData data,
            Dictionary<string, object> context = null)
        {
            data = null;
            if (md.Spec != PsbSpec.xone && md.Spec != PsbSpec.x360)
            {
                return false;
            }

            if (channel.Count != 1 || channel["archData"] is not PsbDictionary archData ||
                archData["data"] is not PsbResource audio || archData["fmt"] is not PsbResource fmt)
            {
                return false;
            }

            // Resource data can be null while a JSON/resx pair is being linked. On Xbox,
            // this exact two-resource layout is the XMA2 representation used by sound_archive.
            if (fmt.Data is {Length: > 0} && !XmaArchData.IsXma2Format(fmt.Data))
            {
                return false;
            }

            data = new XmaArchData
            {
                Data = audio,
                Fmt = fmt
            };
            return true;
        }

        public static bool IsXma2Wave(byte[] wave)
        {
            if (wave == null || wave.Length < 20 || !wave.AsciiEqual("RIFF") ||
                wave[8] != 'W' || wave[9] != 'A' || wave[10] != 'V' || wave[11] != 'E')
            {
                return false;
            }

            var position = 12;
            while (position + 8 <= wave.Length)
            {
                var size = System.BitConverter.ToUInt32(wave, position + 4);
                var dataOffset = position + 8;
                if (size > int.MaxValue || (long)dataOffset + size > wave.Length)
                {
                    return false;
                }

                if (wave[position] == 'f' && wave[position + 1] == 'm' && wave[position + 2] == 't' &&
                    wave[position + 3] == ' ')
                {
                    if (size > int.MaxValue)
                    {
                        return false;
                    }

                    var fmt = new byte[(int)size];
                    System.Buffer.BlockCopy(wave, dataOffset, fmt, 0, fmt.Length);
                    return XmaArchData.IsXma2Format(fmt);
                }

                var next = (long)dataOffset + size + (size & 1);
                if (next > int.MaxValue)
                {
                    return false;
                }
                position = (int)next;
            }

            return false;
        }

        private static bool IsWave(byte[] wave)
        {
            return wave is {Length: >= 12} && wave.AsciiEqual("RIFF") &&
                   wave[8] == 'W' && wave[9] == 'A' && wave[10] == 'V' && wave[11] == 'E';
        }

        private byte[] RunTool(byte[] input, bool decode)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"FreeMote.Xma.{Guid.NewGuid():N}");
            var inputPath = Path.Combine(tempDir, decode ? "input.xma" : "input.wav");
            var outputPath = Path.Combine(tempDir, decode ? "output.wav" : "output.xma");

            ToolSemaphore.Wait();
            try
            {
                Directory.CreateDirectory(tempDir);
                File.WriteAllBytes(inputPath, input);
                var arguments = decode
                    ? $"\"{inputPath}\" /DecodeToPCM \"{outputPath}\""
                    : $"\"{inputPath}\" /TargetFile \"{outputPath}\" /UseLoopPoints";
                var info = new ProcessStartInfo(ToolPath, arguments)
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
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception e)
                {
                    Logger.Log(e);
                }
                finally
                {
                    ToolSemaphore.Release();
                }
            }
        }

        /// <summary>
        /// xma2encode emits PCM with an oversized fmt chunk and can retain Xbox's
        /// 47999 Hz clock value, which the same encoder refuses as input. Rebuild a
        /// conventional PCM header so an extracted file can be encoded again.
        /// </summary>
        private static byte[] NormalizeDecodedPcm(byte[] wave)
        {
            if (!TryGetChunk(wave, "fmt ", out var fmtOffset, out var fmtLength) || fmtLength < 16 ||
                !TryGetChunk(wave, "data", out var dataOffset, out var dataLength) ||
                BitConverter.ToUInt16(wave, fmtOffset) != 1)
            {
                return wave;
            }

            var channels = BitConverter.ToUInt16(wave, fmtOffset + 2);
            var sampleRate = BitConverter.ToUInt32(wave, fmtOffset + 4);
            var blockAlign = BitConverter.ToUInt16(wave, fmtOffset + 12);
            var bitsPerSample = BitConverter.ToUInt16(wave, fmtOffset + 14);
            if (channels == 0 || blockAlign == 0 || bitsPerSample == 0)
            {
                return wave;
            }

            // Some Xbox assets store clocks a few Hz away from a rate accepted by
            // xma2encode (for example 47972 or 47999 instead of nominal 48000).
            sampleRate = NormalizeSampleRate(sampleRate);

            using var output = new MemoryStream(44 + dataLength + (dataLength & 1));
            using var writer = new BinaryWriter(output, Encoding.ASCII, true);
            writer.WriteUTF8("RIFF");
            writer.Write(36 + dataLength + (dataLength & 1));
            writer.WriteUTF8("WAVE");
            writer.WriteUTF8("fmt ");
            writer.Write(16);
            writer.Write((ushort)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(checked(sampleRate * blockAlign));
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);
            writer.WriteUTF8("data");
            writer.Write(dataLength);
            writer.Write(wave, dataOffset, dataLength);
            if ((dataLength & 1) != 0)
            {
                writer.Write((byte)0);
            }
            return output.ToArray();
        }

        private static uint NormalizeSampleRate(uint sampleRate)
        {
            var bestRate = sampleRate;
            var bestDifference = uint.MaxValue;
            foreach (var supportedRate in SupportedSampleRates)
            {
                var difference = sampleRate > supportedRate
                    ? sampleRate - supportedRate
                    : supportedRate - sampleRate;
                if (difference < bestDifference)
                {
                    bestRate = supportedRate;
                    bestDifference = difference;
                }
            }

            return bestDifference <= 100 ? bestRate : sampleRate;
        }

        private static void LogUseToolNotice()
        {
            if (Interlocked.Exchange(ref _useToolNotice, 1) == 0)
            {
                Logger.LogHint("[XMA] Try using xma2encode to encode/decode...");
            }
        }

        private static void LogMissingToolNotice()
        {
            if (Interlocked.Exchange(ref _missingToolNotice, 1) == 0)
            {
                Logger.LogWarn("[XMA] xma2encode was not found. Decoding will export XMA2 data, which may not be directly playable and may require manual conversion; PCM WAV cannot be encoded automatically.");
            }
        }

        private static bool TryGetChunk(byte[] wave, string chunkId, out int dataOffset, out int dataLength)
        {
            dataOffset = 0;
            dataLength = 0;
            if (!IsWave(wave))
            {
                return false;
            }

            var position = 12;
            while (position + 8 <= wave.Length)
            {
                var size = BitConverter.ToUInt32(wave, position + 4);
                var offset = position + 8;
                if (size > int.MaxValue || (long)offset + size > wave.Length)
                {
                    return false;
                }

                if (wave[position] == chunkId[0] && wave[position + 1] == chunkId[1] &&
                    wave[position + 2] == chunkId[2] && wave[position + 3] == chunkId[3])
                {
                    dataOffset = offset;
                    dataLength = (int)size;
                    return true;
                }

                var next = (long)offset + size + (size & 1);
                if (next > int.MaxValue)
                {
                    return false;
                }
                position = (int)next;
            }

            return false;
        }
    }
}
