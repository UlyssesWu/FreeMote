using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;

namespace FreeMote.Plugins.Images
{
    [Export(typeof(IPsbImageFormatter))]
    [ExportMetadata("Name", "FreeMote.WebP")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "WebP encoding and decoding support.")]
    internal class WebPFormatter : IPsbImageFormatter
    {
        public List<string> Extensions { get; } = new() { ".webp" };
        public bool CanToBitmap(in byte[] data, Dictionary<string, object> context = null)
        {
            return data is {Length: >= 16} &&
                   data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F' &&
                   data[8] == (byte)'W' && data[9] == (byte)'E' && data[10] == (byte)'B' && data[11] == (byte)'P' &&
                   data[12] == (byte)'V' && data[13] == (byte)'P' && data[14] == (byte)'8';
        }

        public bool CanToBytes(Bitmap bitmap, Dictionary<string, object> context = null)
        {
            return bitmap != null;
        }

        public Bitmap ToBitmap(in byte[] data, int width, int height, PsbSpec platform, Dictionary<string, object> context = null)
        {
            using var webP = new global::FreeMote.WebP.WebP();
            return webP.Decode(data);
        }

        public byte[] ToBytes(Bitmap bitmap, PsbSpec platform, Dictionary<string, object> context = null)
        {
            using var webP = new global::FreeMote.WebP.WebP();
            return webP.EncodeLossy(bitmap, 90);
        }
    }
}
