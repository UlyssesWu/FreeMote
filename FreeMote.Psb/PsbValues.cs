using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

// ReSharper disable InconsistentNaming

namespace FreeMote.Psb
{
    public enum PsbObjType : byte
    {
        None = 0x0,
        Null = 0x1,
        False = 0x2,
        True = 0x3,

        //int
        NumberN0 = 0x4,
        NumberN1 = 0x5,
        NumberN2 = 0x6,
        NumberN3 = 0x7,
        NumberN4 = 0x8,
        NumberN5 = 0x9,
        NumberN6 = 0xA,
        NumberN7 = 0xB,
        NumberN8 = 0xC,

        //array N(NUMBER) is count mask
        ArrayN1 = 0xD,
        ArrayN2 = 0xE,
        ArrayN3 = 0xF,
        ArrayN4 = 0x10,
        ArrayN5 = 0x11,
        ArrayN6 = 0x12,
        ArrayN7 = 0x13,
        ArrayN8 = 0x14,

        //index of strings table
        StringN1 = 0x15,
        StringN2 = 0x16,
        StringN3 = 0x17,
        StringN4 = 0x18,

        //resource of thunk
        ResourceN1 = 0x19,
        ResourceN2 = 0x1A,
        ResourceN3 = 0x1B,
        ResourceN4 = 0x1C,

        //fpu value
        Float0 = 0x1D,
        Float = 0x1E,
        Double = 0x1F,

        //objects
        List = 0x20, //object list
        Objects = 0x21, //object dictionary

        ExtraChunkN1 = 0x22,
        ExtraChunkN2 = 0x23,
        ExtraChunkN3 = 0x24,
        ExtraChunkN4 = 0x25,

        //used by compiler,it's fake
        Integer = 0x80,
        String = 0x81,
        Resource = 0x82,
        Decimal = 0x83,
        Array = 0x84,
        Boolean = 0x85,
        BTree = 0x86,
    };

    /// <summary>
    /// Contained by a <see cref="IPsbCollection"/>
    /// </summary>
    public interface IPsbChild : IPsbValue
    {
        /// <summary>
        /// <see cref="IPsbCollection"/> which contain this
        /// </summary>
        IPsbCollection Parent { get; set; }

        string Path { get; }
    }


    /// <summary>
    /// Contained by more than one <see cref="IPsbCollection"/>
    /// </summary>
    public interface IPsbSingleton
    {
        /// <summary>
        /// <see cref="IPsbCollection"/>s which contain this
        /// </summary>
        List<IPsbCollection> Parents { get; set; }
    }

    /// <summary>
    /// List or Dictionary
    /// </summary>
    public interface IPsbCollection : IPsbChild, IEnumerable
    {
        IPsbValue this[int i] { get; }
        IPsbValue this[string s] { get; }
    }

    /// <summary>
    /// Directly write as bytes
    /// </summary>
    public interface IPsbWrite
    {
        void WriteTo(BinaryWriter bw);
    }

    /// <summary>
    /// Tracked by index
    /// </summary>
    public interface IPsbIndexed
    {
        uint? Index { get; set; }
        PsbObjType Type { get; }
    }

    /// <summary>
    /// PSB Entry
    /// </summary>
    public interface IPsbValue
    {
        PsbObjType Type { get; }
        string ToString();
    }

    /// <summary>
    /// Null: Reference type
    /// </summary>
    [Serializable]
    public class PsbNull : IPsbValue, IPsbWrite
    {
        /// <summary>
        /// Use <see cref="Null"/> to avoid duplicated null
        /// </summary>
        private PsbNull()
        {
        }

        public object Value => null;

        public PsbObjType Type { get; } = PsbObjType.Null;

        public override string ToString()
        {
            return "null";
        }

        public void WriteTo(BinaryWriter bw)
        {
            bw.Write((byte) Type);
        }

        /// <summary>
        /// Null
        /// </summary>
        public static readonly PsbNull Null = new PsbNull();
    }

