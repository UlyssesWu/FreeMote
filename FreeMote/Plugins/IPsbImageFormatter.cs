using System.Collections.Generic;
using System.Drawing;

namespace FreeMote.Plugins
{
    public interface IPsbImageFormatter : IPsbPlugin
    {
        /// <summary>
        /// Target Extension (if have) e.g. ".png"
        /// </summary>
        List<string> Extensions { get; }
        bool CanToBitmap(in byte[] data, Dictionary<string, object> context = null);
        bool CanToBytes(Bitmap bitmap, Dictionary<string, object> context = null);
        Bitmap ToBitmap(in byte[] data, Dictionary<string, object> context = null);
        byte[] ToBytes(Bitmap bitmap, Dictionary<string, object> context = null);
    }
}
