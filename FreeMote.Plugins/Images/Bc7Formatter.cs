using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using BCnEncoder.Decoder;
using BCnEncoder.Encoder;
using BCnEncoder.Shared;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace FreeMote.Plugins.Images
{
    [Export(typeof(IPsbImageFormatter))]
    [ExportMetadata("Name", "FreeMote.Bc7")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "BC7 support via BCnEncoder.NET.")]
    class Bc7Formatter : IPsbImageFormatter
    {
        public List<string> Extensions { get; } = new() { ".bc7", ".dds" };
        public bool CanToBitmap(in byte[] data, Dictionary<string, object> context = null)
        {
            return true;
        }

        public bool CanToBytes(Bitmap bitmap, Dictionary<string, object> context = null)
        {
            if (bitmap != null && bitmap.PixelFormat == PixelFormat.Format32bppArgb)
                return true;
            return false;
        }

        public Bitmap ToBitmap(in byte[] data, int width, int height, PsbSpec platform, Dictionary<string, object> context = null)
        {
            var decoder = new BcDecoder();
            //var bufferSize = decoder.GetBlockSize(CompressionFormat.Bc7) * width * height;
            //Debug.WriteLine($"size: expect: {bufferSize} ; actual: {buffer.Length}");

            var pixels = decoder.DecodeRaw(data, width, height, CompressionFormat.Bc7);
            var pixelBytes = MemoryMarshal.Cast<ColorRgba32, byte>(pixels);
            return RL.ConvertToImage(pixelBytes.ToArray(), width, height, PsbPixelFormat.BeRGBA8);
        }

        public byte[] ToBytes(Bitmap bitmap, PsbSpec platform, Dictionary<string, object> context = null)
        {
            var encoder = new BcEncoder(CompressionFormat.Bc7)
                {OutputOptions = {GenerateMipMaps = false, Quality = CompressionQuality.Fast}};
            var pixelBytes = RL.GetPixelBytesFromImage(bitmap, PsbPixelFormat.BeRGBA8);
            var pixels = encoder.EncodeToRawBytes(pixelBytes, bitmap.Width, bitmap.Height, BCnEncoder.Encoder.PixelFormat.Rgba32);
            return pixels[0];
        }
    }
}