    /// <summary>
    /// Bool: Value type
    /// </summary>
    [Serializable]
    public class PsbBool : IPsbValue, IPsbWrite
    {
        public PsbBool(bool value = false)
        {
            Value = value;
        }

        public bool Value { get; set; } = false;

        public PsbObjType Type => Value ? PsbObjType.True : PsbObjType.False;

        public override string ToString()
        {
            return Value.ToString();
        }

        public void WriteTo(BinaryWriter bw)
        {
            bw.Write((byte) Type);
        }
    }

    public enum PsbNumberType
    {
        Int,
        Long,
        Float,
        Double,
    }

    /// <summary>
    /// Number: Value type
    /// </summary>
    [Serializable]
    public class PsbNumber : IPsbValue, IPsbWrite
    {
        /// <summary>
        /// PsbNumber: (int) 0
        /// </summary>
        public static PsbNumber Zero => new PsbNumber(0); //always create a new PsbNumber to prevent value modified!

        internal PsbNumber(PsbObjType objType, BinaryReader br)
        {
            switch (objType)
            {
                case PsbObjType.NumberN0:
                case PsbObjType.NumberN1:
                case PsbObjType.NumberN2:
                case PsbObjType.NumberN3:
                case PsbObjType.NumberN4:
                    NumberType = PsbNumberType.Int;
                    Data = new byte[4];
                    break;
                case PsbObjType.NumberN5:
                case PsbObjType.NumberN6:
                case PsbObjType.NumberN7:
                case PsbObjType.NumberN8:
                    NumberType = PsbNumberType.Long;
                    Data = new byte[8];
                    break;
                case PsbObjType.Float0:
                case PsbObjType.Float:
                    NumberType = PsbNumberType.Float;
                    break;
                case PsbObjType.Double:
                    NumberType = PsbNumberType.Double;
                    break;
                default:
                    break;
                //throw new ArgumentOutOfRangeException(nameof(objType), objType, null);
            }

            switch (objType)
            {
                case PsbObjType.NumberN0:
                    IntValue = 0;
                    return;
                case PsbObjType.NumberN1:
                    br.ReadAndUnzip(1, Data);
                    return;
                case PsbObjType.NumberN2:
                    br.ReadAndUnzip(2, Data);
                    return;
                case PsbObjType.NumberN3:
                    br.ReadAndUnzip(3, Data);
                    return;
                case PsbObjType.NumberN4:
                    br.ReadAndUnzip(4, Data);
                    return;
                case PsbObjType.NumberN5:
                    br.ReadAndUnzip(5, Data);
                    return;
                case PsbObjType.NumberN6:
                    br.ReadAndUnzip(6, Data);
                    return;
                case PsbObjType.NumberN7:
                    br.ReadAndUnzip(7, Data);
                    return;
                case PsbObjType.NumberN8:
                    br.ReadAndUnzip(8, Data);
                    return;
                case PsbObjType.Float0:
                    Data = BitConverter.GetBytes(0.0f);
                    return;
                case PsbObjType.Float:
                    Data = br.ReadBytes(4);
                    return;
                case PsbObjType.Double:
                    Data = br.ReadBytes(8);
                    return;
            }
        }

        public PsbNumber()
        {
            Data = new byte[8];
            NumberType = PsbNumberType.Long;
        }

        public PsbNumber(byte[] data, PsbNumberType type)
        {
            Data = data;
            NumberType = type;
        }

        /// <summary>
        /// Int Number
        /// </summary>
        /// <param name="val"></param>
        public PsbNumber(int val)
        {
            NumberType = PsbNumberType.Int;
            IntValue = val;
        }

        /// <summary>
        /// Float Number
        /// </summary>
        /// <param name="val"></param>
        public PsbNumber(float val)
        {
            NumberType = PsbNumberType.Float;
            FloatValue = val;
        }

        /// <summary>
        /// Double Number
        /// </summary>
        /// <param name="val"></param>
        public PsbNumber(double val)
        {
            NumberType = PsbNumberType.Double;
            DoubleValue = val;
        }

