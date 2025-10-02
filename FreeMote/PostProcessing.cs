// Untile/Unswizzle by xdaniel. Copyright(c) 2016 xdaniel(Daniel R.) / DigitalZero Domain. License: The MIT License (MIT) 
// Tile/Swizzle by Ulysses (wdwxy12345@gmail.com). License: same as FreeMote
// UntileTextureRvl/TileTextureRvl was fixed by Krisp (FreeMote#139)

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;

namespace FreeMote
{
    /// <summary>
    /// Texture Post Process for consoles
    /// </summary>
    public static class PostProcessing
    {
        // Unswizzle logic by @FireyFly
        // http://xen.firefly.nu/up/rearrange.c.html

        #region Untile

        static readonly int[] tileOrderPs4 =
        {
            00, 01, 08, 09, 02, 03, 10, 11,
            16, 17, 24, 25, 18, 19, 26, 27,
            04, 05, 12, 13, 06, 07, 14, 15,
            20, 21, 28, 29, 22, 23, 30, 31,
            32, 33, 40, 41, 34, 35, 42, 43,
            48, 49, 56, 57, 50, 51, 58, 59,
            36, 37, 44, 45, 38, 39, 46, 47,
            52, 53, 60, 61, 54, 55, 62, 63
        }; //Also used in 3DS

        static readonly int[] tileOrderDefault =
        {
            00, 01, 02, 03, 04, 05, 06, 07,
            08, 09, 10, 11, 12, 13, 14, 15,
            16, 17, 18, 19, 20, 21, 22, 23,
            24, 25, 26, 27, 28, 29, 30, 31,
            32, 33, 34, 35, 36, 37, 38, 39,
            40, 41, 42, 43, 44, 45, 46, 47,
            48, 49, 50, 51, 52, 53, 54, 55,
            56, 57, 58, 59, 60, 61, 62, 63
        };

        private static int GetTilePixelIndex(int t, int x, int y, int width)
        {
            var tileOrder = tileOrderPs4;
            return (int) ((((tileOrder[t] / 8) + y) * width) + ((tileOrder[t] % 8) + x));
        }

        private static int GetTilePixelOffset(int t, int x, int y, int width, int bitDepth = 32)
        {
            return (GetTilePixelIndex(t, x, y, width) * (bitDepth / 8));
        }

        /// <summary>
        /// Flip texture for PS3 (Reversible)
        /// </summary>
        /// <param name="pixelData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="bitDepth"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static byte[] FlipTexturePs3(byte[] pixelData, int width, int height, int bitDepth = 32)
        {
            var lw = Math.Log(width, 2);
            if (lw != (int) lw)
            {
                throw new ArgumentException("Width must be a power of 2");
            }

            var lh = Math.Log(height, 2);
            if (lh != (int) lh)
            {
                throw new ArgumentException("Height must be a power of 2");
            }

            byte[] flipped = new byte[pixelData.Length];
            var bpp = bitDepth / 8;
            bool wide = width > height;
            int tileLength = wide ? height : width;

            //对于每个坐标点，先上下翻转，再右转90度
            Parallel.For(0, pixelData.Length / bpp, pixelIdx =>
            {
                int x = pixelIdx % width;
                int y = pixelIdx / width;
                var i = pixelIdx * bpp;
                int targetX, targetY = 0;
                if (wide)
                {
                    targetY = x % tileLength;
                    targetX = y + x / tileLength * tileLength;
                }
                else
                {
                    targetX = y % tileLength;
                    targetY = x + y / tileLength * tileLength;
                }

                var targetIdx = targetX + targetY * width;
                var targetI = targetIdx * bpp;
                pixelData.AsSpan().Slice(i, bpp).CopyTo(flipped.AsSpan(targetI, bpp));
            });

            return flipped;
        }

        public static byte[] UntileTexture(byte[] pixelData, int width, int height, int bitDepth = 32)
        {
            byte[] untiled = new byte[pixelData.Length];

            int s = 0;
            for (int y = 0; y < height; y += 8)
            {
                for (int x = 0; x < width; x += 8)
                {
                    for (int t = 0; t < (8 * 8); t++)
                    {
                        int pixelOffset = GetTilePixelOffset(t, x, y, width, bitDepth);
                        Buffer.BlockCopy(pixelData, s, untiled, pixelOffset, 4);
                        s += 4;
                    }
                }
            }

            return untiled;
        }

