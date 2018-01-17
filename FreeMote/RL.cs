using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace FreeMote
{
    /// <summary>
    /// RL Compress for Image
    /// </summary>
    public static class RL
    {
        /// <summary>
        /// RGBA & ARGB conversion
        /// </summary>
        /// <param name="bytes"></param>
        public static unsafe void Rgba2Argb(ref byte[] bytes)
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
                        *iPtr = ((*iPtr & 0xFF000000) >> 24 << 24) |
                            ((*iPtr & 0x000000FF) << 16) |
                            ((*iPtr & 0x0000FF00) >> 8 << 8) |
                            ((*iPtr & 0x00FF0000) >> 16);
                    }
                    i++;
                }
            }
        }

        /// <summary>
        /// RGBA4444 & RGBA8 conversion
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="extend">true: 4 to 8; false: 8 to 4</param>
        /// Shibuya Scramble!
        public static byte[] Rgba428(byte[] bytes, bool extend = true)
        {
            if (extend)
            {
                var result = new byte[bytes.Length * 2];
                var dst = 0;
                for (int i = 0; i < bytes.Length; i += 2)
                {
                    var p = BitConverter.ToUInt16(bytes, i);
                    result[dst++] = (byte)((p & 0x000Fu) * 0xFFu / 0x000Fu);
                    result[dst++] = (byte)((p & 0x00F0u) * 0xFFu / 0x00F0u);
                    result[dst++] = (byte)((p & 0x0F00u) * 0xFFu / 0x0F00u);
                    result[dst++] = (byte)((p & 0xF000u) * 0xFFu / 0xF000u);
                }
                return result;
            }
            else
            {
                var result = new byte[bytes.Length / 2];
                for (int i = 0; i < result.Length; i += 2)
                {
                    ushort p = (ushort) ((bytes[i * 2] / 16) | 
                                         (bytes[i * 2 + 1] / 16) << 4 |
                                         (bytes[i * 2 + 2] / 16) << 8 |
                                         (bytes[i * 2 + 3] / 16) << 12);
                    BitConverter.GetBytes(p).CopyTo(result, i);
                }
                return result;
            }
        }

        public static byte[] Compress(Stream stream, int align = 4)
        {
            return RleCompress.Compress(stream, align);
        }

        public static byte[] Compress(byte[] data, int align = 4)
        {
            using (var stream = new MemoryStream(data))
            {
                return Compress(stream, align);
            }
        }

        public static byte[] CompressImage(Bitmap image, PsbPixelFormat pixelFormat = PsbPixelFormat.None)
        {
            return Compress(PixelBytesFromImage(image, pixelFormat));
        }

        public static byte[] CompressImageFile(string path, PsbPixelFormat pixelFormat = PsbPixelFormat.None)
        {
            return CompressImage(new Bitmap(path, false), pixelFormat);
        }

        public static byte[] GetPixelBytesFromImageFile(string path, PsbPixelFormat pixelFormat = PsbPixelFormat.None)
        {
            Bitmap bmp = new Bitmap(path, false);
            return PixelBytesFromImage(bmp, pixelFormat);
        }

        public static byte[] GetPixelBytesFromImage(Image image, PsbPixelFormat pixelFormat = PsbPixelFormat.None)
        {
            if (!(image is Bitmap bmp))
            {
                bmp = new Bitmap(image);
            }
            return PixelBytesFromImage(bmp, pixelFormat);
        }

        private static byte[] PixelBytesFromImage(Bitmap bmp, PsbPixelFormat pixelFormat = PsbPixelFormat.None)
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

            switch (pixelFormat)
            {
                case PsbPixelFormat.RGBA4444:
                    result = Rgba428(result, false);
                    break;
                case PsbPixelFormat.CommonRGBA8:
                    Rgba2Argb(ref result);
                    break;
                case PsbPixelFormat.DXT5:
                    //Rgba2Argb(ref result);
                    result = DxtUtil.Dxt5Encode(result, bmp.Width, bmp.Height);
                    break;
            }
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

        public static Bitmap UncompressToImage(byte[] data, int height, int width, PsbPixelFormat colorFormat = PsbPixelFormat.None, int align = 4)
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
            return ConvertToImage(bytes, height, width, colorFormat);
        }

        public static Bitmap ConvertToImage(byte[] data, int height, int width, PsbPixelFormat colorFormat = PsbPixelFormat.None)
        {
            Bitmap bmp;
            BitmapData bmpData;
            {
                bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                bmpData = bmp.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly, bmp.PixelFormat);
            }
            switch (colorFormat)
            {
                case PsbPixelFormat.RGBA4444:
                    data = Rgba428(data);
                    break;
                case PsbPixelFormat.CommonRGBA8:
                    Rgba2Argb(ref data);
                    break;
                case PsbPixelFormat.DXT5: //MARK: RL seems compatible to DXT5 compress?
                    data = DxtUtil.DecompressDxt5(data, width, height);
                    Rgba2Argb(ref data); //DXT5(for win) need conversion
                    break;
            }

            int stride = bmpData.Stride; // 扫描线的宽度
            int offset = stride - width; // 显示宽度与扫描线宽度的间隙
            IntPtr iptr = bmpData.Scan0; // 获取bmpData的内存起始位置
            int scanBytes = stride * height; // 用stride宽度，表示这是内存区域的大小

            if (scanBytes >= data.Length)
            {
                System.Runtime.InteropServices.Marshal.Copy(data, 0, iptr, data.Length);
                bmp.UnlockBits(bmpData); // 解锁内存区域
                return bmp;
            }
            throw new BadImageFormatException("data may not corresponding");
        }

        public static void ConvertToImageFile(byte[] data, string path, int height, int width, PsbImageFormat format, PsbPixelFormat colorFormat = PsbPixelFormat.None)
        {
            var bmp = ConvertToImage(data, height, width, colorFormat);
            switch (format)
            {
                case PsbImageFormat.Bmp:
                    bmp.Save(path, ImageFormat.Bmp);
                    break;
                case PsbImageFormat.Png:
                    bmp.Save(path, ImageFormat.Png);
                    break;
            }
        }

        private static byte[] Uncompress(Stream stream, int height, int width, int align = 4)
        {
            var realLength = height * width * align;
            return RleCompress.Uncompress(stream, align, realLength);
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
                return RleCompress.Uncompress(stream, align);
            }
        }
    }
}