        /// <summary>
        /// UInt Number (only used in Compiler)
        /// </summary>
        /// <param name="val"></param>
        public PsbNumber(uint val)
        {
            NumberType = PsbNumberType.Long;
            LongValue = val;
        }

        public PsbNumber(long val)
        {
            NumberType = PsbNumberType.Long;
            LongValue = val;
        }

        public byte[] Data { get; set; }

        public PsbNumberType NumberType { get; set; }

        public ValueType Value
        {
            get
            {
                switch (NumberType)
                {
                    case PsbNumberType.Int:
                        return IntValue;
                    case PsbNumberType.Float:
                        return FloatValue;
                    case PsbNumberType.Double:
                        return DoubleValue;
                    case PsbNumberType.Long:
                        return LongValue;
                    default:
                        return LongValue;
                }
            }
        }

        /// <summary>
        /// When set, change number type to Int
        /// </summary>
        public int AsInt
        {
            get
            {
                switch (NumberType)
                {
                    case PsbNumberType.Int:
                        return IntValue;
                    case PsbNumberType.Float:
                        return (int) FloatValue;
                    case PsbNumberType.Double:
                        return (int) DoubleValue;
                    case PsbNumberType.Long:
                        return (int) LongValue;
                    default:
                        return (int) LongValue;
                }
            }
            set
            {
                NumberType = PsbNumberType.Int;
                IntValue = value;
            }
        }

        public long AsLong
        {
            get
            {
                switch (NumberType)
                {
                    case PsbNumberType.Int:
                        return IntValue;
                    case PsbNumberType.Float:
                        return (long)FloatValue;
                    case PsbNumberType.Double:
                        return (long)DoubleValue;
                    case PsbNumberType.Long:
                        return LongValue;
                    default:
                        return LongValue;
                }
            }
            set
            {
                NumberType = PsbNumberType.Long;
                LongValue = value;
            }
        }

        public float AsFloat
        {
            get
            {
                switch (NumberType)
                {
                    case PsbNumberType.Int:
                        return (float) IntValue;
                    case PsbNumberType.Float:
                        return FloatValue;
                    case PsbNumberType.Double:
                        return (float) DoubleValue;
                    case PsbNumberType.Long:
                        return (float) LongValue;
                    default:
                        return (float) LongValue;
                }
            }
            set
            {
                NumberType = PsbNumberType.Float;
                FloatValue = value;
            }
        }

        public double AsDouble
        {
            get
            {
                switch (NumberType)
                {
                    case PsbNumberType.Int:
                        return (double) IntValue;
                    case PsbNumberType.Float:
                        return (double) FloatValue;
                    case PsbNumberType.Double:
                        return DoubleValue;
                    case PsbNumberType.Long:
                        return (double) LongValue;
                    default:
                        return (double) LongValue;
                }
            }
            set
            {
                NumberType = PsbNumberType.Double;
                DoubleValue = value;
            }
        }

        public int IntValue
        {
            get => BitConverter.ToInt32(Data, 0);
            set => Data = BitConverter.GetBytes(value);
        }

        public uint UIntValue
        {
            get => BitConverter.ToUInt32(Data, 0);
            set => Data = BitConverter.GetBytes(value);
        }

        public float FloatValue
        {
            get => BitConverter.ToSingle(Data, 0);
            set => Data = BitConverter.GetBytes(value);
        }

        public double DoubleValue
        {
            get => Data.Length < 8 ? BitConverter.ToSingle(Data, 0) : BitConverter.ToDouble(Data, 0);
            set => Data = BitConverter.GetBytes(value);
        }

        public long LongValue
        {
            get => Data.Length < 8 ? BitConverter.ToInt32(Data, 0) : BitConverter.ToInt64(Data, 0);
            set => Data = BitConverter.GetBytes(value);
        }

        public bool IsNumber32 => Type == PsbObjType.NumberN0 || Type == PsbObjType.NumberN1 ||
                                  Type == PsbObjType.NumberN2 || Type == PsbObjType.NumberN3 ||
                                  Type == PsbObjType.NumberN4;

