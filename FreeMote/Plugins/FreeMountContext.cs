using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FreeMote.Plugins
{
    public class FreeMountContext
    {
        public Dictionary<string, object> Context { get; set; }

        public FreeMountContext(Dictionary<string,object> context)
        {
            Context = context;
        }

        public Bitmap ResourceToBitmap(string ext, in byte[] data)
        {
            return FreeMount._.ResourceToBitmap(ext, data, Context);
        }

        public byte[] BitmapToResource(string ext, Bitmap bitmap)
        {
            return FreeMount._.BitmapToResource(ext, bitmap, Context);
        }

        //TODO:
        //public Stream OpenFromShell(Stream stream)
        //{
        //    return FreeMount._.OpenFromShell(stream, ref type, Context);
        //}

        //public Stream PackToShell(Stream input)
        //{
        //    return FreeMount._.PackToShell(input, type, out output, Context);
        //}

    }
}