        public static byte[] TileTexture(byte[] pixelData, int width, int height, int bitDepth = 32)
        {
            byte[] tiled = new byte[pixelData.Length];

            int s = 0;
            for (int y = 0; y < height; y += 8)
            {
                for (int x = 0; x < width; x += 8)
                {
                    for (int t = 0; t < (8 * 8); t++)
                    {
                        int pixelOffset = GetTilePixelOffset(t, x, y, width, bitDepth);
                        Buffer.BlockCopy(pixelData, pixelOffset, tiled, s, 4);
                        s += 4;
                    }
                }
            }

            return tiled;
        }

        /// <summary>
        /// UnTile for Revolution (Wii)
        /// </summary>
        /// <param name="pixelData">The source tiled pixel data.</param>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        /// <param name="bitDepth">Image bit depth (e.g., 4 for CI4, 8 for CI8, 32 for RGBA8).</param>
        /// <returns>The untiled (linear) pixel data.</returns>
        public static byte[] UntileTextureRvl(byte[] pixelData, int width, int height, int bitDepth = 32)
        {
            // ref: Revolution SDK Graphics Library (GX)
            // Determine tile dimensions based on bit depth.
            int xTileSize = 4;
            int yTileSize = 4;
            if (bitDepth <= 8)
            {
                xTileSize = 8; // 8x4 for CI8/I8
            }
            if (bitDepth <= 4)
            {
                yTileSize = 8; // 8x8 for CI4/I4
            }

            var byteSizePerPixel = bitDepth / 8.0f;
            var scale = 1;
            bool compactMode = false;
            int bpp = bitDepth / 8;
            byte[] untiledData;

            // Handle compact formats where one byte stores multiple pixels (e.g., 4bpp).
            if (byteSizePerPixel < 1.0f)
            {
                compactMode = true;
                scale = (int) (1.0f / byteSizePerPixel); // For 4bpp, scale is 2.
                untiledData = new byte[width * height / scale];
            }
            else
            {
                untiledData = new byte[width * height * bpp];
            }

            int dataIndex = 0; // Tracks the current read position in the source tiled data.

            // Iterate through the image by tile blocks.
            for (int yt = 0; yt < height; yt += yTileSize)
            {
                for (int xt = 0; xt < width; xt += xTileSize)
                {
                    // Iterate through pixels within a tile.
                    for (int y = yt; y < yt + yTileSize; y++)
                    {
                        for (int x = xt; x < xt + xTileSize; x++)
                        {
                            if (x >= width || y >= height) continue;

                            if (!compactMode)
                            {
                                // Standard copy for formats with 1 or more bytes per pixel.
                                int pixelIndex = ((y * width) + x) * bpp;
                                if (dataIndex + bpp <= pixelData.Length && pixelIndex + bpp <= untiledData.Length)
                                {
                                    Buffer.BlockCopy(pixelData, dataIndex, untiledData, pixelIndex, bpp);
                                }
                                dataIndex += bpp;
                            }
                            else
                            {
                                // Corrected logic for compact formats (e.g., CI4).
                                // dataIndex tracks the current 4-bit pixel in the tiled source data.
                                int srcByteIndex = dataIndex / scale; // Which source byte holds our 4-bit pixel.
                                if (srcByteIndex >= pixelData.Length) continue;

                                byte sourceByte = pixelData[srcByteIndex];

                                // Extract the correct 4-bit nibble from the source byte.
                                // Even index = high bits, odd index = low bits.
                                byte pixelValue = (dataIndex % 2 == 0)
                                    ? (byte) (sourceByte >> 4)      // Get the left nibble.
                                    : (byte) (sourceByte & 0x0F);   // Get the right nibble.

                                // Calculate the destination linear pixel position.
                                int destPixelLinearIndex = (y * width) + x;
                                int destByteIndex = destPixelLinearIndex / scale; // Which byte to write to in the destination.
                                int destNibblePos = destPixelLinearIndex % 2;     // Which nibble (0 or 1) to write to.

                                if (destByteIndex >= untiledData.Length) continue;

                                // Place the 4-bit pixelValue into the correct nibble of the destination byte,
                                // preserving the other nibble that might already be there.
                                if (destNibblePos == 0)
                                {
                                    // Even linear position: write to the high bits (left nibble).
                                    untiledData[destByteIndex] = (byte) ((untiledData[destByteIndex] & 0x0F) | (pixelValue << 4));
                                }
                                else
                                {
                                    // Odd linear position: write to the low bits (right nibble).
                                    untiledData[destByteIndex] = (byte) ((untiledData[destByteIndex] & 0xF0) | pixelValue);
                                }

                                // Move to the next 4-bit pixel in the source tile stream.
                                dataIndex++;
                            }
                        }
                    }
                }
            }
            return untiledData;
        }

