//! \file       ImageTLG.cs
//! \date       Thu Jul 17 21:31:39 2014
//! \brief      KiriKiri TLG image implementation.
//---------------------------------------------------------------------------
// TLG5/6 decoder
//	Copyright (C) 2000-2005  W.Dee <dee@kikyou.info> and contributors
//
// C# port by morkt (https://github.com/morkt/GARbro) LICENSE: MIT
// Modified by Ulysses (wdwxy12345@gmail.com)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace FreeMote
{
    internal static class GameResExtension
    {
        public static bool AsciiEqual(this byte[] array, int offset, string str)
        {
            var strArr = Encoding.ASCII.GetBytes(str);
            if (array.Length < strArr.Length + offset)
            {
                return false;
            }
            return array.Skip(offset).Take(strArr.Length).SequenceEqual(strArr);
        }

        public static bool AsciiEqual(this byte[] array, string str)
        {
            var strArr = Encoding.ASCII.GetBytes(str);
            if (array.Length < strArr.Length)
            {
                return false;
            }
            return array.Take(strArr.Length).SequenceEqual(strArr);
        }

        public static uint ToUInt32(this byte[] array, int offset)
        {
            return BitConverter.ToUInt32(array, offset);
        }

    }

    public class TlgMetaData
    {
        public int Bpp;
        public int Version;
        public int DataOffset;
        public uint Width;
        public uint Height;
        public string FileName;
        public int OffsetX;
        public int OffsetY;

    }

    public class TlgImageConverter
    {
        public string Tag { get { return "TLG"; } }
        public string Description { get { return "KiriKiri game engine image format"; } }
        public uint Signature { get { return 0x30474c54; } } // "TLG0"

        public TlgImageConverter()
        {
            Extensions = new string[] { "tlg", "tlg5", "tlg6" };
            Signatures = new uint[] { 0x30474c54, 0x35474c54, 0x36474c54, 0x35474cAB };
        }

        public uint[] Signatures { get; set; }

        public string[] Extensions { get; set; }

        internal TlgMetaData ReadMetaData (BinaryReader br)
        {
            br.BaseStream.Seek(0, SeekOrigin.Begin);
            var header = br.ReadBytes(38);
            int offset = 0xf;
            if (!header.AsciiEqual ("TLG0.0\x00sds\x1a"))
                offset = 0;
            int version;
            if (!header.AsciiEqual (offset+6, "\x00raw\x1a"))
                return null;
            if (0xAB == header[offset])
                header[offset] = (byte)'T';
            if (header.AsciiEqual (offset, "TLG6.0"))
                version = 6;
            else if (header.AsciiEqual (offset, "TLG5.0"))
                version = 5;
            else if (header.AsciiEqual (offset, "XXXYYY"))
            {
                version = 5;
                header[offset+0x0C] ^= 0xAB;
                header[offset+0x10] ^= 0xAC;
            }
            else if (header.AsciiEqual (offset, "XXXZZZ"))
            {
                version = 6;
                header[offset+0x0F] ^= 0xAB;
                header[offset+0x13] ^= 0xAC;
            }
            else
                return null;
            int colors = header[offset+11];
            if (6 == version)
            {
                if (1 != colors && 4 != colors && 3 != colors)
                    return null;
                if (header[offset+12] != 0 || header[offset+13] != 0 || header[offset+14] != 0)
                    return null;
                offset += 15;
            }
            else
            {
                if (4 != colors && 3 != colors)
                    return null;
                offset += 12;
            }
            return new TlgMetaData
            {
                Width   = header.ToUInt32 (offset),
                Height  = header.ToUInt32 (offset+4),
                Bpp     = colors*8,
                Version     = version,
                DataOffset  = offset+8,
            };
        }

        public Bitmap ReadAndGetMetaData(BinaryReader file, out TlgMetaData md)
        {
            TlgMetaData meta = ReadMetaData(file);
            md = meta;
            var image = ReadTlg(file, meta);

            int tailSize = (int)Math.Min(file.BaseStream.Length - file.BaseStream.Position, 512);
            if (tailSize > 8)
            {
                var tail = file.ReadBytes(tailSize);
                try
                {
                    var blendedImage = ApplyTags(image, meta, tail);
                    if (null != blendedImage)
                        return blendedImage;
                }
                catch (FileNotFoundException x)
                {
                    Trace.WriteLine(string.Format("{0}: {1}", x.Message, x.FileName), "[TlgFormat.Read]");
                }
                catch (Exception x)
                {
                    Trace.WriteLine(x.Message, "[TlgFormat.Read]");
                }
            }
            //PixelFormat format = 32 == meta.BPP ? PixelFormats.Bgra32 : PixelFormats.Bgr32;
            PixelFormat format = 32 == meta.Bpp ? PixelFormat.Format32bppArgb : PixelFormat.Format32bppRgb;
            return CreateImage(meta, format, image, (int)meta.Width * 4);
        }

        public Bitmap Read (BinaryReader file)
        {
            return ReadAndGetMetaData(file, out _);
        }

        private unsafe Bitmap CreateImage(TlgMetaData meta, PixelFormat format, byte[] image, int stride)
        {
            Bitmap bmp;
            fixed (byte* p = image)
            {
                IntPtr ptr = (IntPtr)p;
                bmp = new Bitmap((int)meta.Width, (int)meta.Height, stride, format, ptr);
            }

            return bmp;
        }

        public byte[] Write (BinaryReader file)
        {
            throw new NotImplementedException ("TlgFormat.Write not implemented");
        }

        byte[] ReadTlg (BinaryReader src, TlgMetaData info)
        {
            src.BaseStream.Position = info.DataOffset;
            if (6 == info.Version)
                return ReadV6 (src, info);
            else
                return ReadV5 (src, info);
        }

        Bitmap ApplyTags (byte[] image, TlgMetaData meta, byte[] tail)
        {
            int i = tail.Length - 8;
            while (i >= 0)
            {
                if ('s' == tail[i+3] && 'g' == tail[i+2] && 'a' == tail[i+1] && 't' == tail[i])
                    break;
                --i;
            }
            if (i < 0)
                return null;
            var tags = new TagsParser (tail, i+4);
            if (!tags.Parse())
                return null;
            var baseName   = tags.GetString (1);
            meta.OffsetX    = tags.GetInt (2) & 0xFFFF;
            meta.OffsetY    = tags.GetInt (3) & 0xFFFF;
            if (string.IsNullOrEmpty (baseName))
                return null;

            Console.WriteLine($"[Missing] {meta.FileName}/{baseName}");
            throw new NotImplementedException("Blend Images are not supported yet.");
            //return null;

            /*
            baseName = VFS.CombinePath (VFS.GetDirectoryName (meta.FileName), baseName);
            if (baseName == meta.FileName)
                return null;

            TlgMetaData base_info;
            byte[] base_image;
            using (BinaryReader base_file = VFS.OpenBinaryStream (baseName))
            {
                base_info = ReadMetaData (base_file) as TlgMetaData;
                if (null == base_info)
                    return null;
                base_info.FileName = baseName;
                base_image = ReadTlg (base_file, base_info);
            }
            var pixels = BlendImage (base_image, base_info, image, meta);
            //PixelFormat format = 32 == base_info.BPP ? PixelFormats.Bgra32 : PixelFormats.Bgr32;
            PixelFormat format = 32 == meta.BPP ? PixelFormat.Format32bppArgb : PixelFormat.Format32bppRgb;
            return CreateImage (base_info, format, pixels, (int)base_info.Width*4);
        */
        }

        byte[] BlendImage(byte[] baseImage, TlgMetaData baseInfo, byte[] overlay, TlgMetaData overlayInfo)
        {
            int dstStride = (int)baseInfo.Width * 4;
            int srcStride = (int)overlayInfo.Width * 4;
            int dst = overlayInfo.OffsetY * dstStride + overlayInfo.OffsetX * 4;
            int src = 0;
            int gap = dstStride - srcStride;
            for (uint y = 0; y < overlayInfo.Height; ++y)
            {
                for (uint x = 0; x < overlayInfo.Width; ++x)
                {
                    byte srcAlpha = overlay[src+3];
                    if (srcAlpha != 0)
                    {
                        if (0xFF == srcAlpha || 0 == baseImage[dst+3])
                        {
                            baseImage[dst]   = overlay[src];
                            baseImage[dst+1] = overlay[src+1];
                            baseImage[dst+2] = overlay[src+2];
                            baseImage[dst+3] = srcAlpha;
                        }
                        else
                        {
                            // FIXME this blending algorithm is oversimplified.
                            baseImage[dst+0] = (byte)((overlay[src+0] * srcAlpha
                                              + baseImage[dst+0] * (0xFF - srcAlpha)) / 0xFF);
                            baseImage[dst+1] = (byte)((overlay[src+1] * srcAlpha
                                              + baseImage[dst+1] * (0xFF - srcAlpha)) / 0xFF);
                            baseImage[dst+2] = (byte)((overlay[src+2] * srcAlpha
                                              + baseImage[dst+2] * (0xFF - srcAlpha)) / 0xFF);
                            baseImage[dst+3] = (byte)Math.Max (srcAlpha, baseImage[dst+3]);
                        }
                    }
                    dst += 4;
                    src += 4;
                }
                dst += gap;
            }
            return baseImage;
        }

        const int TVP_TLG6_H_BLOCK_SIZE = 8;
        const int TVP_TLG6_W_BLOCK_SIZE = 8;

        const int TVP_TLG6_GOLOMB_N_COUNT = 4;
        const int TVP_TLG6_LeadingZeroTable_BITS = 12;
        const int TVP_TLG6_LeadingZeroTable_SIZE = (1<<TVP_TLG6_LeadingZeroTable_BITS);

        byte[] ReadV6 (BinaryReader src, TlgMetaData info)
        {
            int width = (int)info.Width;
            int height = (int)info.Height;
            int colors = info.Bpp / 8;
            int maxBitLength = src.ReadInt32();

            int xBlockCount = ((width - 1)/ TVP_TLG6_W_BLOCK_SIZE) + 1;
            int yBlockCount = ((height - 1)/ TVP_TLG6_H_BLOCK_SIZE) + 1;
            int mainCount = width / TVP_TLG6_W_BLOCK_SIZE;
            int fraction = width -  mainCount * TVP_TLG6_W_BLOCK_SIZE;

            var imageBits = new uint[height * width];
            var bitPool = new byte[maxBitLength / 8 + 5];
            var pixelbuf = new uint[width * TVP_TLG6_H_BLOCK_SIZE + 1];
            var filterTypes = new byte[xBlockCount * yBlockCount];
            var zeroline = new uint[width];
            var lzssText = new byte[4096];

            // initialize zero line (virtual y=-1 line)
            uint zerocolor = 3 == colors ? 0xff000000 : 0x00000000;
            for (var i = 0; i < width; ++i)
                zeroline[i] = zerocolor;

            uint[] prevline = zeroline;
            int prevlineIndex = 0;

            // initialize LZSS text (used by chroma filter type codes)
            int p = 0;
            for (uint i = 0; i < 32*0x01010101; i += 0x01010101)
            {
                for (uint j = 0; j < 16*0x01010101; j += 0x01010101)
                {
                    lzssText[p++] = (byte)(i       & 0xff);
                    lzssText[p++] = (byte)(i >> 8  & 0xff);
                    lzssText[p++] = (byte)(i >> 16 & 0xff);
                    lzssText[p++] = (byte)(i >> 24 & 0xff);
                    lzssText[p++] = (byte)(j       & 0xff);
                    lzssText[p++] = (byte)(j >> 8  & 0xff);
                    lzssText[p++] = (byte)(j >> 16 & 0xff);
                    lzssText[p++] = (byte)(j >> 24 & 0xff);
                }
            }
            // read chroma filter types.
            // chroma filter types are compressed via LZSS as used by TLG5.
            {
                int inbufSize = src.ReadInt32();
                byte[] inbuf = src.ReadBytes (inbufSize);
                if (inbufSize != inbuf.Length)
                    return null;
                TVPTLG5DecompressSlide (filterTypes, inbuf, inbufSize, lzssText, 0);
            }

            // for each horizontal block group ...
            for (int y = 0; y < height; y += TVP_TLG6_H_BLOCK_SIZE)
            {
                int ylim = y + TVP_TLG6_H_BLOCK_SIZE;
                if (ylim >= height) ylim = height;

                int pixelCount = (ylim - y) * width;

                // decode values
                for (int c = 0; c < colors; c++)
                {
                    // read bit length
                    int bitLength = src.ReadInt32();

                    // get compress method
                    int method = (bitLength >> 30) & 3;
                    bitLength &= 0x3fffffff;

                    // compute byte length
                    int byteLength = bitLength / 8;
                    if (0 != (bitLength % 8)) byteLength++;

                    // read source from input
                    src.Read (bitPool, 0, byteLength);

                    // decode values
                    // two most significant bits of bitlength are
                    // entropy coding method;
                    // 00 means Golomb method,
                    // 01 means Gamma method (not yet suppoted),
                    // 10 means modified LZSS method (not yet supported),
                    // 11 means raw (uncompressed) data (not yet supported).

                    switch (method)
                    {
                    case 0:
                        if (c == 0 && colors != 1)
                            TVPTLG6DecodeGolombValuesForFirst (pixelbuf, pixelCount, bitPool);
                        else
                            TVPTLG6DecodeGolombValues (pixelbuf, c*8, pixelCount, bitPool);
                        break;
                    default:
                        throw new FormatException ("Unsupported entropy coding method");
                    }
                }

                // for each line
                int ft = (y / TVP_TLG6_H_BLOCK_SIZE) * xBlockCount; // within filter_types
                int skipbytes = (ylim - y) * TVP_TLG6_W_BLOCK_SIZE;

                for (int yy = y; yy < ylim; yy++)
                {
                    int curline = yy*width;

                    int dir = (yy&1)^1;
                    int oddskip = ((ylim - yy -1) - (yy-y));
                    if (0 != mainCount)
                    {
                        int start =
                            ((width < TVP_TLG6_W_BLOCK_SIZE) ? width : TVP_TLG6_W_BLOCK_SIZE) *
                                (yy - y);
                        TVPTLG6DecodeLineGeneric (
                            prevline, prevlineIndex,
                            imageBits, curline,
                            width, 0, mainCount,
                            filterTypes, ft,
                            skipbytes,
                            pixelbuf, start,
                            zerocolor, oddskip, dir);
                    }

                    if (mainCount != xBlockCount)
                    {
                        int ww = fraction;
                        if (ww > TVP_TLG6_W_BLOCK_SIZE) ww = TVP_TLG6_W_BLOCK_SIZE;
                        int start = ww * (yy - y);
                        TVPTLG6DecodeLineGeneric (
                            prevline, prevlineIndex,
                            imageBits, curline,
                            width, mainCount, xBlockCount,
                            filterTypes, ft,
                            skipbytes,
                            pixelbuf, start,
                            zerocolor, oddskip, dir);
                    }
                    prevline = imageBits;
                    prevlineIndex = curline;
                }
            }
            int stride = width * 4;
            var pixels = new byte[height * stride];
            Buffer.BlockCopy (imageBits, 0, pixels, 0, pixels.Length);
            return pixels;
        }

        byte[] ReadV5 (BinaryReader src, TlgMetaData info)
        {
            int width = (int)info.Width;
            int height = (int)info.Height;
            int colors = info.Bpp / 8;
            int blockheight = src.ReadInt32();
            int blockcount = (height - 1) / blockheight + 1;

            // skip block size section
            src.BaseStream.Seek (blockcount * 4, SeekOrigin.Current);

            int stride = width * 4;
            var imageBits = new byte[height * stride];
            var text = new byte[4096];
            for (int i = 0; i < 4096; ++i)
                text[i] = 0;

            var inbuf = new byte[blockheight * width + 10];
            byte [][] outbuf = new byte[4][];
            for (int i = 0; i < colors; i++)
                outbuf[i] = new byte[blockheight * width + 10];

            int z = 0;
            int prevline = -1;
            for (int yBlk = 0; yBlk < height; yBlk += blockheight)
            {
                // read file and decompress
                for (int c = 0; c < colors; c++)
                {
                    byte mark = src.ReadByte();
                    int size;
                    size = src.ReadInt32();
                    if (mark == 0)
                    {
                        // modified LZSS compressed data
                        if (size != src.Read (inbuf, 0, size))
                            return null;
                        z = TVPTLG5DecompressSlide (outbuf[c], inbuf, size, text, z);
                    }
                    else
                    {
                        // raw data
                        src.Read (outbuf[c], 0, size);
                    }
                }

                // compose colors and store
                int yLim = yBlk + blockheight;
                if (yLim > height) yLim = height;
                int outbufPos = 0;
                for (int y = yBlk; y < yLim; y++)
                {
                    int current = y * stride;
                    int currentOrg = current;
                    if (prevline >= 0)
                    {
                        // not first line
                        switch(colors)
                        {
                        case 3:
                            TVPTLG5ComposeColors3To4 (imageBits, current, prevline,
                                                        outbuf, outbufPos, width);
                            break;
                        case 4:
                            TVPTLG5ComposeColors4To4 (imageBits, current, prevline,
                                                        outbuf, outbufPos, width);
                            break;
                        }
                    }
                    else
                    {
                        // first line
                        switch(colors)
                        {
                        case 3:
                            for (int pr = 0, pg = 0, pb = 0, x = 0;
                                    x < width; x++)
                            {
                                int b = outbuf[0][outbufPos+x];
                                int g = outbuf[1][outbufPos+x];
                                int r = outbuf[2][outbufPos+x];
                                b += g; r += g;
                                imageBits[current++] = (byte)(pb += b);
                                imageBits[current++] = (byte)(pg += g);
                                imageBits[current++] = (byte)(pr += r);
                                imageBits[current++] = 0xff;
                            }
                            break;
                        case 4:
                            for (int pr = 0, pg = 0, pb = 0, pa = 0, x = 0;
                                    x < width; x++)
                            {
                                int b = outbuf[0][outbufPos+x];
                                int g = outbuf[1][outbufPos+x];
                                int r = outbuf[2][outbufPos+x];
                                int a = outbuf[3][outbufPos+x];
                                b += g; r += g;
                                imageBits[current++] = (byte)(pb += b);
                                imageBits[current++] = (byte)(pg += g);
                                imageBits[current++] = (byte)(pr += r);
                                imageBits[current++] = (byte)(pa += a);
                            }
                            break;
                        }
                    }
                    outbufPos += width;
                    prevline = currentOrg;
                }
            }
            return imageBits;
        }

        void TVPTLG5ComposeColors3To4 (byte[] outp, int outpIndex, int upper,
                                       byte[][] buf, int bufpos, int width)
        {
            byte pc0 = 0, pc1 = 0, pc2 = 0;
            byte c0, c1, c2;
            for (int x = 0; x < width; x++)
            {
                c0 = buf[0][bufpos+x];
                c1 = buf[1][bufpos+x];
                c2 = buf[2][bufpos+x];
                c0 += c1; c2 += c1;
                outp[outpIndex++] = (byte)(((pc0 += c0) + outp[upper+0]) & 0xff);
                outp[outpIndex++] = (byte)(((pc1 += c1) + outp[upper+1]) & 0xff);
                outp[outpIndex++] = (byte)(((pc2 += c2) + outp[upper+2]) & 0xff);
                outp[outpIndex++] = 0xff;
                upper += 4;
            }
        }

        void TVPTLG5ComposeColors4To4 (byte[] outp, int outpIndex, int upper,
                                       byte[][] buf, int bufpos, int width)
        {
            byte pc0 = 0, pc1 = 0, pc2 = 0, pc3 = 0;
            byte c0, c1, c2, c3;
            for (int x = 0; x < width; x++)
            {
                c0 = buf[0][bufpos+x];
                c1 = buf[1][bufpos+x];
                c2 = buf[2][bufpos+x];
                c3 = buf[3][bufpos+x];
                c0 += c1; c2 += c1;
                outp[outpIndex++] = (byte)(((pc0 += c0) + outp[upper+0]) & 0xff);
                outp[outpIndex++] = (byte)(((pc1 += c1) + outp[upper+1]) & 0xff);
                outp[outpIndex++] = (byte)(((pc2 += c2) + outp[upper+2]) & 0xff);
                outp[outpIndex++] = (byte)(((pc3 += c3) + outp[upper+3]) & 0xff);
                upper += 4;
            }
        }

        int TVPTLG5DecompressSlide (byte[] outbuf, byte[] inbuf, int inbufSize, byte[] text, int initialr)
        {
            int r = initialr;
            uint flags = 0;
            int o = 0;
            for (int i = 0; i < inbufSize; )
            {
                if (((flags >>= 1) & 256) == 0)
                {
                    flags = (uint)(inbuf[i++] | 0xff00);
                }
                if (0 != (flags & 1))
                {
                    int mpos = inbuf[i] | ((inbuf[i+1] & 0xf) << 8);
                    int mlen = (inbuf[i+1] & 0xf0) >> 4;
                    i += 2;
                    mlen += 3;
                    if (mlen == 18) mlen += inbuf[i++];

                    while (0 != mlen--)
                    {
                        outbuf[o++] = text[r++] = text[mpos++];
                        mpos &= (4096 - 1);
                        r &= (4096 - 1);
                    }
                }
                else
                {
                    byte c = inbuf[i++];
                    outbuf[o++] = c;
                    text[r++] = c;
                    r &= (4096 - 1);
                }
            }
            return r;
        }

        static uint tvp_make_gt_mask (uint a, uint b)
        {
            uint tmp2 = ~b;
            uint tmp = ((a & tmp2) + (((a ^ tmp2) >> 1) & 0x7f7f7f7f) ) & 0x80808080;
            tmp = ((tmp >> 7) + 0x7f7f7f7f) ^ 0x7f7f7f7f;
            return tmp;
        }

        static uint tvp_packed_bytes_add (uint a, uint b)
        {
            uint tmp = (uint)((((a & b)<<1) + ((a ^ b) & 0xfefefefe) ) & 0x01010100);
            return a+b-tmp;
        }

        static uint tvp_med2 (uint a, uint b, uint c)
        {
            /* do Median Edge Detector   thx, Mr. sugi  at    kirikiri.info */
            uint aa_gt_bb = tvp_make_gt_mask(a, b);
            uint a_xor_b_and_aa_gt_bb = ((a ^ b) & aa_gt_bb);
            uint aa = a_xor_b_and_aa_gt_bb ^ a;
            uint bb = a_xor_b_and_aa_gt_bb ^ b;
            uint n = tvp_make_gt_mask(c, bb);
            uint nn = tvp_make_gt_mask(aa, c);
            uint m = ~(n | nn);
            return (n & aa) | (nn & bb) | ((bb & m) - (c & m) + (aa & m));
        }

        static uint tvp_med (uint a, uint b, uint c, uint v)
        {
            return tvp_packed_bytes_add (tvp_med2 (a, b, c), v);
        }

        static uint tvp_avg (uint a, uint b, uint c, uint v)
        {
            return tvp_packed_bytes_add ((((a&b) + (((a^b) & 0xfefefefe) >> 1)) + ((a^b)&0x01010101)), v);
        }

        delegate uint TvpDecoder (uint a, uint b, uint c, uint v);

        void TVPTLG6DecodeLineGeneric (uint[] prevline, int prevlineIndex,
                                       uint[] curline, int curlineIndex,
                                       int width, int startBlock, int blockLimit,
                                       byte[] filtertypes, int filtertypesIndex,
                                       int skipblockbytes,
                                       uint[] inbuf, int inbufIndex,
                                       uint initialp, int oddskip, int dir)
        {
            /*
                chroma/luminosity decoding
                (this does reordering, color correlation filter, MED/AVG  at a time)
            */
            uint p, up;

            if (0 != startBlock)
            {
                prevlineIndex += startBlock * TVP_TLG6_W_BLOCK_SIZE;
                curlineIndex  += startBlock * TVP_TLG6_W_BLOCK_SIZE;
                p  = curline[curlineIndex-1];
                up = prevline[prevlineIndex-1];
            }
            else
            {
                p = up = initialp;
            }

            inbufIndex += skipblockbytes * startBlock;
            int step = 0 != (dir & 1) ? 1 : -1;

            for (int i = startBlock; i < blockLimit; i++)
            {
                int w = width - i*TVP_TLG6_W_BLOCK_SIZE;
                if (w > TVP_TLG6_W_BLOCK_SIZE) w = TVP_TLG6_W_BLOCK_SIZE;
                int ww = w;
                if (step == -1) inbufIndex += ww-1;
                if (0 != (i & 1)) inbufIndex += oddskip * ww;

                TvpDecoder decoder;
                switch (filtertypes[filtertypesIndex+i])
                {
                case 0:
                    decoder = tvp_med;
                    break;
                case 1:
                    decoder = tvp_avg;
                    break;
                case 2:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+((v>>8)&0xff))<<16)) + (((v>>8)&0xff)<<8) + (0xff & ((v&0xff)+((v>>8)&0xff))) + ((v&0xff000000))));
                    break;
                case 3:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+((v>>8)&0xff))<<16)) + (((v>>8)&0xff)<<8) + (0xff & ((v&0xff)+((v>>8)&0xff))) + ((v&0xff000000))));
                    break;
                case 4:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff)+((v>>8)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 5:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff)+((v>>8)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 6:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>16)&0xff)+((v>>8)&0xff))) + ((v&0xff000000))));
                    break;
                case 7:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>16)&0xff)+((v>>8)&0xff))) + ((v&0xff000000))));
                    break;
                case 8:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff)+((v>>16)&0xff)+((v>>8)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>16)&0xff))) + ((v&0xff000000))));
                    break;
                case 9:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff)+((v>>16)&0xff)+((v>>8)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>16)&0xff))) + ((v&0xff000000))));
                    break;
                case 10:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>16)&0xff))) + ((v&0xff000000))));
                    break;
                case 11:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>16)&0xff))) + ((v&0xff000000))));
                    break;
                case 12:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>8)&0xff))) + ((v&0xff000000))));
                    break;
                case 13:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>8)&0xff))) + ((v&0xff000000))));
                    break;
                case 14:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 15:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 16:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+((v>>8)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 17:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+((v>>8)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 18:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff)+((v>>8)&0xff)+((v>>16)&0xff)+(v&0xff))) + ((v&0xff000000))));
                    break;
                case 19:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff)+((v>>8)&0xff)+((v>>16)&0xff)+(v&0xff))) + ((v&0xff000000))));
                    break;
                case 20:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>16)&0xff))) + ((v&0xff000000))));
                    break;
                case 21:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>16)&0xff))) + ((v&0xff000000))));
                    break;
                case 22:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 23:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 24:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 25:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 26:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff)+((v>>8)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff)+(v&0xff)+((v>>8)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>8)&0xff))) + ((v&0xff000000))));
                    break;
                case 27:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff)+((v>>8)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff)+(v&0xff)+((v>>8)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>8)&0xff))) + ((v&0xff000000))));
                    break;
                case 28:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff)+((v>>8)&0xff)+((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>8)&0xff)+((v>>16)&0xff))) + ((v&0xff000000))));
                    break;
                case 29:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff)+((v>>8)&0xff)+((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>8)&0xff)+((v>>16)&0xff))) + ((v&0xff000000))));
                    break;
                case 30:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+((v&0xff)<<1))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v&0xff)<<1))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 31:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+((v&0xff)<<1))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v&0xff)<<1))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                default: return;
                }
                do {
                    uint u = prevline[prevlineIndex];
                    p = decoder (p, u, up, inbuf[inbufIndex]);
                    up = u;
                    curline[curlineIndex] = p;
                    curlineIndex++;
                    prevlineIndex++;
                    inbufIndex += step;
                } while (0 != --w);
                if (step == 1)
                    inbufIndex += skipblockbytes - ww;
                else
                    inbufIndex += skipblockbytes + 1;
                if (0 != (i&1)) inbufIndex -= oddskip * ww;
            }
        }

        static class TvpTables
        {
            public static byte[] TVPTLG6LeadingZeroTable = new byte[TVP_TLG6_LeadingZeroTable_SIZE];
            public static sbyte[,] TVPTLG6GolombBitLengthTable = new sbyte
                [TVP_TLG6_GOLOMB_N_COUNT*2*128, TVP_TLG6_GOLOMB_N_COUNT];
            static short[,] TVPTLG6GolombCompressed = new short[TVP_TLG6_GOLOMB_N_COUNT,9] {
                    {3,7,15,27,63,108,223,448,130,},
                    {3,5,13,24,51,95,192,384,257,},
                    {2,5,12,21,39,86,155,320,384,},
                    {2,3,9,18,33,61,129,258,511,},
                /* Tuned by W.Dee, 2004/03/25 */
            };

            static TvpTables ()
            {
                TVPTLG6InitLeadingZeroTable();
                TVPTLG6InitGolombTable();
            }

            static void TVPTLG6InitLeadingZeroTable ()
            {
                /* table which indicates first set bit position + 1. */
                /* this may be replaced by BSF (IA32 instrcution). */

                for (int i = 0; i < TVP_TLG6_LeadingZeroTable_SIZE; i++)
                {
                    int cnt = 0;
                    int j;
                    for(j = 1; j != TVP_TLG6_LeadingZeroTable_SIZE && 0 == (i & j);
                        j <<= 1, cnt++);
                    cnt++;
                    if (j == TVP_TLG6_LeadingZeroTable_SIZE) cnt = 0;
                    TVPTLG6LeadingZeroTable[i] = (byte)cnt;
                }
            }

            static void TVPTLG6InitGolombTable()
            {
                for (int n = 0; n < TVP_TLG6_GOLOMB_N_COUNT; n++)
                {
                    int a = 0;
                    for (int i = 0; i < 9; i++)
                    {
                        for (int j = 0; j < TVPTLG6GolombCompressed[n,i]; j++)
                            TVPTLG6GolombBitLengthTable[a++,n] = (sbyte)i;
                    }
                    if(a != TVP_TLG6_GOLOMB_N_COUNT*2*128)
                        throw new Exception ("Invalid data initialization");   /* THIS MUST NOT BE EXECUETED! */
                            /* (this is for compressed table data check) */
                }
            }
        }

        void TVPTLG6DecodeGolombValuesForFirst (uint[] pixelbuf, int pixelCount, byte[] bitPool)
        {
            /*
                decode values packed in "bit_pool".
                values are coded using golomb code.

                "ForFirst" function do dword access to pixelbuf,
                clearing with zero except for blue (least siginificant byte).
            */
            int bitPoolIndex = 0;

            int n = TVP_TLG6_GOLOMB_N_COUNT - 1; /* output counter */
            int a = 0; /* summary of absolute values of errors */

            int bitPos = 1;
            bool zero = 0 == (bitPool[bitPoolIndex] & 1);

            for (int pixel = 0; pixel < pixelCount; )
            {
                /* get running count */
                int count;

                {
                    uint t = BitConverter.ToUInt32 (bitPool, bitPoolIndex) >> bitPos;
                    int b = TvpTables.TVPTLG6LeadingZeroTable[t & (TVP_TLG6_LeadingZeroTable_SIZE-1)];
                    int bitCount = b;
                    while (0 == b)
                    {
                        bitCount += TVP_TLG6_LeadingZeroTable_BITS;
                        bitPos += TVP_TLG6_LeadingZeroTable_BITS;
                        bitPoolIndex += bitPos >> 3;
                        bitPos &= 7;
                        t = BitConverter.ToUInt32 (bitPool, bitPoolIndex) >> bitPos;
                        b = TvpTables.TVPTLG6LeadingZeroTable[t&(TVP_TLG6_LeadingZeroTable_SIZE-1)];
                        bitCount += b;
                    }
                    bitPos += b;
                    bitPoolIndex += bitPos >> 3;
                    bitPos &= 7;

                    bitCount --;
                    count = 1 << bitCount;
                    count += ((BitConverter.ToInt32 (bitPool, bitPoolIndex) >> (bitPos)) & (count-1));

                    bitPos += bitCount;
                    bitPoolIndex += bitPos >> 3;
                    bitPos &= 7;
                }
                if (zero)
                {
                    /* zero values */

                    /* fill distination with zero */
                    do { pixelbuf[pixel++] = 0; } while (0 != --count);

                    zero = !zero;
                }
                else
                {
                    /* non-zero values */

                    /* fill distination with glomb code */

                    do
                    {
                        int k = TvpTables.TVPTLG6GolombBitLengthTable[a,n];
                        int v, sign;

                        uint t = BitConverter.ToUInt32 (bitPool, bitPoolIndex) >> bitPos;
                        int bitCount;
                        int b;
                        if (0 != t)
                        {
                            b = TvpTables.TVPTLG6LeadingZeroTable[t&(TVP_TLG6_LeadingZeroTable_SIZE-1)];
                            bitCount = b;
                            while (0 == b)
                            {
                                bitCount += TVP_TLG6_LeadingZeroTable_BITS;
                                bitPos += TVP_TLG6_LeadingZeroTable_BITS;
                                bitPoolIndex += bitPos >> 3;
                                bitPos &= 7;
                                t = BitConverter.ToUInt32 (bitPool, bitPoolIndex) >> bitPos;
                                b = TvpTables.TVPTLG6LeadingZeroTable[t&(TVP_TLG6_LeadingZeroTable_SIZE-1)];
                                bitCount += b;
                            }
                            bitCount --;
                        }
                        else
                        {
                            bitPoolIndex += 5;
                            bitCount = bitPool[bitPoolIndex-1];
                            bitPos = 0;
                            t = BitConverter.ToUInt32 (bitPool, bitPoolIndex);
                            b = 0;
                        }

                        v = (int)((bitCount << k) + ((t >> b) & ((1<<k)-1)));
                        sign = (v & 1) - 1;
                        v >>= 1;
                        a += v;
                        pixelbuf[pixel++] = (byte)((v ^ sign) + sign + 1);

                        bitPos += b;
                        bitPos += k;
                        bitPoolIndex += bitPos >> 3;
                        bitPos &= 7;

                        if (--n < 0)
                        {
                            a >>= 1;
                            n = TVP_TLG6_GOLOMB_N_COUNT - 1;
                        }
                    } while (0 != --count);
                    zero = !zero;
                }
            }
        }

        void TVPTLG6DecodeGolombValues (uint[] pixelbuf, int offset, int pixelCount, byte[] bitPool)
        {
            /*
                decode values packed in "bit_pool".
                values are coded using golomb code.
            */
            uint mask = (uint)~(0xff << offset);
            int bitPoolIndex = 0;

            int n = TVP_TLG6_GOLOMB_N_COUNT - 1; /* output counter */
            int a = 0; /* summary of absolute values of errors */

            int bitPos = 1;
            bool zero = 0 == (bitPool[bitPoolIndex] & 1);

            for (int pixel = 0; pixel < pixelCount; )
            {
                /* get running count */
                int count;

                {
                    uint t = BitConverter.ToUInt32 (bitPool, bitPoolIndex) >> bitPos;
                    int b = TvpTables.TVPTLG6LeadingZeroTable[t&(TVP_TLG6_LeadingZeroTable_SIZE-1)];
                    int bitCount = b;
                    while (0 == b)
                    {
                        bitCount += TVP_TLG6_LeadingZeroTable_BITS;
                        bitPos += TVP_TLG6_LeadingZeroTable_BITS;
                        bitPoolIndex += bitPos >> 3;
                        bitPos &= 7;
                        t = BitConverter.ToUInt32 (bitPool, bitPoolIndex) >> bitPos;
                        b = TvpTables.TVPTLG6LeadingZeroTable[t&(TVP_TLG6_LeadingZeroTable_SIZE-1)];
                        bitCount += b;
                    }
                    bitPos += b;
                    bitPoolIndex += bitPos >> 3;
                    bitPos &= 7;

                    bitCount --;
                    count = 1 << bitCount;
                    count += (int)((BitConverter.ToUInt32 (bitPool, bitPoolIndex) >> (bitPos)) & (count-1));

                    bitPos += bitCount;
                    bitPoolIndex += bitPos >> 3;
                    bitPos &= 7;
                }
                if (zero)
                {
                    /* zero values */

                    /* fill distination with zero */
                    do { pixelbuf[pixel++] &= mask; } while (0 != --count);

                    zero = !zero;
                }
                else
                {
                    /* non-zero values */

                    /* fill distination with glomb code */

                    do
                    {
                        int k = TvpTables.TVPTLG6GolombBitLengthTable[a,n];
                        int v, sign;

                        uint t = BitConverter.ToUInt32 (bitPool, bitPoolIndex) >> bitPos;
                        int bitCount;
                        int b;
                        if (0 != t)
                        {
                            b = TvpTables.TVPTLG6LeadingZeroTable[t&(TVP_TLG6_LeadingZeroTable_SIZE-1)];
                            bitCount = b;
                            while (0 == b)
                            {
                                bitCount += TVP_TLG6_LeadingZeroTable_BITS;
                                bitPos += TVP_TLG6_LeadingZeroTable_BITS;
                                bitPoolIndex += bitPos >> 3;
                                bitPos &= 7;
                                t = BitConverter.ToUInt32 (bitPool, bitPoolIndex) >> bitPos;
                                b = TvpTables.TVPTLG6LeadingZeroTable[t&(TVP_TLG6_LeadingZeroTable_SIZE-1)];
                                bitCount += b;
                            }
                            bitCount --;
                        }
                        else
                        {
                            bitPoolIndex += 5;
                            bitCount = bitPool[bitPoolIndex-1];
                            bitPos = 0;
                            t = BitConverter.ToUInt32 (bitPool, bitPoolIndex);
                            b = 0;
                        }

                        v = (int)((bitCount << k) + ((t >> b) & ((1<<k)-1)));
                        sign = (v & 1) - 1;
                        v >>= 1;
                        a += v;
                        uint c = (uint)((pixelbuf[pixel] & mask) | (uint)((byte)((v ^ sign) + sign + 1) << offset));
                        pixelbuf[pixel++] = c;

                        bitPos += b;
                        bitPos += k;
                        bitPoolIndex += bitPos >> 3;
                        bitPos &= 7;

                        if (--n < 0)
                        {
                            a >>= 1;
                            n = TVP_TLG6_GOLOMB_N_COUNT - 1;
                        }
                    } while (0 != --count);
                    zero = !zero;
                }
            }
        }
    }

    internal class TagsParser
    {
        byte[]                              _mTags;
        Dictionary<int, Tuple<int, int>>    _mMap = new Dictionary<int, Tuple<int, int>>();
        int                                 _mOffset;

        public TagsParser (byte[] tags, int offset)
        {
            _mTags = tags;
            _mOffset = offset;
        }

        public bool Parse ()
        {
            int length = BitConverter.ToInt32 (_mTags, _mOffset);
            _mOffset += 4;
            if (length <= 0 || length > _mTags.Length - _mOffset)
                return false;
            while (_mOffset < _mTags.Length)
            {
                int keyLen = ParseInt();
                if (keyLen < 0)
                    return false;
                int key;
                switch (keyLen)
                {
                case 1:
                    key = _mTags[_mOffset];
                    break;
                case 2:
                    key = BitConverter.ToUInt16 (_mTags, _mOffset);
                    break;
                case 4:
                    key = BitConverter.ToInt32 (_mTags, _mOffset);
                    break;
                default:
                    return false;
                }
                _mOffset += keyLen + 1;
                int valueLen = ParseInt();
                if (valueLen < 0)
                    return false;
                _mMap[key] = Tuple.Create (_mOffset, valueLen);
                _mOffset += valueLen + 1;
            }
            return _mMap.Count > 0;
        }

        int ParseInt ()
        {
            int colon = Array.IndexOf (_mTags, (byte)':', _mOffset);
            if (-1 == colon)
                return -1;
            var lenStr = Encoding.ASCII.GetString (_mTags, _mOffset, colon-_mOffset);
            _mOffset = colon + 1;
            return Int32.Parse (lenStr);
        }

        public int GetInt (int key)
        {
            var val = _mMap[key];
            switch (val.Item2)
            {
            case 0: return 0;
            case 1: return _mTags[val.Item1];
            case 2: return BitConverter.ToUInt16 (_mTags, val.Item1);
            case 4: return BitConverter.ToInt32 (_mTags, val.Item1);
            default: throw new FormatException();
            }
        }

        public string GetString (int key)
        {
            var val = _mMap[key];
            return Encoding.GetEncoding("SHIFT-JIS").GetString (_mTags, val.Item1, val.Item2);
        }
    }
}
