using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