        public static explicit operator int(PsbNumber p)
        {
            switch (p.NumberType)
            {
                case PsbNumberType.Float:
                    return (int) p.FloatValue;
                case PsbNumberType.Double:
                    return (int) p.DoubleValue;
                case PsbNumberType.Long:
                    return (int) p.LongValue;
                case PsbNumberType.Int:
                default:
                    return p.IntValue;
            }
        }

        public static explicit operator long(PsbNumber p)
        {
            switch (p.NumberType)
            {
                case PsbNumberType.Float:
                    return (long)p.FloatValue;
                case PsbNumberType.Double:
                    return (long)p.DoubleValue;
                case PsbNumberType.Int:
                    return p.IntValue;
                case PsbNumberType.Long:
                default:
                    return p.LongValue;
            }
        }

        public static explicit operator float(PsbNumber p)
        {
            switch (p.NumberType)
            {
                case PsbNumberType.Int:
                    return (float) p.IntValue;
                case PsbNumberType.Double:
                    return (float) p.DoubleValue;
                case PsbNumberType.Long:
                    return (float) p.LongValue;
                case PsbNumberType.Float:
                default:
                    return p.FloatValue;
            }
        }

        public static explicit operator double(PsbNumber p)
        {
            switch (p.NumberType)
            {
                case PsbNumberType.Int:
                    return (double) p.IntValue;
                case PsbNumberType.Float:
                    return (double) p.FloatValue;
                case PsbNumberType.Long:
                    return (double) p.LongValue;
                case PsbNumberType.Double:
                default:
                    return p.DoubleValue;
            }
        }

        public static implicit operator PsbNumber(int n)
        {
            return new PsbNumber(n);
        }

        public static implicit operator PsbNumber(float n)
        {
            return new PsbNumber(n);
        }

        public static implicit operator PsbNumber(double n)
        {
            return new PsbNumber(n);
        }

        public static implicit operator PsbNumber(long n)
        {
            return new PsbNumber(n);
        }

        public PsbObjType Type
        {
            get
            {
                switch (NumberType)
                {
                    case PsbNumberType.Int:
                    case PsbNumberType.Long:
                        switch (LongValue.GetSize())
                        {
                            case 0:
                                return PsbObjType.NumberN0;
                            case 1:
                                if (LongValue == 0L)
                                {
                                    return PsbObjType.NumberN0;
                                }

                                return PsbObjType.NumberN1;
                            case 2:
                                return PsbObjType.NumberN2;
                            case 3:
                                return PsbObjType.NumberN3;
                            case 4:
                                return PsbObjType.NumberN4;
                            case 5:
                                return PsbObjType.NumberN5;
                            case 6:
                                return PsbObjType.NumberN6;
                            case 7:
                                return PsbObjType.NumberN7;
                            case 8:
                                return PsbObjType.NumberN8;
                            default:
                                throw new ArgumentOutOfRangeException("Not a valid Integer");
                        }

                    case PsbNumberType.Float:
                        //TODO: Float0 or not
                        if (Math.Abs(FloatValue) < float.Epsilon) //should we just use 0?
                        {
                            return PsbObjType.Float0;
                        }

                        return PsbObjType.Float;
                    case PsbNumberType.Double:
                        return PsbObjType.Double;
                    default:
                        throw new ArgumentOutOfRangeException("Unknown number type");
                }
            }
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public void WriteTo(BinaryWriter bw)
        {
            bw.Write((byte) Type);
            switch (NumberType)
            {
                case PsbNumberType.Int:
                    if (Type != PsbObjType.NumberN0)
                    {
                        bw.Write(IsNumber32 ? IntValue.ZipNumberBytes() : LongValue.ZipNumberBytes());
                    }
                    break;
                case PsbNumberType.Long:
                    bw.Write(LongValue.ZipNumberBytes());
                    break;
                case PsbNumberType.Float:
                    if (Type != PsbObjType.Float0)
                    {
                        bw.Write(FloatValue);
                    }

                    break;
                case PsbNumberType.Double:
                    bw.Write(DoubleValue);
                    break;
                default:
                    bw.Write(Data);
                    break;
            }
        }

        public byte[] ToBytes()
        {
            switch (NumberType)
            {
                case PsbNumberType.Int:
                    if (Type != PsbObjType.NumberN0)
                    {
                        return IsNumber32 ? IntValue.ZipNumberBytes() : LongValue.ZipNumberBytes();
                    }

                    return new byte[0];
                case PsbNumberType.Long:
                    return LongValue.ZipNumberBytes();
                case PsbNumberType.Float:
                    if (Type != PsbObjType.Float0)
                    {
                        return BitConverter.GetBytes(FloatValue);
                    }

                    return new byte[0];
                case PsbNumberType.Double:
                    return BitConverter.GetBytes(DoubleValue);
                default:
                    return Data;
            }
        }
    }