        /// <summary>
        /// Tile for Revolution (Wii)
        /// </summary>
        /// <param name="pixelData">The source linear pixel data.</param>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        /// <param name="bitDepth">Image bit depth (e.g., 4 for CI4, 8 for CI8, 32 for RGBA8).</param>
        /// <returns>The tiled pixel data.</returns>
        public static byte[] TileTextureRvl(byte[] pixelData, int width, int height, int bitDepth = 32)
        {
            // Determine tile dimensions based on bit depth.
            int xTileSize = 4;
            int yTileSize = 4;
            if (bitDepth <= 8)
            {
                xTileSize = 8; // 8x4 for CI8/I8
            }
            if (bitDepth <= 4)
            {
                yTileSize = 8; // 8x8 for CI4/I4
            }

            var byteSizePerPixel = bitDepth / 8.0f;
            var scale = 1;
            bool compactMode = false;
            int bpp = bitDepth / 8;
            byte[] tiledData;

            // Handle compact formats where one byte stores multiple pixels (e.g., 4bpp).
            if (byteSizePerPixel < 1.0f)
            {
                compactMode = true;
                scale = (int) (1.0f / byteSizePerPixel); // For 4bpp, scale is 2.
                tiledData = new byte[width * height / scale];
            }
            else
            {
                tiledData = new byte[width * height * bpp];
            }

            int dataIndex = 0; // Tracks the current write position in the destination tiled data.

            // Iterate through the image by tile blocks.
            for (int yt = 0; yt < height; yt += yTileSize)
            {
                for (int xt = 0; xt < width; xt += xTileSize)
                {
                    // Iterate through pixels within a tile.
                    for (int y = yt; y < yt + yTileSize; y++)
                    {
                        for (int x = xt; x < xt + xTileSize; x++)
                        {
                            if (x >= width || y >= height) continue;

                            if (!compactMode)
                            {
                                // Standard copy for formats with 1 or more bytes per pixel.
                                int pixelIndex = ((y * width) + x) * bpp;
                                if (pixelIndex + bpp <= pixelData.Length && dataIndex + bpp <= tiledData.Length)
                                {
                                    Buffer.BlockCopy(pixelData, pixelIndex, tiledData, dataIndex, bpp);
                                }
                                dataIndex += bpp;
                            }
                            else
                            {
                                // Corrected logic for tiling compact formats (e.g., CI4).
                                // 1. Find and extract the 4-bit pixel from the linear source data (pixelData).
                                int srcPixelLinearIndex = (y * width) + x;
                                int srcByteIndex = srcPixelLinearIndex / scale; // Which source byte to read from.
                                int srcNibblePos = srcPixelLinearIndex % 2;      // Which nibble (0 or 1) to read.

                                if (srcByteIndex >= pixelData.Length) continue;

                                byte sourceByte = pixelData[srcByteIndex];

                                // Extract the correct 4-bit pixelValue.
                                byte pixelValue = (srcNibblePos == 0)
                                    ? (byte) (sourceByte >> 4)      // Get the left nibble.
                                    : (byte) (sourceByte & 0x0F);   // Get the right nibble.

                                // 2. Write this 4-bit pixel to the next available position in the tiled destination.
                                int destByteIndex = dataIndex / scale; // Which destination byte to write to.
                                int destNibblePos = dataIndex % 2;     // Which nibble in that byte to write to.

                                if (destByteIndex >= tiledData.Length) continue;

                                // Place the pixelValue into the correct nibble, preserving the other nibble.
                                if (destNibblePos == 0)
                                {
                                    // Even dataIndex: write to the high bits (left nibble).
                                    tiledData[destByteIndex] = (byte) ((tiledData[destByteIndex] & 0x0F) | (pixelValue << 4));
                                }
                                else
                                {
                                    // Odd dataIndex: write to the low bits (right nibble).
                                    tiledData[destByteIndex] = (byte) ((tiledData[destByteIndex] & 0xF0) | pixelValue);
                                }

                                // Move to the next 4-bit pixel position in the destination tile stream.
                                dataIndex++;
                            }
                        }
                    }
                }
            }
            return tiledData;
        }

