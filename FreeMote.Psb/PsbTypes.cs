using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

// ReSharper disable InconsistentNaming

namespace FreeMote.Psb
{
    public enum PsbType : byte
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
        Collection = 0x20, //object collection
        Objects = 0x21,    //object dictionary


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
    public interface IPsbChild
    {
        /// <summary>
        /// <see cref="IPsbCollection"/> which contain this
        /// </summary>
        IPsbCollection Parent { get; set; }
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

    /// <inheritdoc />
    /// <summary>
    /// Collection
    /// </summary>
    public interface IPsbCollection : IPsbChild, IEnumerable
    {
        IPsbCollection Parent { get; }
        IPsbValue this[int i]
        {
            get;
        }
        IPsbValue this[string s]
        {
            get;
        }
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
        PsbType Type { get; }
    }

    /// <summary>
    /// PSB Entry
    /// </summary>
    public interface IPsbValue
    {
        PsbType Type { get; }
        string ToString();
    }

    [Serializable]
    public class PsbNull : IPsbValue, IPsbWrite
    {
        /// <summary>
        /// Use <see cref="Null"/> to avoid duplicated null
        /// </summary>
        internal PsbNull() { }

        public object Value => null;

        public PsbType Type { get; } = PsbType.Null;

        public override string ToString()
        {
            return "null";
        }

        public void WriteTo(BinaryWriter bw)
        {
            bw.Write((byte)Type);
        }
        
        public static PsbNull Null => new PsbNull();
    }

    [Serializable]
    public class PsbBool : IPsbValue, IPsbWrite
    {
        public PsbBool(bool value = false)
        {
            Value = value;
        }

        public bool Value { get; set; } = false;

        public PsbType Type => Value ? PsbType.True : PsbType.False;

        public override string ToString()
        {
            return Value.ToString();
        }

        public void WriteTo(BinaryWriter bw)
        {
            bw.Write((byte)Type);
        }
    }

    public enum PsbNumberType
    {
        Int,
        Float,
        Double
    }

    [Serializable]
    public class PsbNumber : IPsbValue, IPsbWrite
    {
        internal PsbNumber(PsbType type, BinaryReader br)
        {
            Data = new byte[8];
            NumberType = PsbNumberType.Int;

            switch (type)
            {
                case PsbType.NumberN0:
                    IntValue = 0;
                    return;
                case PsbType.NumberN1:
                    Data = br.ReadBytes(1).UnzipNumberBytes();
                    return;
                case PsbType.NumberN2:
                    Data = br.ReadBytes(2).UnzipNumberBytes();
                    return;
                case PsbType.NumberN3:
                    Data = br.ReadBytes(3).UnzipNumberBytes();
                    return;
                case PsbType.NumberN4:
                    Data = br.ReadBytes(4).UnzipNumberBytes();
                    return;
                case PsbType.NumberN5:
                    Data = br.ReadBytes(5).UnzipNumberBytes();
                    return;
                case PsbType.NumberN6:
                    Data = br.ReadBytes(6).UnzipNumberBytes();
                    return;
                case PsbType.NumberN7:
                    Data = br.ReadBytes(7).UnzipNumberBytes();
                    return;
                case PsbType.NumberN8:
                    Data = br.ReadBytes(8).UnzipNumberBytes();
                    return;
                case PsbType.Float0:
                    NumberType = PsbNumberType.Float;
                    //Data = br.ReadBytes(1);
                    Data = BitConverter.GetBytes(0.0f);
                    return;
                case PsbType.Float:
                    NumberType = PsbNumberType.Float;
                    Data = br.ReadBytes(4);
                    return;
                case PsbType.Double:
                    NumberType = PsbNumberType.Double;
                    Data = br.ReadBytes(8);
                    return;
            }
        }

        public PsbNumber()
        {
            Data = new byte[8];
            NumberType = PsbNumberType.Int;
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
            NumberType = PsbNumberType.Int;
            IntValue = (int)val;
        }

        public byte[] Data { get; set; }