    /// <summary>
    /// uint[]: Value type
    /// </summary>
    [Serializable]
    public class PsbArray : IPsbValue, IPsbWrite
    {
        internal static List<uint> LoadIntoList(int n, BinaryReader br)
        {
            if (n < 0 || n > 8)
            {
                throw new PsbBadFormatException(PsbBadFormatReason.Array);
            }

            uint count = br.ReadBytes(n).UnzipUInt();
            if (count > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException("Long array is not supported yet");
            }

            var entryLength = (byte) (br.ReadByte() - PsbObjType.NumberN8);
            var list = new List<uint>((int) count);
            //for (int i = 0; i < count; i++)
            //{
            //    list.Add(br.ReadBytes(entryLength).UnzipUInt());
            //}

            var shouldBeLength = entryLength * (int) count;
            var buffer = ArrayPool<byte>.Shared.Rent(shouldBeLength);

            br.Read(buffer, 0, shouldBeLength); //WARN: the actual buffer.Length >= shouldBeLength
            for (int i = 0; i < count; i++)
            {
                list.Add(buffer.UnzipUInt(i * entryLength, entryLength));
            }

            ArrayPool<byte>.Shared.Return(buffer);

            return list;
        }

        internal PsbArray(int n, BinaryReader br)
        {
            if (n < 0 || n > 8)
            {
                throw new PsbBadFormatException(PsbBadFormatReason.Array);
            }

            uint count = br.ReadBytes(n).UnzipUInt();
            if (count > int.MaxValue)
            {
                throw new PsbBadFormatException(PsbBadFormatReason.Array, innerException: new ArgumentOutOfRangeException("Long array is not supported yet"));
            }

            EntryLength = (byte) (br.ReadByte() - PsbObjType.NumberN8);
            Value = new List<uint>((int) count);
            //for (int i = 0; i < count; i++)
            //{
            //    Value.Add(br.ReadBytes(EntryLength).UnzipUInt());
            //}

            var shouldBeLength = EntryLength * (int) count;
            var buffer = ArrayPool<byte>.Shared.Rent(shouldBeLength);

            br.Read(buffer, 0, shouldBeLength); //WARN: the actual buffer.Length >= shouldBeLength
            for (int i = 0; i < count; i++)
            {
                Value.Add(buffer.UnzipUInt(i * EntryLength, EntryLength));
            }

            ArrayPool<byte>.Shared.Return(buffer);
        }

        public PsbArray()
        {
            Value = new List<uint>();
        }

        public PsbArray(List<uint> array)
        {
            Value = array;
            GetEntryLength();
        }

        public uint this[int index]
        {
            get => Value[index];
            set => Value[index] = value;
        }

        public byte EntryLength { get; private set; } = 4;
        public List<uint> Value { get; }

