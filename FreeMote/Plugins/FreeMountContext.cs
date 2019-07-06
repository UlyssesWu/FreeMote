using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace FreeMote.Plugins
{

    public class FreeMountContext
    {
        public Dictionary<string, object> Context { get; set; }

        public FreeMountContext(Dictionary<string, object> context)
        {
            Context = context;
        }

        public bool HasShell => Context.ContainsKey(Consts.Context_PsbShellType) && Context[Consts.Context_PsbShellType] != null && !string.IsNullOrEmpty(Context[Consts.Context_PsbShellType].ToString());

        public bool SupportImageExt(string ext)
        {
            return FreeMount._.ImageFormatters.ContainsKey(ext);
        }

        /// <summary>
        /// Use plugins to convert resource bytes to bitmap
        /// </summary>
        /// <param name="ext"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public Bitmap ResourceToBitmap(string ext, in byte[] data)
        {
            return FreeMount._.ResourceToBitmap(ext, data, Context);
        }

        /// <summary>
        /// Use plugins to convert bitmap to resource bytes
        /// </summary>
        /// <param name="ext"></param>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        public byte[] BitmapToResource(string ext, Bitmap bitmap)
        {
            return FreeMount._.BitmapToResource(ext, bitmap, Context);
        }

        /// <summary>
        /// Use plugins to decompress shell types to PSB
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public MemoryStream OpenFromShell(Stream stream, ref string type)
        {
            return FreeMount._.OpenFromShell(stream, ref type, Context);
        }

        /// <summary>
        /// Use plugins to compress PSB to shell type
        /// </summary>
        /// <param name="input"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public MemoryStream PackToShell(Stream input, string type = null)
        {
            return FreeMount._.PackToShell(input, type ?? Context[Consts.Context_PsbShellType] as string, Context);
        }

        /// <summary>
        /// Use plugin to try to get PSB key
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public uint? GetKey(Stream input)
        {
            return FreeMount._.GetKey(input, Context);
        }
    }
}
