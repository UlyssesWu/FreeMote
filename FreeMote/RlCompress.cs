using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace FreeMote
{
    public enum PsbImageFormat
    {
        Bmp,
        Png,
    }

    public enum PsbPixelFormat
    {
        None,
        /// <summary>
        /// Little Endian RGBA8
        /// </summary>
        WinRGBA8,
        /// <summary>
        /// Big Endian RGBA8
        /// </summary>
        CommonRGBA8,
    }

    /// <summary>
    /// RL Compress for Image
    /// </summary>
    public static class RlCompress
    {
        /// <summary>
        /// Pixel Color Convert
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="littleEndian"></param>
        public static unsafe void Rgba2Argb(ref byte[] bytes, bool littleEndian = false)
        {
            //ARGB actually is BGRA in little-endian
            fixed (byte* ptr = bytes)
            {
                int i = 0;
                int len = bytes.Length / 4;
                while (i < len)
                {
                    uint* iPtr = (uint*)ptr + i;
                    if (*iPtr != 0)
                    {
                        var p1 = (*iPtr & 0xFF000000) >> 24;
                        var p2 = (*iPtr & 0x00FF0000) >> 16;
                        var p3 = (*iPtr & 0x0000FF00) >> 8;
                        var p4 = (*iPtr & 0x000000FF);

                        if (littleEndian) //Win
                        {
                            *iPtr = (p1 << 24) | (p2 << 16) | (p3 << 8) | (p4);
                        }
                        else //Common
                        {
                            *iPtr = (p1 << 24) | (p4 << 16) | (p3 << 8) | (p2);
                        }
                    }
                    i++;
                }
            }
        }

        public static byte[] Compress(Stream stream, int align = 4)
        {
            return PixelCompress.Compress(stream, align);
        }

        public static byte[] Compress(byte[] data, int align = 4)
        {
            using (var stream = new MemoryStream(data))
            {
                return Compress(stream, align);
            }
        }

        public static byte[] CompressImageFile(string path)
        {
            return Compress(PixelBytesFromImage(new Bitmap(path)));
        }

        public static byte[] GetPixelBytesFromImageFile(string path)
        {
            Bitmap bmp = new Bitmap(path);
            return PixelBytesFromImage(bmp);
        }
        public static byte[] GetPixelBytesFromImage(Image image)
        {
            Bitmap bmp = new Bitmap(image);
            return PixelBytesFromImage(bmp);
        }

        private static byte[] PixelBytesFromImage(Bitmap bmp)
        {
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            int stride = bmpData.Stride; // 扫描线的宽度
            int offset = stride - bmp.Width; // 显示宽度与扫描线宽度的间隙
            IntPtr iptr = bmpData.Scan0; // 获取bmpData的内存起始位置
            int scanBytes = stride * bmp.Height; // 用stride宽度，表示这是内存区域的大小

            var result = new byte[scanBytes];
            System.Runtime.InteropServices.Marshal.Copy(iptr, result, 0, scanBytes);
            bmp.UnlockBits(bmpData); // 解锁内存区域
            return result;
        }

        public static void UncompressToImageFile(byte[] data, string path, int height, int width, PsbImageFormat format = PsbImageFormat.Png, PsbPixelFormat colorFormat = PsbPixelFormat.None, int align = 4)
        {
            byte[] bytes;
            try
            {
                bytes = Uncompress(data, height, width, align);
            }
            catch (Exception e)
            {
                throw new BadImageFormatException("data incorrect", e);
            }
            ConvertToImageFile(bytes, path, height, width, format, colorFormat);
        }

        public static void ConvertToImageFile(byte[] data, string path, int height, int width, PsbImageFormat format, PsbPixelFormat colorFormat = PsbPixelFormat.None)
        {
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            if (colorFormat == PsbPixelFormat.WinRGBA8)
            {
                Rgba2Argb(ref data, true);
            }
            else if (colorFormat == PsbPixelFormat.CommonRGBA8)
            {
                Rgba2Argb(ref data, false);
            }

            int stride = bmpData.Stride; // 扫描线的宽度
            int offset = stride - width; // 显示宽度与扫描线宽度的间隙
            IntPtr iptr = bmpData.Scan0; // 获取bmpData的内存起始位置
            int scanBytes = stride * height; // 用stride宽度，表示这是内存区域的大小

            if (scanBytes >= data.Length)
            {
                System.Runtime.InteropServices.Marshal.Copy(data, 0, iptr, data.Length);
                bmp.UnlockBits(bmpData); // 解锁内存区域
                switch (format)
                {
                    case PsbImageFormat.Bmp:
                        bmp.Save(path, ImageFormat.Bmp);
                        break;
                    case PsbImageFormat.Png:
                        bmp.Save(path, ImageFormat.Png);
                        break;
                }

                return;
            }
            throw new BadImageFormatException("data may not corresponding");
        }

        private static byte[] Uncompress(Stream stream, int height, int width, int align = 4)
        {
            var realLength = height * width * align;
            return PixelCompress.Uncompress(stream, align, realLength);
        }

        public static byte[] Uncompress(byte[] data, int height, int width, int align = 4)
        {
            using (var stream = new MemoryStream(data))
            {
                return Uncompress(stream, height, width, align);
            }
        }

        public static byte[] Uncompress(byte[] data, int align = 4)
        {
            using (var stream = new MemoryStream(data))
            {
                return PixelCompress.Uncompress(stream, align);
            }
        }
    }
}
