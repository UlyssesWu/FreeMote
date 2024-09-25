using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace FreeMote
{
    public static class BitmapExtension
    {
        /// <summary>
        /// Copies a region of the source bitmap into this fast bitmap
        /// </summary>
        /// <param name="target">The image which <paramref name="source"/> applies to</param>
        /// <param name="source">The source image to copy</param>
        /// <param name="srcRect">The region on the source bitmap that will be copied over</param>
        /// <param name="destRect">The region on this fast bitmap that will be changed</param>
        /// <exception cref="ArgumentException">The provided source bitmap is the same bitmap locked in this FastBitmap</exception>
        public static void CopyRegion(this Bitmap target, Bitmap source, Rectangle srcRect, Rectangle destRect)
        {
            if (srcRect.Width != destRect.Width || srcRect.Height != destRect.Height)
            {
                throw new ArgumentException("Source and destination rectangles must have the same dimensions.");
            }
            
            var srcData = source.LockBits(srcRect, ImageLockMode.ReadOnly, source.PixelFormat);
            var destData = target.LockBits(destRect, ImageLockMode.WriteOnly, target.PixelFormat);

            try
            {
                unsafe
                {
                    byte* srcPtr = (byte*)srcData.Scan0;
                    byte* destPtr = (byte*)destData.Scan0;

                    int bytesPerPixel = Image.GetPixelFormatSize(source.PixelFormat) / 8;
                    int stride = srcData.Stride;
                    int height = srcRect.Height;
                    int widthInBytes = srcRect.Width * bytesPerPixel;

                    for (int y = 0; y < height; y++)
                    {
                        byte* srcRow = srcPtr + (y * stride);
                        byte* destRow = destPtr + (y * destData.Stride);

                        for (int x = 0; x < widthInBytes; x++)
                        {
                            destRow[x] = srcRow[x];
                        }
                    }
                }
            }
            finally
            {                
                source.UnlockBits(srcData);
                target.UnlockBits(destData);
            }
        }

        /// <summary>
        /// Resize the image to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        /// https://stackoverflow.com/a/24199315/4374462
        public static Bitmap ResizeImage(this Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality; // = SmoothingMode.AntiAlias
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }
    }
}
