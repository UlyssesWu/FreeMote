using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FreeMote
{
    public static class PsbConstants
    {
        /// <summary>
        /// 0x075BCD15
        /// </summary>
        public const uint Key1 = 123456789;
        /// <summary>
        /// 0x159A55E5
        /// </summary>
        public const uint Key2 = 362436069;
        /// <summary>
        /// 0x1F123BB5
        /// </summary>
        public const uint Key3 = 521288629;

        /// <summary>
        /// Read a <see cref="uint"/> from <see cref="BinaryReader"/>, and then encode using <see cref="PsbStreamContext"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="br"></param>
        /// <returns></returns>
        public static uint ReadUInt32(this PsbStreamContext context, BinaryReader br)
        {
            return BitConverter.ToUInt32(context.Encode(br.ReadBytes(4)), 0);
        }

        /// <summary>
        /// Read a <see cref="byte[]"/> from <see cref="BinaryReader"/>, and then encode using <see cref="PsbStreamContext"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="br"></param>
        /// <returns></returns>
        public static byte[] ReadBytes(this PsbStreamContext context, BinaryReader br, int count)
        {
            return context.Encode(br.ReadBytes(count));
        }

        /// <summary>
        /// Read a <see cref="ushort"/> from <see cref="BinaryReader"/>, and then encode using <see cref="PsbStreamContext"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="br"></param>
        /// <returns></returns>
        public static ushort ReadUInt16(this PsbStreamContext context, BinaryReader br)
        {
            return BitConverter.ToUInt16(context.Encode(br.ReadBytes(2)), 0);
        }

        /// <summary>
        /// Encode a value and write using <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="value"></param>
        /// <param name="bw"></param>
        public static void Write(this PsbStreamContext context, uint value, BinaryWriter bw)
        {
            bw.Write(context.Encode(BitConverter.GetBytes(value)));
        }

        /// <summary>
        /// Encode a value and write using <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="value"></param>
        /// <param name="bw"></param>
        public static void Write(this PsbStreamContext context, ushort value, BinaryWriter bw)
        {
            bw.Write(context.Encode(BitConverter.GetBytes(value)));
        }
    }
}
