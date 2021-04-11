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

        /// <summary>
        /// Check if <see cref="ToBitmap"/> is available
        /// </summary>
        /// <param name="data"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        bool CanToBitmap(in byte[] data, Dictionary<string, object> context = null);

        /// <summary>
        /// Check if <see cref="ToBytes"/> is available
        /// </summary>
        /// <param name="bitmap"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        bool CanToBytes(Bitmap bitmap, Dictionary<string, object> context = null);

        /// <summary>
        /// Convert image bytes to <see cref="Bitmap"/>
        /// </summary>
        /// <param name="data"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        Bitmap ToBitmap(in byte[] data, Dictionary<string, object> context = null);

        /// <summary>
        /// Convert <see cref="Bitmap"/> to image bytes
        /// </summary>
        /// <param name="bitmap"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        byte[] ToBytes(Bitmap bitmap, Dictionary<string, object> context = null);
    }
}