        /// <summary>
        /// Untile / Deswizzle texture data using a fixed 8x8 tile order (like PS4)
        /// </summary>
        /// <param name="pixelData">Input swizzled pixel data</param>
        /// <param name="width">Image width in units (e.g., pixels, or 4x4 blocks for BC7)</param>
        /// <param name="height">Image height in units</param>
        /// <param name="bytesPerUnit">Number of bytes per unit (e.g., bytes per pixel, or bytes per 4x4 BC7 block)</param>
        /// <returns>Untiled / Deswizzled pixel data</returns>
        public static byte[] UntileTextureV2(byte[] pixelData, int width, int height, int bytesPerUnit)
        {
            byte[] untiled = new byte[pixelData.Length];

            int s = 0;
            for (int y = 0; y < height; y += 8)
            {
                for (int x = 0; x < width; x += 8)
                {
                    for (int t = 0; t < (8 * 8); t++)
                    {
                        // Calculate the target pixel offset in the untiled output array
                        // The tileOrderPs4 provides the relative (x, y) coordinates within the 8x8 block
                        // for the 't'th unit from the swizzled input stream.
                        int pixelOffset = GetTilePixelOffset(t, x, y, width, bytesPerUnit * 8); // Pass bitDepth, so bytesPerUnit * 8

                        // Perform the copy
                        // Ensure we don't read or write out of bounds
                        if (s + bytesPerUnit > pixelData.Length)
                        {
                            throw new ArgumentOutOfRangeException(nameof(pixelData), "Input pixelData is too short for the specified dimensions and bytesPerUnit.");
                        }

                        if (pixelOffset + bytesPerUnit > untiled.Length)
                        {
                            throw new ArgumentOutOfRangeException(nameof(untiled), "Calculated pixelOffset is out of bounds for the output untiled array.");
                        }

                        Buffer.BlockCopy(pixelData, s, untiled, pixelOffset, bytesPerUnit);
                        s += bytesPerUnit;
                    }
                }
            }

            return untiled;
        }

        /// <summary>
        /// Tile / Swizzle texture data using a fixed 8x8 tile order (like PS4)
        /// </summary>
        /// <param name="pixelData">Input linear pixel data</param>
        /// <param name="width">Image width in units (e.g., pixels, or 4x4 blocks for BC7)</param>
        /// <param name="height">Image height in units</param>
        /// <param name="bytesPerUnit">Number of bytes per unit (e.g., bytes per pixel, or bytes per 4x4 BC7 block)</param>
        /// <returns>Tiled / Swizzled pixel data</returns>
        public static byte[] TileTextureV2(byte[] pixelData, int width, int height, int bytesPerUnit)
        {
            byte[] tiled = new byte[pixelData.Length];

            int s = 0;
            for (int y = 0; y < height; y += 8)
            {
                for (int x = 0; x < width; x += 8)
                {
                    for (int t = 0; t < (8 * 8); t++)
                    {
                        // Calculate the source pixel offset in the linear input array
                        // The tileOrderPs4 provides the relative (x, y) coordinates within the 8x8 block
                        // for the 't'th unit to be written to the swizzled output stream.
                        int pixelOffset = GetTilePixelOffset(t, x, y, width, bytesPerUnit * 8); // Pass bitDepth, so bytesPerUnit * 8

                        // Perform the copy
                        // Ensure we don't read or write out of bounds
                        if (pixelOffset + bytesPerUnit > pixelData.Length)
                        {
                            throw new ArgumentOutOfRangeException(nameof(pixelData), "Input pixelData is too short for the specified dimensions and bytesPerUnit.");
                        }

                        if (s + bytesPerUnit > tiled.Length)
                        {
                            throw new ArgumentOutOfRangeException(nameof(tiled), "Calculated output position is out of bounds for the tiled array.");
                        }

                        Buffer.BlockCopy(pixelData, pixelOffset, tiled, s, bytesPerUnit);
                        s += bytesPerUnit;
                    }
                }
            }

            return tiled;
        }
        #endregion

