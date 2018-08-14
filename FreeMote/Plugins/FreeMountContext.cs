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

        public bool HasShell => Context[FreeMount.PsbShellType] != null && !string.IsNullOrEmpty(Context[FreeMount.PsbShellType].ToString());

        public bool SupportImageExt(string ext)
        {
            return FreeMount._.ImageFormatters.ContainsKey(ext);
        }


        public Bitmap ResourceToBitmap(string ext, in byte[] data)
        {
            return FreeMount._.ResourceToBitmap(ext, data, Context);
        }

        public byte[] BitmapToResource(string ext, Bitmap bitmap)
        {
            return FreeMount._.BitmapToResource(ext, bitmap, Context);
        }

        public MemoryStream OpenFromShell(Stream stream, ref string type)
        {
            return FreeMount._.OpenFromShell(stream, ref type, Context);
        }

        public MemoryStream PackToShell(Stream input, string type = null)
        {
            return FreeMount._.PackToShell(input, type ?? Context[FreeMount.PsbShellType] as string, Context);
        }

    }
}
