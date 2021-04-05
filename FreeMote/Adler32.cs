// Adler32 originally by Singyuen Yip
using System;
using System.IO;
using System.Text;

namespace FreeMote
{
    /// <summary>
    /// A class that can be used to compute the Adler-32 checksum of a data
    /// stream. An Adler-32 checksum is almost as reliable as a CRC-32 but
    /// can be computed much faster.
    /// <create>1.00 12/25/2009</create>
    /// <version>1.02 01/22/2018</version>
    /// <author>Singyuen Yip / Ulysses</author>
    /// </summary>
    /// http://blog.163.com/light_warm/blog/static/3168104201001195634824/
    public class Adler32
    {
        // largest prime smaller than 65536
        private const int BASE = 65521;
        // NMAX is the largest n such that 255n(n+1)/2 + (n+1)(BASE-1) <= 2^32-1
        private const int NMAX = 5552;
        private long adler = 1;

        /// <summary>
        /// The checksum value.
        /// </summary>
        public long Checksum
        {
            get { return adler; }
        }

        /// <summary>
        /// Update checksum with stream.
        /// </summary>
        /// <param name="stream"></param>
        public void Update(Stream stream)
        {
            byte[] bytes = new byte[128 * 1024]; //128KB for each round
            using (BinaryReader br = new BinaryReader(stream, Encoding.UTF8, true))
            {
                while (stream.Length - stream.Position > 0)
                {
                    var readCount = br.Read(bytes, 0, bytes.Length);
                    if (readCount == 0)
                    {
                        break;
                    }
                    adler = UpdateBytes(adler, bytes, 0, readCount);
                }
            }
        }

        /// <summary>
        /// Updates checksum with specified byte.
        /// </summary>
        /// <param name="b">an array of bytes</param>
        public void Update(int b)
        {
            adler = UpdateByte(adler, b);
        }

        /// <summary>
        /// Updates checksum with specified array of bytes.
        /// </summary>
        /// <param name="buf">the byte array to update the checksum with</param>
        public void Update(byte[] buf)
        {
            adler = UpdateBytes(adler, buf, 0, buf.Length);
        }

        /// <summary>
        /// Updates checksum with specified array of bytes.
        /// </summary>
        /// <param name="b">the byte array to update the checksum with</param>
        /// <param name="offset">the start offset of the data</param>
        /// <param name="length">the number of bytes to use for the update</param>
        public void Update(byte[] b, int offset, int length)
        {
            if (null == b)
            {
                throw new NullReferenceException();
            }
            if (offset < 0 || length < 0 || offset > b.Length - length)
            {
                throw new IndexOutOfRangeException();
            }
            adler = UpdateBytes(adler, b, offset, length);
        }

        /// <summary>
        /// Resets the checksum to its initial value.
        /// </summary>
        public void Reset()
        {
            adler = 1;
        }

        /// <summary>
        /// Computing the checksum based on the Adler Algorithm.
        /// </summary>
        /// <param name="adler">the former checksum</param>
        /// <param name="buf">the byte array</param>
        /// <param name="index">the start offset of the data</param>
        /// <param name="len">the number of bytes to use for the update</param>
        /// <returns>the new checksum</returns>
        private long UpdateBytes(long adler, byte[] buf, int index, int len)
        {
            if (buf == null) { return 1L; }

            long s1 = adler & 0xffff;
            long s2 = (adler >> 16) & 0xffff;
            int k;

            while (len > 0)
            {
                k = len < NMAX ? len : NMAX;
                len -= k;
                while (k >= 16)
                {
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    k -= 16;
                }
                if (k != 0)
                {
                    do
                    {
                        s1 += buf[index++] & 0xff; s2 += s1;
                    }
                    while (--k != 0);
                }
                s1 %= BASE;
                s2 %= BASE;
            }
            return (s2 << 16) | s1;
        }

        /// <summary>
        /// Update just one byte
        /// <para><b>since 1.01</b></para>
        /// </summary>
        /// <param name="adler">the former checksum</param>
        /// <param name="b">the byte in the integer</param>
        /// <returns>the new checksum</returns>
        private long UpdateByte(long adler, int b)
        {
            long s1 = adler & 0xffff;
            long s2 = adler >> 16;

            s1 += (byte)b;
            s1 %= BASE;
            s2 += s1;
            s2 %= BASE;

            return (s2 << 16) | s1;
        }
    }
}