        public PsbNumberType NumberType { get; set; } = PsbNumberType.Int;

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
                    default:
                        return LongValue;
                }
            }
        }

        public int IntValue
        {
            get => BitConverter.ToInt32(Data, 0);
            set => Data = BitConverter.GetBytes(value);
        }

        public float FloatValue
        {
            get => BitConverter.ToSingle(Data, 0);
            set => Data = BitConverter.GetBytes(value);
        }
        public double DoubleValue
        {
            get => BitConverter.ToDouble(Data, 0);
            set => Data = BitConverter.GetBytes(value);
        }
        public long LongValue
        {
            get => BitConverter.ToInt64(Data, 0);
            set => Data = BitConverter.GetBytes(value);
        }

        public bool IsNumber32()
        {
            return Type == PsbType.NumberN0 || Type == PsbType.NumberN1 ||
                   Type == PsbType.NumberN2 || Type == PsbType.NumberN3 ||
                   Type == PsbType.NumberN4;
        }

        public static explicit operator int(PsbNumber p)
        {
            switch (p.NumberType)
            {
                case PsbNumberType.Float:
                    return (int)p.FloatValue;
                case PsbNumberType.Double:
                    return (int)p.DoubleValue;
                case PsbNumberType.Int:
                default:
                    return p.IntValue;
            }
        }

        public static explicit operator float(PsbNumber p)
        {
            switch (p.NumberType)
            {
                case PsbNumberType.Int:
                    return (float)p.IntValue;
                case PsbNumberType.Double:
                    return (float)p.DoubleValue;
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
                    return (double)p.IntValue;
                case PsbNumberType.Float:
                    return (double)p.FloatValue;
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

        public PsbType Type
        {
            get
            {
                switch (NumberType)
                {
                    case PsbNumberType.Int: //FIXED: What did I wrote? when IntValue <= 8 && IntValue >= 0
                        switch (IntValue.GetSize())
                        {
                            case 1:
                                if (IntValue == 0)
                                {
                                    return PsbType.NumberN0;
                                }
                                return PsbType.NumberN1;
                            case 2:
                                return PsbType.NumberN2;
                            case 3:
                                return PsbType.NumberN3;
                            case 4:
                                return PsbType.NumberN4;
                            case 5:
                                return PsbType.NumberN5;
                            case 6:
                                return PsbType.NumberN6;
                            case 7:
                                return PsbType.NumberN7;
                            case 8:
                                return PsbType.NumberN8;
                            default:
                                throw new ArgumentOutOfRangeException("Not a valid Integer");
                        }
                    case PsbNumberType.Float:
                        //TODO: Float0 or not
                        if (Math.Abs(FloatValue) < float.Epsilon) //should we just use 0?
                        {
                            return PsbType.Float0;
                        }
                        return PsbType.Float;
                    case PsbNumberType.Double:
                        return PsbType.Double;
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
            bw.Write((byte)Type);
            switch (NumberType)
            {
                case PsbNumberType.Int:
                    if (Type != PsbType.NumberN0)
                    {
                        bw.Write(IntValue.ZipNumberBytes());
                    }
                    break;
                case PsbNumberType.Float:
                    if (Type != PsbType.Float0)
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
                    if (Type != PsbType.NumberN0)
                    {
                        return IntValue.ZipNumberBytes();
                    }
                    return new byte[0];
                case PsbNumberType.Float:
                    if (Type != PsbType.Float0)
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
    /// uint[]
    /// </summary>
    [Serializable]
    public class PsbArray : IPsbValue, IPsbWrite
    {
        internal PsbArray(int n, BinaryReader br)
        {
            uint count = br.ReadBytes(n).UnzipUInt();
            if (count > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException("Long array is not supported yet");
            }
            EntryLength = (byte)(br.ReadByte() - PsbType.NumberN8);
            Value = new List<uint>((int)count);
            for (int i = 0; i < count; i++)
            {
                Value.Add(br.ReadBytes(EntryLength).UnzipUInt());
            }
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

        public PsbType Type
        {
            get
            {
                switch (Value.Count.GetSize())
                {
                    case 1:
                        return PsbType.ArrayN1;
                    case 2:
                        return PsbType.ArrayN2;
                    case 3:
                        return PsbType.ArrayN3;
                    case 4:
                        return PsbType.ArrayN4;
                    case 5:
                        return PsbType.ArrayN5;
                    case 6:
                        return PsbType.ArrayN6;
                    case 7:
                        return PsbType.ArrayN7;
                    case 8:
                        return PsbType.ArrayN8;
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
            EntryLength = (byte)maxSize;
            return EntryLength;
        }

        public void WriteTo(BinaryWriter bw)
        {
            bw.Write((byte)Type); //Type
            bw.Write(Value.Count.ZipNumberBytes(Value.Count.GetSize())); //Count
            bw.Write((byte)(GetEntryLength() + (byte)PsbType.NumberN8)); //FIXED: EntryLength is added by 0xC
            foreach (var u in Value)
            {
                bw.Write(u.ZipNumberBytes(EntryLength));
            }
        }
    }

    [Serializable]
    [DebuggerDisplay("{Value}(#{Index})")]
    public class PsbString : IPsbValue, IPsbIndexed, IPsbWrite
    {
        internal PsbString(int n, BinaryReader br)
        {
            Index = br.ReadBytes(n).UnzipUInt();
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
        public PsbType Type
        {
            get
            {
                var size = Index?.GetSize() ?? 0.GetSize();
                switch (size)
                {
                    case 1:
                        return PsbType.StringN1;
                    case 2:
                        return PsbType.StringN2;
                    case 3:
                        return PsbType.StringN3;
                    case 4:
                        return PsbType.StringN4;
                    default:
                        throw new ArgumentOutOfRangeException("size", "Not a valid string");
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

        public override bool Equals(object obj)
        {
            var s = obj as PsbString;
            return s != null ? Equals(s) : base.Equals(obj);
        }

        protected bool Equals(PsbString other)
        {
            if (other is null)
            {
                return false;
            }
            return string.Equals(Value, other.Value);
        }

        public override int GetHashCode()
        {
            return (Value != null ? Value.GetHashCode() : 0);
        }

        public void WriteTo(BinaryWriter bw)
        {
            bw.Write((byte)Type);
            if (Index == null)
            {
                throw new ArgumentNullException("Index", "Index can not be null when writing");
            }
            bw.Write(Index.Value.ZipNumberBytes()); //FIXED:
            //new PsbNumber(Index ?? 0u).WriteTo(bw); //Wrong because it writes number type
        }
    }

    /// <summary>
    /// psb_objects_t
    /// </summary>
    [Serializable]
    public class PsbDictionary : Dictionary<string, IPsbValue>, IPsbValue, IPsbCollection
    {
        public PsbDictionary(int capacity):base(capacity)
        {
        }
        public Dictionary<string, IPsbValue> Value => this;
        
        public IPsbCollection Parent { get; set; } = null;

        IPsbValue IPsbCollection.this[int i] => ContainsKey(i.ToString()) ? base[i.ToString()] : null;

        public new IPsbValue this[string index]
        {
            get => TryGetValue(index, out IPsbValue val) ? val : null;
            set => base[index] = value;
        }

        public PsbType Type { get; } = PsbType.Objects;

        public override string ToString()
        {
            return $"Dictionary[{Count}]";
        }
    }

    [Serializable]
    public class PsbCollection : List<IPsbValue>, IPsbValue, IPsbCollection
    {
        public PsbCollection(int capacity) : base(capacity)
        {
        }

        public List<IPsbValue> Value => this;
        
        public IPsbCollection Parent { get; set; } = null;
        
        public new IPsbValue this[int index]
        {
            get => index < Count ? base[index] : null;
            set => base[index] = value;
        }

        IPsbValue IPsbCollection.this[string s] => int.TryParse(s, out int i) ? base[i] : null;

        public PsbType Type { get; } = PsbType.Collection;

        public override string ToString()
        {
            return $"Collection[{Count}]";
        }
    }

    [Serializable]
    [DebuggerDisplay("Resource[{Data?.Length}](#{" + nameof(Index) + "})")]
    public class PsbResource : IPsbValue, IPsbIndexed, IPsbWrite, IPsbSingleton
    {
        internal PsbResource(int n, BinaryReader br)
        {
            Index = br.ReadBytes(n).UnzipUInt();
        }
        public PsbResource(uint index = 0)
        {
            Index = index;
        }

        /// <summary>
        /// Update index when compile
        /// </summary>
        public uint? Index { get; set; }
        public byte[] Data { get; set; } = new byte[0];

        public PsbType Type
        {
            get
            {
                var size = Index?.GetSize() ?? 0.GetSize();
                switch (size)
                {
                    case 1:
                        return PsbType.ResourceN1;
                    case 2:
                        return PsbType.ResourceN2;
                    case 3:
                        return PsbType.ResourceN3;
                    case 4:
                        return PsbType.ResourceN4;
                    default:
                        throw new ArgumentOutOfRangeException("Index", "Not a valid resource");
                }
            }
        }

        public override string ToString()
        {
            return $"#resource#{Index}";
        }

        public void WriteTo(BinaryWriter bw)
        {
            bw.Write((byte)Type);
            if (Index == null)
            {
                throw new ArgumentNullException("Index", "Index can not be null when writing");
            }
            bw.Write(Index.Value.ZipNumberBytes()); //FIXED:
        }

        public List<IPsbCollection> Parents { get; set; } = new List<IPsbCollection>();
    }

}