        public PsbObjType Type
        {
            get
            {
                switch (Value.Count.GetSize())
                {
                    case 0:
                    case 1:
                        return PsbObjType.ArrayN1;
                    case 2:
                        return PsbObjType.ArrayN2;
                    case 3:
                        return PsbObjType.ArrayN3;
                    case 4:
                        return PsbObjType.ArrayN4;
                    case 5:
                        return PsbObjType.ArrayN5;
                    case 6:
                        return PsbObjType.ArrayN6;
                    case 7:
                        return PsbObjType.ArrayN7;
                    case 8:
                        return PsbObjType.ArrayN8;
                    default:
                        throw new ArgumentOutOfRangeException("Not a valid array");
                }
            }
        }

        public override string ToString()
        {
            return $"Array[{Value.Count}]";
        }

        private byte GetEntryLength()
        {
            if (Value == null || Value.Count <= 0)
            {
                return 0;
            }

            var maxSize = Value.Max(u => u.GetSize());
            if (maxSize > 8)
            {
                maxSize = 8;
            }

            EntryLength = (byte) maxSize;
            return EntryLength;
        }

        public void WriteTo(BinaryWriter bw)
        {
            bw.Write((byte) Type); //Type
            bw.Write(Value.Count.ZipNumberBytes(Value.Count.GetSize())); //Count
            bw.Write((byte) (GetEntryLength() + (byte) PsbObjType.NumberN8)); //FIXED: EntryLength is added by 0xC
            foreach (var u in Value)
            {
                bw.Write(u.ZipNumberBytes(EntryLength));
            }
        }
    }

    /// <summary>
    /// Detect if byte segment is a <see cref="PsbArray"/>
    /// </summary>
    public class PsbArrayDetector : IPsbValue
    {
        /// <summary>
        /// Check if this byte is a PsbArray type byte
        /// </summary>
        /// <param name="b"></param>
        /// <param name="maxSize">max ArrayN allowed</param>
        /// <returns></returns>
        public static bool IsPsbArrayType(byte b, int maxSize = 4)
            => b >= (byte) PsbObjType.ArrayN1 && b <= (byte) PsbObjType.ArrayN1 + maxSize;

        private const int MaxIntSize = 4;
        public bool IsArray { get; private set; }
        public int N { get; }
        public long Position { get; set; }

        /// <summary>
        /// First Element
        /// </summary>
        public uint First { get; set; }

        public int Count { get; set; }
        public int EntryLength { get; set; }

        public PsbObjType Type { get; private set; }
        public int Size => 1 + N + 1 + Count * EntryLength;

        public PsbArrayDetector(BinaryReader br)
        {
            IsArray = false;
            Position = br.BaseStream.Position;
            var remainingLength = br.BaseStream.Length - br.BaseStream.Position;

            //Parse Type
            var type = br.ReadByte();
            if (Enum.IsDefined(typeof(PsbObjType), type))
            {
                Type = (PsbObjType) type;
            }
            else
            {
                Type = PsbObjType.None;
            }

            if (type < (byte) PsbObjType.ArrayN1 || type > (byte) PsbObjType.ArrayN1 + MaxIntSize)
            {
                return;
            }

            //Parse N
            N = type - (byte) PsbObjType.ArrayN1 + 1;
            if (N < 0 || N > MaxIntSize)
            {
                return;
            }

            //Parse Count
            var count = br.ReadBytes(N).UnzipUInt();
            if (count > int.MaxValue)
            {
                return;
            }

            Count = (int) count;

            //Parse EntryLength
            EntryLength = (byte) (br.ReadByte() - PsbObjType.NumberN8);
            if (EntryLength < 0 || EntryLength > MaxIntSize)
            {
                return;
            }

            if (Count > 0)
            {
                First = br.ReadBytes(EntryLength).UnzipUInt();
            }

            if (Size > remainingLength) //False positive
            {
                return;
            }

            IsArray = true;
        }

        public PsbArray ToPsbArray(BinaryReader br)
        {
            if (IsArray)
            {
                //var p = Position;
                //var n = N;
                br.BaseStream.Seek(Position + 1, SeekOrigin.Begin);
                return new PsbArray(N, br);
            }

            return null;
        }

