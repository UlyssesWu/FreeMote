//using BCnEncoder.Decoder;
//using BCnEncoder.Encoder;
//using BCnEncoder.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace FreeMote.Plugins.Images
{
    [Export(typeof(IPsbImageFormatter))]
    [ExportMetadata("Name", "FreeMote.Bc7")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "BC7 support via bc7enc.")]
    class Bc7Formatter : IPsbImageFormatter
    {
        private const string EncoderTool = "bc7enc.exe";
        public List<string> Extensions { get; } = new() {".bc7", ".dds"};
        public string ToolPath { get; set; } = null;
        private static bool _useToolNotice = false;
        //private static bool _usePluginNotice = false;

        public Bc7Formatter()
        {
            var toolPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? "", "Tools", EncoderTool);
            if (File.Exists(toolPath))
            {
                ToolPath = toolPath;
            }
        }
        
        public bool CanToBitmap(in byte[] data, Dictionary<string, object> context = null)
        {
            return true;
        }

        public bool CanToBytes(Bitmap bitmap, Dictionary<string, object> context = null)
        {
            if (bitmap != null && bitmap.PixelFormat == PixelFormat.Format32bppArgb && File.Exists(ToolPath))
                return true;
            return false;
        }

        public Bitmap ToBitmap(in byte[] data, int width, int height, PsbSpec platform, Dictionary<string, object> context = null)
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

            var outData = new Bc7Decoder(data, width, height).Unpack();
            return RL.ConvertToImage(outData, width, height);

            //var decoder = new BcDecoder();
            ////var bufferSize = decoder.GetBlockSize(CompressionFormat.Bc7) * width * height;
            ////Debug.WriteLine($"size: expect: {bufferSize} ; actual: {buffer.Length}");

            //var pixels = decoder.DecodeRaw(data, width, height, CompressionFormat.Bc7);
            //var pixelBytes = MemoryMarshal.Cast<ColorRgba32, byte>(pixels);
            //return RL.ConvertToImage(pixelBytes.ToArray(), width, height, PsbPixelFormat.BeRGBA8);
        }

        public byte[] ToBytes(Bitmap bitmap, PsbSpec platform, Dictionary<string, object> context = null)
        {
            byte[] outBytes = null;
            if (File.Exists(ToolPath))
            {
                if (!_useToolNotice)
                {
                    Logger.LogHint("[BC7] Try using bc7enc to encode...");
                    _useToolNotice = true;
                }
                try
                {
                    var tempFile = Path.GetTempFileName();
                    bitmap.Save(tempFile);
                    var tempOutFile = Path.ChangeExtension(tempFile, ".bc7");

                    ProcessStartInfo info = new ProcessStartInfo(ToolPath, $"-R -C -e -g -q -o \"{tempFile}\"")
                    {
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    };
                    Process process = Process.Start(info);
                    process?.WaitForExit();

                    if (process?.ExitCode == 0)
                    {
                        if (!File.Exists(tempOutFile))
                        {
                            Logger.LogError("[ERROR] BC7 convert failed.");
                            File.Delete(tempFile);
                        }
                        else
                        {
                            outBytes = File.ReadAllBytes(tempOutFile);
                            if (outBytes.Length == 0)
                            {
                                Logger.LogWarn("[WARN] BC7 encoder output length is 0");
                            }
                            File.Delete(tempFile);
                            File.Delete(tempOutFile);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
            }

            return outBytes;

            //if (!_usePluginNotice)
            //{
            //    Logger.LogHint("[BC7] Try using BCnEncoder to encode. Most likely it will fail.");
            //    _usePluginNotice = true;
            //}

            //var encoder = new BcEncoder(CompressionFormat.Bc7)
            //    {OutputOptions = {GenerateMipMaps = false, Quality = CompressionQuality.Fast}};
            //var pixelBytes = RL.GetPixelBytesFromImage(bitmap, PsbPixelFormat.BeRGBA8);
            //var pixels = encoder.EncodeToRawBytes(pixelBytes, bitmap.Width, bitmap.Height, BCnEncoder.Encoder.PixelFormat.Rgba32);
            //return pixels[0]; //other = mipmap
        }
    }
}