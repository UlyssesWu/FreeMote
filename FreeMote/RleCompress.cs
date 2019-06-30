using System;
using System.IO;
using System.Linq;

namespace FreeMote
{
    /// <summary>
    /// RLE Compress
    /// <para>originally by number201724</para>
    /// </summary>
    internal static class RleCompress
    {
        public const int LzssLookShift = 7;
        public const int LzssLookAhead = 1 << LzssLookShift;

        /// <summary>
        /// RLE Decompress
        /// </summary>
        /// <param name="input"></param>
        /// <param name="actualSize"></param>
        /// <param name="align"></param>
        /// <returns></returns>
        public static byte[] Decompress(Stream input, int align = 4, int actualSize = 0)
        {
            MemoryStream output = actualSize > 0 ? new MemoryStream(actualSize) : new MemoryStream();
            //int currentIndex = 0;
            int totalBytes = 0;
            while (input.Position < input.Length)
            {
                int current = input.ReadByte();
                totalBytes++;
                int count;
                if ((current & LzssLookAhead) != 0) //Redundant
                {
                    count = (current ^ LzssLookAhead) + 3;
                    byte[] buffer = new byte[align];
                    input.Read(buffer, 0, align);
                    for (int i = 0; i < count; i++)
                    {
                        output.Write(buffer, 0, align);
                    }
                    //output.Write(new byte[align], 0, align);
                    totalBytes += align;
                }
                else //not redundant
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
        /// <param name="cmdByte"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        private static int CompressBound(Stream input, int align, out byte cmdByte, out byte[] buffer)
        {
            var pos = input.Position;
            buffer = new byte[align];
            input.Read(buffer, 0, align);
            int count = 1; //repeat count, first not included

            for (int i = 1; i < LzssLookAhead + 2; i++) //0 is `buffer`, 1,2,3... are redundant
            {
                if (input.Position >= input.Length)
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
                cmdByte = (byte)((count - 3) | LzssLookAhead); //>=128
                return count;
            }
            cmdByte = 0;
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
            var pos = input.Position;
            byte[] buffer = new byte[align];
            input.Read(buffer, 0, align);
            int count = 1; //FIXED: Start from 1

            for (int i = 1; i < LzssLookAhead; i++) //<128
            {
                if (input.Position >= input.Length)
                {
                    break;
                }

                if (CompressBound(input, align, out result, out _) == 0)
                {
                    byte[] buffer2 = new byte[align];
                    input.Read(buffer2, 0, align); //Skip
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
            return b1.SequenceEqual(b2);
        }


        /// <summary>
        /// RLE Compress
        /// </summary>
        /// <param name="input"></param>
        /// <param name="align"></param>
        /// <returns></returns>
        public static byte[] Compress(Stream input, int align)
        {
            MemoryStream output = new MemoryStream();
            while (input.Position < input.Length)
            {
                byte cmdByte;
                byte[] buffer;
                var count = CompressBound(input, align, out cmdByte, out buffer);
                if (count > 0)
                {
                    output.WriteByte(cmdByte);
                    output.Write(buffer, 0, buffer.Length);
                    input.Seek(align * count, SeekOrigin.Current);
                }
                else
                {
                    count = CompressBoundNp(input, align, out cmdByte);
                    buffer = new byte[count * align];
                    input.Read(buffer, 0, buffer.Length);
                    output.WriteByte(cmdByte);
                    output.Write(buffer, 0, buffer.Length);
                }
            }
            return output.ToArray();
        }
    }
}