        #region Unswizzle (Morton)

        private static int Compact1By1(int x)
        {
            x &= 0x55555555; // x = -f-e -d-c -b-a -9-8 -7-6 -5-4 -3-2 -1-0
            x = (x ^ (x >> 1)) & 0x33333333; // x = --fe --dc --ba --98 --76 --54 --32 --10
            x = (x ^ (x >> 2)) & 0x0f0f0f0f; // x = ---- fedc ---- ba98 ---- 7654 ---- 3210
            x = (x ^ (x >> 4)) & 0x00ff00ff; // x = ---- ---- fedc ba98 ---- ---- 7654 3210
            x = (x ^ (x >> 8)) & 0x0000ffff; // x = ---- ---- ---- ---- fedc ba98 7654 3210
            return x;
        }

        private static int Part1By1(int x)
        {
            x &= 0x0000ffff; // x = ---- ---- ---- ---- fedc ba98 7654 3210
            x = (x ^ (x << 8)) & 0x00ff00ff; // x = ---- ---- fedc ba98 ---- ---- 7654 3210
            x = (x ^ (x << 4)) & 0x0f0f0f0f; // x = ---- fedc ---- ba98 ---- 7654 ---- 3210
            x = (x ^ (x << 2)) & 0x33333333; // x = --fe --dc --ba --98 --76 --54 --32 --10
            x = (x ^ (x << 1)) & 0x55555555; // x = -f-e -d-c -b-a -9-8 -7-6 -5-4 -3-2 -1-0
            return x;
        }

        private static int EncodeMorton2(int x, int y)
        {
            return (Part1By1(y) << 1) + Part1By1(x);
        }

        private static int DecodeMorton2X(int code)
        {
            return Compact1By1(code >> 0);
        }

        private static int DecodeMorton2Y(int code)
        {
            return Compact1By1(code >> 1);
        }

        public static void SwitchPixel(this Bitmap bmp, int x1, int y1, int x2, int y2)
        {
            var c1 = bmp.GetPixel(x1, y1);
            var c2 = bmp.GetPixel(x2, y2);
            bmp.SetPixel(x2, y2, c1);
            bmp.SetPixel(x1, y1, c2);
        }

        /// <summary>
        /// Unswizzle, this is much slower than <seealso cref="SwizzleTexture"/>
        /// </summary>
        /// <param name="bmp"></param>
        public static void Swizzle(this Bitmap bmp)
        {
            var width = bmp.Width;
            var height = bmp.Height;

            int min = width < height ? width : height;
            int k = (int) Math.Log(min, 2);

            for (int i = 0; i < width * height; i++)
            {
                int x, y;
                if (height < width)
                {
                    // XXXyxyxyx → XXXxxxyyy
                    int j = i >> (2 * k) << (2 * k)
                            | (DecodeMorton2Y(i) & (min - 1)) << k
                            | (DecodeMorton2X(i) & (min - 1)) << 0;
                    x = j / height;
                    y = j % height;
                }
                else
                {
                    // YYYyxyxyx → YYYyyyxxx
                    int j = i >> (2 * k) << (2 * k)
                            | (DecodeMorton2X(i) & (min - 1)) << k
                            | (DecodeMorton2Y(i) & (min - 1)) << 0;
                    x = j % width;
                    y = j / width;
                }

                if (y >= height || x >= width) continue;

                var oriX = i % width;
                var oriY = i / width;

                bmp.SwitchPixel(oriX, oriY, x, y);
            }
        }

        public static byte[] UnswizzleTexture(byte[] pixelData, int width, int height, int bitDepth = 32,
            SwizzleType swizzle = SwizzleType.PSV)
        {
            switch (swizzle)
            {
                case SwizzleType.PSV:
                    return UnswizzleTexturePSV(pixelData, width, height, bitDepth);
                case SwizzleType.PSP:
                    return UnswizzleTexturePSP(pixelData, width, height, bitDepth);
                default:
                    return pixelData;
            }
        }


