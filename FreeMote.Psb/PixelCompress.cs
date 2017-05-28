using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FreeMote.Psb
{
    /// <summary>
    /// PSB Pixel Compress (for images)
    /// <para>originally by number201724</para>
    /// </summary>
    public static class PixelCompress
    {
        public const int LzssLookShift = 7;
        public const int LzssLookAhead = 1 << LzssLookShift;

       
        /// <summary>
        /// Pixel Uncompress
        /// </summary>
        /// <param name="input"></param>
        /// <param name="actualSize"></param>
        /// <param name="align"></param>
        /// <returns></returns>
        public static byte[] Uncompress(Stream input, int actualSize, int align)
        {
            MemoryStream output = new MemoryStream(actualSize);
            //int currentIndex = 0;
            int totalBytes = 0;
            int count;
            while (actualSize != totalBytes)
            {
                int current = input.ReadByte();
                totalBytes++;
                if ((current & LzssLookAhead) != 0)
                {
                    count = (current ^ LzssLookAhead) + 3;
                    byte[] buffer = new byte[align];
                    for (int i = 0; i < count; i++)
                    {
                        input.Read(buffer, 0, align);
                        output.Write(buffer, 0, align);
                    }
                    output.Write(new byte[align], 0, align);
                    totalBytes += align;
                }
                else
                {
                    count = (current + 1) * align;
                    byte[] buffer = new byte[count];
                    input.Read(buffer, 0, count);
                    output.Write(buffer, 0, count);
                    totalBytes += count;
                }
            }
            return output.ToArray();
        }

        /// <summary>
        /// Count equal patterns
        /// </summary>
        /// <param name="input"></param>
        /// <param name="align"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        private static int CompressBound(Stream input, int align, out byte result)
        {
            int count = 0;
            byte[] buffer = new byte[align];
            var pos = input.Position;
            input.Read(buffer, 0, align);

            for (int i = 0; i < LzssLookAhead + 2; i++)
            {
                if (i * align >= input.Length)
                {
                    break;
                }
                byte[] buffer2 = new byte[align];
                input.Read(buffer2, 0, align);
                if (BytesEqual(buffer, buffer2))
                {
                    count++;
                }
                else
                {
                    break;
                }
            }

            input.Seek(pos, SeekOrigin.Begin);
            if (count >= 3)
            {
                result = (byte)((count - 3) | LzssLookAhead);
                return count;
            }
            result = 0;
            return 0;
        }

        /// <summary>
        /// Count not equal patterns
        /// </summary>
        /// <param name="input"></param>
        /// <param name="align"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        private static int CompressBoundNp(Stream input, int align, out byte result)
        {
            int count = 0;
            byte[] buffer = new byte[align];
            var pos = input.Position;
            input.Read(buffer, 0, align);

            for (int i = 0; i < LzssLookAhead + 2; i++)
            {
                if (i * align >= input.Length)
                {
                    break;
                }
                byte[] buffer2 = new byte[align];
                input.Read(buffer2, 0, align);
                if (CompressBound(input,align, out result) == 0)
                {
                    count++;
                }
                else
                {
                    break;
                }
            }

            input.Seek(pos, SeekOrigin.Begin);
            result = (byte)(count - 1);
            return count;
        }

        //Interesting disscussion: https://stackoverflow.com/questions/43289/comparing-two-byte-arrays-in-net/8808245#comment19299391_8808245
        private static bool BytesEqual(byte[] b1, byte[] b2)
        {
            if (b1.Length != b2.Length)
            {
                return false;
            }
            if (b1.Length <= 4) //This is faster than for-loop
            {
                return BitConverter.ToUInt32(b1, 0) == BitConverter.ToUInt32(b2, 0);
            }
            for (int i = 0; i < b1.Length; i++)
            {
                if (b1[i] != b2[i])
                {
                    return false;
                }
            }
            return true;
        }


        /// <summary>
        /// Pixel Compress
        /// </summary>
        /// <param name="input"></param>
        /// <param name="align"></param>
        /// <param name="actualSize"></param>
        /// <returns></returns>
        public static byte[] Compress(Stream input, int align, out int actualSize)
        {
            MemoryStream output = new MemoryStream();
            int totalSize = 0;
            int blockSize = 0;
            int count;
            byte cmdByte;
            while (input.Position < input.Length)
            {
                count = CompressBound(input, align, out cmdByte);
                if (count > 0)
                {
                    byte[] buffer = new byte[align];
                    input.Read(buffer, 0, align);
                    blockSize = align + sizeof(byte);
                    output.WriteByte(cmdByte);
                    output.Write(buffer, 0, align);
                }
                else
                {
                    count = CompressBoundNp(input, align, out cmdByte);
                    byte[] buffer = new byte[count * align];
                    input.Read(buffer, 0, buffer.Length);
                    blockSize = count * align + sizeof(byte);
                    output.WriteByte(cmdByte);
                    output.Write(buffer, 0, buffer.Length);
                }

                totalSize += blockSize;
            }
            actualSize = totalSize;
            return output.ToArray();
        }
    }
}