        public override string ToString()
        {
            if (IsArray)
            {
                return $"Array[{Count}]{{{First}...}}";
            }

            return Type.ToString();
        }
    }

    /// <summary>
    /// String: Reference type
    /// </summary>
    [Serializable]
    [DebuggerDisplay("{Value}(#{Index})")]
    public class PsbString : IPsbValue, IPsbIndexed, IPsbWrite
    {
        /// <summary>
        /// new empty PsbString ""
        /// </summary>
        public static PsbString Empty => new PsbString();

        internal PsbString(int n, BinaryReader br)
        {
            Index = br.ReadCompactUInt((byte) n);
        }

        public PsbString(string value = "", uint? index = null)
        {
            Value = value;
            Index = index;
        }

        /// <summary>
        /// Update index when compile
        /// </summary>
        public uint? Index { get; set; }

        public string Value { get; set; }

        /// <summary>
        /// It's based on index...
        /// </summary>
        public PsbObjType Type
        {
            get
            {
                var size = Index?.GetSize() ?? 0.GetSize();
                switch (size)
                {
                    case 0:
                    case 1:
                        return PsbObjType.StringN1;
                    case 2:
                        return PsbObjType.StringN2;
                    case 3:
                        return PsbObjType.StringN3;
                    case 4:
                        return PsbObjType.StringN4;
                    default:
                        throw new ArgumentOutOfRangeException("size", size, "String index has wrong size");
                }
            }
        }

        public override string ToString()
        {
            return Value;
        }

        public static implicit operator string(PsbString s)
        {
            return s.Value;
        }

        public static explicit operator PsbString(string s)
        {
            return new PsbString(s);
        }

        public static bool operator ==(PsbString s1, PsbString s2)
        {
            if (s1 is null)
            {
                return s2 is null;
            }

            return s1.Equals(s2);
        }

        public static bool operator !=(PsbString s1, PsbString s2)
        {
            return !(s1 == s2);
        }

        public static bool operator ==(PsbString s1, string s2)
        {
            return s1?.Value == s2;
        }

        public static bool operator !=(PsbString s1, string s2)
        {
            return !(s1 == s2);
        }

        public static bool operator ==(string s1, PsbString s2)
        {
            return s1 == s2?.Value;
        }

        public static bool operator !=(string s1, PsbString s2)
        {
            return !(s1 == s2);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PsbString) obj);
        }

