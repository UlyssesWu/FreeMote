using System;
using System.IO;
using System.Linq;
using System.Text;

namespace FreeMote.Psb
{
    internal static class PsbHelper
    {
        public static int GetSize(this int i)
        {
            bool neg = false;
            if (i < 0)
            {
                neg = true;
                i = Math.Abs(i);
            }
            var l = i.ToString("X").Length;
            if (l % 2 != 0)
            {
                l++;
            }
            l = l / 2;
            if (neg)
            {
                l++;
            }
            return l;
        }

        public static int GetSize(this uint i)
        {
            var l = i.ToString("X").Length;
            if (l % 2 != 0)
            {
                l++;
            }
            l = l / 2;
            return l;
        }

        public static byte[] UnzipNumberBytes(this byte[] b, int size = 8, bool unsigned = false)
        {
            byte[] r = new byte[size];
            if (!unsigned && (b.Last() >= 0b10000000)) //b.Last() == 0xFF
            {
                for (int i = 0; i < size; i++)
                {
                    r[i] = 0xFF;
                }
                b.CopyTo(r, 0);
            }
            else
            {
                b.CopyTo(r, 0);
            }
            return r;
        }

        public static long UnzipNumber(this byte[] b)
        {
            return BitConverter.ToInt64(b.UnzipNumberBytes(), 0);
        }

        public static uint UnzipUInt(this byte[] b)
        {
            return BitConverter.ToUInt32(b.UnzipNumberBytes(4, true), 0);
        }

        public static string ReadStringZeroTrim(this BinaryReader br)
        {
            StringBuilder sb = new StringBuilder();
            while (br.PeekChar() != 0)
            {
                sb.Append(br.ReadChar());
            }
            return sb.ToString();
        }

    }


}