        public static byte[] UnswizzleTexturePSV(byte[] pixelData, int width, int height, int bitDepth = 32)
        {
            bool compactMode = false;
            var scale = 1;
            var pixelFormatSize = bitDepth;
            int unswizzledShouldBeSize;
            int bytesPerPixel = pixelFormatSize / 8;
            if (bytesPerPixel == 0) //less than 8
            {
                compactMode = true;
                scale = 8 / pixelFormatSize;
                if (scale != 2)
                {
                    throw new NotSupportedException("BytesPerPixel must >= 0.5");
                }

                unswizzledShouldBeSize = width * height / scale;
            }
            else
            {
                unswizzledShouldBeSize = (pixelFormatSize / 8) * width * height;
            }

            byte[] unswizzled = new byte[Math.Max(pixelData.Length, unswizzledShouldBeSize)];
            int min = width < height ? width : height;
            int k = (int) Math.Log(min, 2);

            for (int i = 0; i < width * height; i++)
            {
                int x, y;
                if (height < width)
                {
                    // XXXyxyxyx → XXXxxxyyy
                    int j = i >> (2 * k) << (2 * k)
                            | (DecodeMorton2Y(i) & (min - 1)) << k
                            | (DecodeMorton2X(i) & (min - 1)) << 0;
                    x = j / height;
                    y = j % height;
                }
                else
                {
                    // YYYyxyxyx → YYYyyyxxx
                    int j = i >> (2 * k) << (2 * k)
                            | (DecodeMorton2X(i) & (min - 1)) << k
                            | (DecodeMorton2Y(i) & (min - 1)) << 0;
                    x = j % width;
                    y = j / width;
                }

                if (y >= height || x >= width) continue;

                /*
                   0    1    2
                   +---------+
                   |    |**  |
                   |    |    |
                   +----+----+
                    1234 1234
                    0 1  2 3             
                 */

                var dstIndex = (y * width) + x;
                if (compactMode)
                {
                    var srcPosition = i / scale;
                    var srcSubIndex = i % scale;
                    if (srcPosition >= pixelData.Length)
                    {
                        continue;
                    }

                    var srcData = srcSubIndex == 0 ? pixelData[srcPosition] & 0x0F : ((pixelData[srcPosition] & 0xF0) >> pixelFormatSize);
                    var dstPosition = dstIndex / scale;
                    var dstSubIndex = dstIndex % scale;
                    // 这里是大端，dstSubIndex = 0, 则设置大端前2字节
                    unswizzled[dstPosition] = dstSubIndex == 1
                        ? (byte) ((unswizzled[dstPosition] & 0xF0) | srcData)
                        : (byte) ((unswizzled[dstPosition] & 0x0F) | (srcData << pixelFormatSize));
                }
                else
                {
                    var startCopyPosition = i * bytesPerPixel;
                    if (startCopyPosition >= pixelData.Length)
                    {
                        continue;
                    }

                    if (bytesPerPixel <= 1)
                    {
                        Buffer.BlockCopy(pixelData, startCopyPosition, unswizzled, dstIndex * bytesPerPixel,
                            bytesPerPixel);
                    }
                    else
                    {
                        var endCopyPosition = startCopyPosition + bytesPerPixel;
                        var actualCopySize = bytesPerPixel;
                        if (endCopyPosition >= pixelData.Length)
                        {
                            actualCopySize = pixelData.Length - startCopyPosition;
                        }

                        Buffer.BlockCopy(pixelData, startCopyPosition, unswizzled, dstIndex * bytesPerPixel,
                            actualCopySize);
                    }
                }
            }

            return unswizzled;
        }

        public static byte[] SwizzleTexture(byte[] pixelData, int width, int height, int bitDepth = 32,
            SwizzleType swizzle = SwizzleType.PSV)
        {
            switch (swizzle)
            {
                case SwizzleType.PSV:
                    return SwizzleTexturePSV(pixelData, width, height, bitDepth);
                case SwizzleType.PSP:
                    return SwizzleTexturePSP(pixelData, width, height, bitDepth);
                default:
                    return pixelData;
            }
        }