        protected bool Equals(PsbString other)
        {
            if (other == null)
            {
                return false;
            }

            return Index == other.Index && string.Equals(Value, other.Value);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Index.GetHashCode() * 397) ^ (Value != null ? Value.GetHashCode() : 0);
            }
        }

        public void WriteTo(BinaryWriter bw)
        {
            bw.Write((byte) Type);
            if (Index == null)
            {
                throw new ArgumentNullException("Index", "Index can not be null when writing");
            }

            bw.Write(Index.Value.ZipNumberBytes()); //FIXED:
            //new PsbNumber(Index ?? 0u).WriteTo(bw); //Wrong because it writes number type
        }
    }

    /// <summary>
    /// psb_objects_t: {key: value}
    /// </summary>
    [Serializable]
    public class PsbDictionary : Dictionary<string, IPsbValue>, IPsbCollection
    {
        public PsbDictionary(int capacity) : base(capacity)
        {
        }

        public PsbDictionary() : base()
        {
        }

        public Dictionary<string, IPsbValue> Value => this;

        public IPsbCollection Parent { get; set; } = null;

        public string Path
        {
            get
            {
                if (Parent != null)
                {
                    var parentPath = Parent.Path;
                    return $"{parentPath}{(parentPath.EndsWith("/") ? "" : "/")}{this.GetName() ?? "*"}";
                }

                return "/";
            }
        }

        IPsbValue IPsbCollection.this[int i] => ContainsKey(i.ToString()) ? base[i.ToString()] : null;

        public new IPsbValue this[string index]
        {
            get => TryGetValue(index, out IPsbValue val) ? val : null;
            set => base[index] = value;
        }

        public PsbObjType Type { get; } = PsbObjType.Objects;

        public void UnionWith(PsbDictionary dic)
        {
            foreach (var key in dic.Keys)
            {
                if (ContainsKey(key))
                {
                    if (this[key] is PsbDictionary childDic && dic[key] is PsbDictionary otherDic)
                    {
                        childDic.UnionWith(otherDic);
                    }
                }
                else
                {
                    Add(key, dic[key]);
                }
            }
        }

        public override string ToString()
        {
            return $"Dictionary[{Count}]";
        }
    }

    /// <summary>
    /// [value1, value2...]
    /// </summary>
    [Serializable]
    public class PsbList : List<IPsbValue>, IPsbCollection
    {
        public PsbList(int capacity) : base(capacity)
        {
        }

        public PsbList() : base()
        {
        }

        public List<IPsbValue> Value => this;

        public IPsbCollection Parent { get; set; } = null;

        public string Path => Parent != null
            ? $"{Parent.Path}{(Parent.Path.EndsWith("/") ? "" : "/")}{this.GetName() ?? "(array)"}"
            : "/";

        public new IPsbValue this[int index]
        {
            get => index < Count ? base[index] : null;
            set => base[index] = value;
        }

        IPsbValue IPsbCollection.this[string s] =>
            int.TryParse(s.Replace("[", "").Replace("]", ""), out int i) ? base[i] : null;

        public PsbObjType Type { get; } = PsbObjType.List;

        public override string ToString()
        {
            return $"List[{Count}]";
        }
    }

    /// <summary>
    /// Resource: Reference type
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Resource[{Data?.Length}]({" + nameof(IdentifierCharString) + ",nq}{" + nameof(Index) + "})")]
    public class PsbResource : IPsbValue, IPsbIndexed, IPsbWrite, IPsbSingleton
    {
        internal PsbResource(int n, BinaryReader br)
        {
            Index = br.ReadCompactUInt((byte) n);
        }

        private char IdentifierChar => IsExtra ? Consts.ExtraResourceIdentifierChar : Consts.ResourceIdentifierChar;

        private string IdentifierCharString => IdentifierChar.ToString();


        internal string ResourceIdentifier => IsExtra ? Consts.ExtraResourceIdentifier : Consts.ResourceIdentifier;

        public PsbResource(uint? index = null, bool isExtra = false)
        {
            Index = index;
            IsExtra = isExtra;
        }

        public bool IsExtra { get; set; } = false;

        /// <summary>
        /// Update index when compile
        /// </summary>
        public uint? Index { get; set; }

        public byte[] Data { get; set; } = new byte[0];

        public PsbObjType Type
        {
            get
            {
                //var size = Index?.GetSize() ?? 0.GetSize();
                var size = Index?.GetSize() ?? 0;
                switch (size)
                {
                    case 0:
                    case 1:
                        return IsExtra ? PsbObjType.ExtraChunkN1 : PsbObjType.ResourceN1;
                    case 2:
                        return IsExtra ? PsbObjType.ExtraChunkN2 : PsbObjType.ResourceN2;
                    case 3:
                        return IsExtra ? PsbObjType.ExtraChunkN3 : PsbObjType.ResourceN3;
                    case 4:
                        return IsExtra ? PsbObjType.ExtraChunkN4 : PsbObjType.ResourceN4;
                    default:
                        throw new ArgumentOutOfRangeException("Index", "Not a valid resource");
                }
            }
        }

        public override string ToString()
        {
            return $"{(IsExtra? Consts.ExtraResourceIdentifier : Consts.ResourceIdentifier)}{Index}";
        }

        public void WriteTo(BinaryWriter bw)
        {
            bw.Write((byte) Type);
            if (Index == null)
            {
                throw new ArgumentNullException("Index", "Index can not be null when writing");
            }

            bw.Write(Index.Value.ZipNumberBytes()); //FIXED:
        }

        public List<IPsbCollection> Parents { get; set; } = new List<IPsbCollection>();
    }
}