using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;

namespace FreeMote.Plugins.Images
{
    [Export(typeof(IPsbImageFormatter))]
    [ExportMetadata("Name", "FreeMote.WebP")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "WebP support (WIP).")]
    internal class WebPFormatter : IPsbImageFormatter
    {
        public List<string> Extensions { get; } = new() { ".webp" };
        public bool CanToBitmap(in byte[] data, Dictionary<string, object> context = null)
        {
            return false;
        }

        public bool CanToBytes(Bitmap bitmap, Dictionary<string, object> context = null)
        {
            return false;
        }

        public Bitmap ToBitmap(in byte[] data, int width, int height, PsbSpec platform, Dictionary<string, object> context = null)
        {
            return null;
        }

        public byte[] ToBytes(Bitmap bitmap, PsbSpec platform, Dictionary<string, object> context = null)
        {
            return null;
        }
    }
}