        public static byte[] SwizzleTexturePSP(byte[] pixelData, int width, int height, int bitDepth = 32)
        {
            var scale = 1;
            bool compactMode = false;
            int bytesPerPixel = bitDepth / 8;
            if (bytesPerPixel == 0) //less than 8
            {
                compactMode = true;
                scale = 8 / bitDepth;
                if (scale != 2)
                {
                    throw new NotSupportedException("BytesPerPixel must >= 0.5");
                }
            }

            byte[] swizzled = new byte[pixelData.Length];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    GetPixelCoordinatesPSP(x, y, width, height, bitDepth, out var nx, out var ny);
                    var srcIndex = nx + (width * ny);
                    var dstIndex = x + (width * y);
                    if (compactMode)
                    {
                        var srcPosition = srcIndex / scale;
                        var srcSubIndex = srcIndex % scale;
                        if (srcPosition >= pixelData.Length)
                        {
                            continue;
                        }

                        var srcData = srcSubIndex == 1
                            ? pixelData[srcPosition] & 0x0F
                            : ((pixelData[srcPosition] & 0xF0) >> bitDepth);

                        var dstPosition = dstIndex / scale;
                        var dstSubIndex = dstIndex % scale;
                        swizzled[dstPosition] = dstSubIndex == 0
                            ? (byte) ((swizzled[dstPosition] & 0xF0) | srcData)
                            : (byte) ((swizzled[dstPosition] & 0x0F) | (srcData << bitDepth));
                    }
                    else
                    {
                        Buffer.BlockCopy(pixelData, srcIndex * bytesPerPixel, swizzled, dstIndex * bytesPerPixel,
                            bytesPerPixel);
                    }
                }
            }

