//DXT Decoding/Encoding Class by gdkchan
//https://github.com/gdkchan/CEGTool/blob/master/CEGTool/DXTCodec.cs
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FreeMote
{
    internal static partial class DxtUtil
    {
        private enum DxtAlphaMode
        {
            None,
            DXT3,
            DXT5
        }

        /// <summary>
        /// Decode a texture from DXT1 compression.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static Bitmap Dxt1Decode(byte[] data, int width, int height)
        {
            byte[] Out = new byte[(width * height * 4)];
            int offset = 0;

            for (int y = 0; y < height / 4; y++)
            {
                for (int x = 0; x < width / 4; x++)
                {
                    byte[] block = new byte[8];
                    Buffer.BlockCopy(data, offset, block, 0, 8);
                    offset += 8;
                    block = DxtDecodeBlock(block, true);

                    for (int ty = 0; ty < 4; ty++)
                    {
                        for (int tx = 0; tx < 4; tx++)
                        {
                            int outOffset = (x * 4 + tx + ((y * 4 + ty) * width)) * 4;
                            int blockOffset = (tx + (ty * 4)) * 4;
                            Buffer.BlockCopy(block, blockOffset, Out, outOffset, 3);
                            Out[outOffset + 3] = 0xff;
                        }
                    }
                }
            }

            return GetBitmapFromArray(Out, width, height);
        }

        /// <summary>
        /// Decode a texture from DXT3 compression.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static Bitmap Dxt3Decode(byte[] data, int width, int height)
        {
            byte[] Out = new byte[(width * height * 4)];
            int offset = 0;

            for (int y = 0; y < height / 4; y++)
            {
                for (int x = 0; x < width / 4; x++)
                {
                    byte[] block = new byte[8];
                    Buffer.BlockCopy(data, offset + 8, block, 0, 8);
                    block = DxtDecodeBlock(block, false);
                    ulong alpha = BitConverter.ToUInt64(data, offset);
                    offset += 16;

                    for (int ty = 0; ty < 4; ty++)
                    {
                        for (int tx = 0; tx < 4; tx++)
                        {
                            int outOffset = (x * 4 + tx + ((y * 4 + ty) * width)) * 4;
                            int blockOffset = (tx + (ty * 4)) * 4;
                            Buffer.BlockCopy(block, blockOffset, Out, outOffset, 3);
                            ulong alphaVal = (alpha >> ((ty * 16) + (tx * 4))) & 0xF;
                            Out[outOffset + 3] = (byte)((alphaVal << 4) | alphaVal);
                        }
                    }
                }
            }

            return GetBitmapFromArray(Out, width, height);
        }

        /// <summary>
        /// Decode a texture from DXT5 compression.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static Bitmap Dxt5Decode(byte[] data, int width, int height)
        {
            byte[] Out = new byte[(width * height * 4)];
            int offset = 0;

            for (int y = 0; y < height / 4; y++)
            {
                for (int x = 0; x < width / 4; x++)
                {
                    byte[] block = new byte[8];
                    Buffer.BlockCopy(data, offset + 8, block, 0, 8);
                    block = DxtDecodeBlock(block, true);
                    byte[] alpha = new byte[8];
                    alpha[0] = data[offset];
                    alpha[1] = data[offset + 1];
                    Dxt5CalculateAlphas(alpha);

                    ulong alphaVal = BitConverter.ToUInt64(data, offset + 2) & 0xffffffffffff;
                    offset += 16;

                    for (int ty = 0; ty < 4; ty++)
                    {
                        for (int tx = 0; tx < 4; tx++)
                        {
                            int outOffset = (x * 4 + tx + ((y * 4 + ty) * width)) * 4;
                            int blockOffset = (tx + (ty * 4)) * 4;
                            Buffer.BlockCopy(block, blockOffset, Out, outOffset, 3);
                            Out[outOffset + 3] = alpha[(alphaVal >> ((ty * 12) + (tx * 3))) & 7];
                        }
                    }
                }
            }

            return GetBitmapFromArray(Out, width, height);
        }

        /// <summary>
        /// Encode a bitmap to DXT1 texture.
        /// </summary>
        /// <param name="img"></param>
        /// <returns></returns>
        public static byte[] Dxt1Encode(Bitmap img)
        {
            return DxtEncode(img, true, DxtAlphaMode.None);
        }
        public static byte[] Dxt1Encode(byte[] data, int width, int height)
        {
            return DxtEncode(data, width, height, DxtAlphaMode.None);
        }

        /// <summary>
        /// Encode a bitmap to DXT3 texture.
        /// </summary>
        /// <param name="img"></param>
        /// <returns></returns>
        public static byte[] Dxt3Encode(Bitmap img)
        {
            return DxtEncode(img, false, DxtAlphaMode.DXT3);
        }

        public static byte[] Dxt3Encode(byte[] data, int width, int height)
        {
            return DxtEncode(data, width, height, DxtAlphaMode.DXT3);
        }

        /// <summary>
        /// Encode a bitmap to DXT5 texture.
        /// </summary>
        /// <param name="img"></param>
        /// <returns></returns>
        public static byte[] Dxt5Encode(Bitmap img)
        {
            return DxtEncode(img, true, DxtAlphaMode.DXT5);
        }

        public static byte[] Dxt5Encode(byte[] data, int width, int height)
        {
            return DxtEncode(data, width, height);
        }

        private static byte[] DxtEncode(Bitmap img, bool dxt1, DxtAlphaMode dxtAlpha)
        {
            int width = img.Width;
            int height = img.Height;
            PixelFormat pixelFormat = img.PixelFormat;
            BitmapData imgData = img.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, pixelFormat);
            byte[] data = new byte[imgData.Height * imgData.Stride];
            Marshal.Copy(imgData.Scan0, data, 0, data.Length);
            img.UnlockBits(imgData);

            return DxtEncode(data, width, height, dxtAlpha, pixelFormat);
        }

        private static byte[] DxtEncode(byte[] data, int width, int height, DxtAlphaMode dxtAlpha = DxtAlphaMode.DXT5, PixelFormat pixelFormat = PixelFormat.Format32bppArgb)
        {
            int bpp = 3;
            if (pixelFormat == PixelFormat.Format32bppArgb) bpp = 4;

            byte[] Out = new byte[((width / 4 * (height / 4)) * (dxtAlpha != DxtAlphaMode.None ? 16 : 8))];
            int outOffset = 0;

            for (int tileY = 0; tileY < height / 4; tileY++)
            {
                for (int tileX = 0; tileX < width / 4; tileX++)
                {
                    byte[] block = new byte[8];

                    //Calculo de Luminância mín/máx
                    byte minLuma = 0xff;
                    byte maxLuma = 0;

                    byte minR = 0, maxR = 0;
                    byte minG = 0, maxG = 0;
                    byte minB = 0, maxB = 0;

                    for (int y = 0; y < 4; y++)
                    {
                        for (int x = 0; x < 4; x++)
                        {
                            int offset = ((tileX * 4) + x + (((tileY * 4) + y) * width)) * bpp;

                            byte r = data[offset + 2];
                            byte g = data[offset + 1];
                            byte b = data[offset];

                            byte luma = (byte) (0.257f * r + 0.504f * g + 0.098f * b + 16);
                            if (luma < minLuma)
                            {
                                minR = r;
                                minG = g;
                                minB = b;

                                minLuma = luma;
                            }
                            if (luma > maxLuma)
                            {
                                maxR = r;
                                maxG = g;
                                maxB = b;

                                maxLuma = luma;
                            }
                        }
                    }

                    block[2] = (byte) ((minB >> 3) | ((minG & 0x1C) << 3));
                    block[3] = (byte) (((minG & 0xE0) >> 5) | (minR & 0xF8));
                    block[0] = (byte) ((maxB >> 3) | ((maxG & 0x1C) << 3));
                    block[1] = (byte) (((maxG & 0xE0) >> 5) | (maxR & 0xF8));

                    Color[] pixelColor = new Color[4];
                    ushort c0 = Read16(block, 0);
                    ushort c1 = Read16(block, 2);
                    pixelColor[0] = GetColorFromBgr565(c0);
                    pixelColor[1] = GetColorFromBgr565(c1);
                    pixelColor[2] = ProcessDxt1ColorChannel(2, c0, c1, true);
                    pixelColor[3] = ProcessDxt1ColorChannel(3, c0, c1, true);

                    for (int y = 0; y < 4; y++)
                    {
                        for (int x = 0; x < 4; x++)
                        {
                            int imageOffset = ((tileX * 4) + x + (((tileY * 4) + y) * width)) * bpp;

                            byte r = data[imageOffset + 2];
                            byte g = data[imageOffset + 1];
                            byte b = data[imageOffset];

                            int luma = (int) (0.257f * r + 0.504f * g + 0.098f * b + 16);

                            int minDiff = 0xff;
                            int index = 0;
                            for (int i = 0; i < 4; i++)
                            {
                                int testLuma = (int) (0.257f * pixelColor[i].R + 0.504f * pixelColor[i].G +
                                                      0.098f * pixelColor[i].B + 16);
                                int diff = Math.Abs(testLuma - luma);
                                if (diff < minDiff)
                                {
                                    minDiff = diff;
                                    index = i;
                                }
                            }

                            block[4 + y] |= (byte) (index << (x * 2));
                        }
                    }

                    byte[] alphaBlock = new byte[8];
                    if (dxtAlpha == DxtAlphaMode.DXT3)
                    {
                        for (int y = 0; y < 4; y++)
                        {
                            for (int x = 0; x < 4; x++)
                            {
                                int imageOffset = ((tileX * 4) + x + (((tileY * 4) + y) * width)) * bpp;

                                byte a = (byte) (data[imageOffset + 3] >> 4);

                                alphaBlock[(y * 2) + (x / 2)] |= (byte) (a << ((x % 2) * 4));
                            }
                        }

                        Buffer.BlockCopy(alphaBlock, 0, Out, outOffset, alphaBlock.Length);
                        outOffset += alphaBlock.Length;
                    }
                    else if (dxtAlpha == DxtAlphaMode.DXT5)
                    {
                        byte minAlpha = 0xff;
                        byte maxAlpha = 0;

                        for (int y = 0; y < 4; y++)
                        {
                            for (int x = 0; x < 4; x++)
                            {
                                int offset = ((tileX * 4) + x + (((tileY * 4) + y) * width)) * bpp;

                                byte a = data[offset + 3];

                                if (a < minAlpha) minAlpha = a;
                                if (a > maxAlpha) maxAlpha = a;
                            }
                        }

                        byte[] alpha = new byte[8];
                        alpha[0] = minAlpha;
                        alpha[1] = maxAlpha;
                        alphaBlock[0] = alpha[0];
                        alphaBlock[1] = alpha[1];
                        Dxt5CalculateAlphas(alpha);

                        uint alphaVal = 0;
                        byte shift = 0;
                        int alphaBlockOffset = 2;
                        for (int y = 0; y < 4; y++)
                        {
                            for (int x = 0; x < 4; x++)
                            {
                                int imageOffset = ((tileX * 4) + x + (((tileY * 4) + y) * width)) * bpp;

                                byte a = data[imageOffset + 3];

                                int minDiff = 0xff;
                                byte index = 0;
                                for (int i = 0; i < 8; i++)
                                {
                                    int diff = Math.Abs(alpha[i] - a);
                                    if (diff < minDiff)
                                    {
                                        minDiff = diff;
                                        index = (byte) i;
                                    }
                                }

                                alphaVal |= (uint) ((index & 7) << shift);
                                shift += 3;
                                if (shift == 24)
                                {
                                    Buffer.BlockCopy(BitConverter.GetBytes(alphaVal), 0, alphaBlock, alphaBlockOffset, 3);
                                    alphaVal = 0;
                                    shift = 0;
                                    alphaBlockOffset += 3;
                                }
                            }
                        }

                        Buffer.BlockCopy(alphaBlock, 0, Out, outOffset, alphaBlock.Length);
                        outOffset += alphaBlock.Length;
                    }

                    Buffer.BlockCopy(block, 0, Out, outOffset, block.Length);
                    outOffset += block.Length;
                }
            }
            return Out;
        }

        private static void Dxt5CalculateAlphas(byte[] alpha)
        {
            for (int i = 2; i < 8; i++)
            {
                if (alpha[0] > alpha[1])
                {
                    alpha[i] = (byte)(((8 - i) * alpha[0] + (i - 1) * alpha[1]) / 7);
                }
                else
                {
                    if (i < 6)
                    {
                        alpha[i] = (byte)(((6 - i) * alpha[0] + (i - 1) * alpha[1]) / 7);
                    }
                    else
                    {
                        if (i == 6) alpha[i] = 0;
                        else if (i == 7) alpha[i] = 0xff;
                    }
                }
            }
        }

        private static byte[] DxtDecodeBlock(byte[] data, bool dxt1)
        {
            Color[] pixelColor = new Color[4];
            ushort c0 = Read16(data, 0);
            ushort c1 = Read16(data, 2);
            pixelColor[0] = GetColorFromBgr565(c0);
            pixelColor[1] = GetColorFromBgr565(c1);
            pixelColor[2] = ProcessDxt1ColorChannel(2, c0, c1, dxt1);
            pixelColor[3] = ProcessDxt1ColorChannel(3, c0, c1, dxt1);

            byte[] Out = new byte[(4 * 4 * 4)];

            int index = 3;
            int indexShift = 0;
            uint indexBits = Read32(data, 4);
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    long colorIndex = (indexBits & index) >> indexShift;
                    Color pixel = pixelColor[colorIndex];
                    index <<= 2;
                    indexShift += 2;

                    Out[(y * 4 + x) * 4] = pixel.B;
                    Out[((y * 4 + x) * 4) + 1] = pixel.G;
                    Out[((y * 4 + x) * 4) + 2] = pixel.R;
                }
            }

            return Out;
        }

        private static Color ProcessDxt1ColorChannel(byte cX, ushort c0, ushort c1, bool dxt1)
        {
            Color Out = new Color();
            Color color0 = GetColorFromBgr565(c0);
            Color color1 = GetColorFromBgr565(c1);

            if (c0 > c1 || !dxt1)
            {
                if (cX == 2)
                    Out = Color.FromArgb((2 * color0.R + color1.R) / 3, (2 * color0.G + color1.G) / 3, (2 * color0.B + color1.B) / 3);
                else
                    Out = Color.FromArgb((2 * color1.R + color0.R) / 3, (2 * color1.G + color0.G) / 3, (2 * color1.B + color0.B) / 3);
            }
            else
            {
                if (cX == 2)
                    Out = Color.FromArgb((color0.R + color1.R) / 2, (color0.G + color1.G) / 2, (color0.B + color1.B) / 2);
                else
                    Out = Color.Black;
            }

            return Out;
        }

        private static Bitmap GetBitmapFromArray(byte[] array, int width, int height)
        {
            Bitmap img = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData imgData = img.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(array, 0, imgData.Scan0, array.Length);
            img.UnlockBits(imgData);
            return img;
        }

        private static Color GetColorFromBgr565(ushort value)
        {
            byte r = (byte)((value >> 11) & 0x1f);
            byte g = (byte)((value >> 5) & 0x3f);
            byte b = (byte)((value) & 0x1f);

            r = (byte)((r << 3) | (r >> 2));
            g = (byte)((g << 2) | (g >> 4));
            b = (byte)((b << 3) | (b >> 2));

            return Color.FromArgb(r, g, b);
        }

        private static uint Read32(byte[] data, int address)
        {
            return BitConverter.ToUInt32(data, address);
        }

        private static ushort Read16(byte[] data, int address)
        {
            return BitConverter.ToUInt16(data, address);
        }

    }
}