            return swizzled;
        }

        public static byte[] SwizzleTexturePSV(byte[] pixelData, int width, int height, int bitDepth = 32)
        {
            var scale = 1;
            var pixelFormatSize = bitDepth;
            bool compactMode = false;
            int bytesPerPixel = pixelFormatSize / 8;
            if (bytesPerPixel == 0) //less than 8
            {
                compactMode = true;
                scale = 8 / pixelFormatSize;
                if (scale != 2)
                {
                    throw new NotSupportedException("BytesPerPixel must >= 0.5");
                }
            }

            byte[] swizzled = new byte[pixelData.Length];
            int min = width < height ? width : height;
            int k = (int) Math.Log(min, 2);

            for (int i = 0; i < width * height; i++)
            {
                int x, y;
                if (height < width)
                {
                    // XXXyxyxyx → XXXxxxyyy
                    int j = i >> (2 * k) << (2 * k)
                            | (DecodeMorton2Y(i) & (min - 1)) << k
                            | (DecodeMorton2X(i) & (min - 1)) << 0;
                    x = j / height;
                    y = j % height;
                }
                else
                {
                    // YYYyxyxyx → YYYyyyxxx
                    int j = i >> (2 * k) << (2 * k)
                            | (DecodeMorton2X(i) & (min - 1)) << k
                            | (DecodeMorton2Y(i) & (min - 1)) << 0;
                    x = j % width;
                    y = j / width;
                }

                if (y >= height || x >= width) continue;

                if (compactMode)
                {
                    var srcIndex = (y * width) + x;
                    var srcPosition = srcIndex / scale;
                    var srcSubIndex = srcIndex % scale;
                    var srcData = srcSubIndex == 1 ? pixelData[srcPosition] & 0x0F : ((pixelData[srcPosition] & 0xF0) >> pixelFormatSize);
                    var dstPosition = i / scale;
                    var dstSubIndex = i % scale;
                    //Inverse the order comparing to Unswizzle
                    swizzled[dstPosition] = dstSubIndex == 0
                        ? (byte) ((swizzled[dstPosition] & 0xF0) | srcData)
                        : (byte) ((swizzled[dstPosition] & 0x0F) | (srcData << pixelFormatSize));
                }
                else
                {
                    Buffer.BlockCopy(pixelData, ((y * width) + x) * bytesPerPixel, swizzled, i * bytesPerPixel,
                        bytesPerPixel);
                }
            }

            return swizzled;
        }

        public static byte[] UnswizzleTexturePSP(byte[] pixelData, int width, int height, int bitDepth = 32)
        {
            bool compactMode = false;
            var scale = 1;
            int unswizzledShouldBeSize;
            int bytesPerPixel = bitDepth / 8;
            if (bitDepth < 8)
            {
                compactMode = true;
                scale = 8 / bitDepth;
                unswizzledShouldBeSize = width * height / scale;
            }
            else
            {
                unswizzledShouldBeSize = (bitDepth / 8) * width * height;
            }

            byte[] unswizzled = new byte[Math.Max(pixelData.Length, unswizzledShouldBeSize)];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    GetPixelCoordinatesPSP(x, y, width, height, bitDepth, out var nx, out var ny);
                    var srcIndex = x + (width * y);
                    var dstIndex = nx + (width * ny);
                    if (compactMode)
                    {
                        var srcPosition = srcIndex / scale;
                        var srcSubIndex = srcIndex % scale;
                        if (srcPosition >= pixelData.Length)
                        {
                            continue;
                        }

                        var srcData = srcSubIndex == 0
                            ? pixelData[srcPosition] & 0x0F
                            : ((pixelData[srcPosition] & 0xF0) >> bitDepth);
                        var dstPosition = dstIndex / scale;
                        var dstSubIndex = dstIndex % scale;
                        unswizzled[dstPosition] = dstSubIndex == 1
                            ? (byte) ((unswizzled[dstPosition] & 0xF0) | srcData)
                            : (byte) ((unswizzled[dstPosition] & 0x0F) | (srcData << bitDepth));
                    }
                    else
                    {
                        var startCopyPosition = srcIndex * bytesPerPixel;
                        if (startCopyPosition >= pixelData.Length)
                        {
                            continue;
                        }

                        if (bytesPerPixel <= 1)
                        {
                            Buffer.BlockCopy(pixelData, startCopyPosition, unswizzled, dstIndex * bytesPerPixel,
                                bytesPerPixel);
                        }
                        else
                        {
                            var endCopyPosition = startCopyPosition + bytesPerPixel;
                            var actualCopySize = bytesPerPixel;
                            if (endCopyPosition >= pixelData.Length)
                            {
                                actualCopySize = pixelData.Length - startCopyPosition;
                            }

                            Buffer.BlockCopy(pixelData, startCopyPosition, unswizzled, dstIndex * bytesPerPixel,
                                actualCopySize);
                        }
                    }
                }
            }

            return unswizzled;
        }

        private static void GetPixelCoordinatesPSP(int origX, int origY, int width, int height, int bitDepth,
            out int transformedX, out int transformedY)
        {
            var bitsPerPixel = bitDepth;
            int tileWidth = (bitsPerPixel < 8 ? 32 : (16 / (bitsPerPixel / 8)));
            GetPixelCoordinatesTiledEx(origX, origY, width, height, out transformedX, out transformedY, tileWidth, 8, null);
        }

        private static void GetPixelCoordinatesTiledEx(int origX, int origY, int width, int height, out int transformedX,
            out int transformedY, int tileWidth, int tileHeight, int[] pixelOrdering)
        {
            // TODO: sometimes eats the last few blocks(?) in the image (ex. BC7 GNFs)

            // Sanity checks
            if (width == 0) width = tileWidth;
            if (height == 0) height = tileHeight;

            // Calculate coords in image
            int tileSize = (tileWidth * tileHeight);
            int globalPixel = ((origY * width) + origX);
            int globalX = ((globalPixel / tileSize) * tileWidth);
            int globalY = ((globalX / width) * tileHeight);
            globalX %= width;

            // Calculate coords in tile
            int inTileX = (globalPixel % tileWidth);
            int inTileY = ((globalPixel / tileWidth) % tileHeight);
            int inTilePixel = ((inTileY * tileHeight) + inTileX);

            // If applicable, transform by ordering table
            if (pixelOrdering != null && tileSize <= pixelOrdering.Length)
            {
                inTileX = (pixelOrdering[inTilePixel] % 8);
                inTileY = (pixelOrdering[inTilePixel] / 8);
            }

            // Set final image coords
            transformedX = (globalX + inTileX);
            transformedY = (globalY + inTileY);
        }

        #endregion
    }
}