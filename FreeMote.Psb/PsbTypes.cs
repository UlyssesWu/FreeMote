using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FreeMote.Psb
{
    public enum PsbType : byte
    {

        None = 0x0,
        Null = 0x1, // 0
        False = 0x2, //??
        True = 0x3,  //??

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
        Objects = 0x21,    //object


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
    /// Tracked by index
    /// </summary>
    public interface IPsbIndexed
    {
        uint Index { get; set; }
        PsbType Type { get; }
    }

    /// <summary>
    /// PSB Unit
    /// </summary>
    public interface IPsbValue
    {
        PsbType Type { get; }
        string ToString();
    }

    [Serializable]
    public class PsbNull : IPsbValue
    {
        public object Value => null;

        public PsbType Type { get; } = PsbType.Null;

        public override string ToString()
        {
            return "null";
        }
    }

    [Serializable]
    public class PsbBool : IPsbValue
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
    }

    public enum PsbNumberType
    {
        Int,
        Float,
        Double
    }

    [Serializable]
    public class PsbNumber : IPsbValue
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

        public PsbType Type
        {
            get
            {
                switch (NumberType)
                {
                    case PsbNumberType.Int when IntValue <= 8 && IntValue >= 0:
                        switch (IntValue.GetSize()) //Black magic to get size hehehe...
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
    }

    /// <summary>
    /// uint[]
    /// </summary>
    [Serializable]
    public class PsbArray : IPsbValue
    {
        internal PsbArray(int n, BinaryReader br)
        {
            uint count = br.ReadBytes(n).UnzipUInt();
            if (count > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException("Long array is not supported yet");
            }
            EntryLength = br.ReadByte() - (byte)PsbType.NumberN8;
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

        public uint this[int index]
        {
            get => Value[index];
            set => Value[index] = value;
        }

        public int EntryLength { get; set; } = 4;
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
    }

    [Serializable]
    public class PsbString : IPsbValue, IPsbIndexed
    {
        internal PsbString(int n, BinaryReader br)
        {
            Index = br.ReadBytes(n).UnzipUInt();
        }

        public PsbString(string value = "", uint index = 0)
        {
            Value = value;
            Index = index;
        }

        /// <summary>
        /// Update index when compile
        /// </summary>
        public uint Index { get; set; }

        public string Value { get; set; }

        /// <summary>
        /// It's based on index...
        /// </summary>
        public PsbType Type
        {
            get
            {
                switch (Index.GetSize())
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
                        throw new ArgumentOutOfRangeException("Not a valid string");
                }
            }
        }

        public override string ToString()
        {
            return "\"" + Value + "\"" + $"(#{Index})";
        }

        public static implicit operator string(PsbString s)
        {
            return s.Value;
        }
    }

    /// <summary>
    /// psb_objects_t
    /// </summary>
    [Serializable]
    public class PsbDictionary : IPsbValue
    {
        public PsbDictionary(int capacity)
        {
            Value = new Dictionary<string, IPsbValue>(capacity);
        }
        public IPsbValue this[string index]
        {
            get => Value[index];
            set => Value[index] = value;
        }

        public Dictionary<string, IPsbValue> Value { get; }
        public PsbType Type { get; } = PsbType.Objects;

        public override string ToString()
        {
            return $"Dictionary[{Value.Count}]";
        }
    }

    [Serializable]
    public class PsbCollection : IPsbValue
    {
        public PsbCollection(int capacity)
        {
            Value = new List<IPsbValue>(capacity);
        }

        public List<IPsbValue> Value { get; set; }
        public IPsbValue this[int index]
        {
            get => Value[index];
            set => Value[index] = value;
        }

        public PsbType Type { get; } = PsbType.Collection;

        public override string ToString()
        {
            return $"Collection[{Value.Count}]";
        }
    }

    [Serializable]
    public class PsbResource : IPsbValue, IPsbIndexed
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
        public uint Index { get; set; }
        public byte[] Data { get; set; }

        public PsbType Type
        {
            get
            {
                switch (Index.GetSize())
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
                        throw new ArgumentOutOfRangeException("Not a valid resource");
                }
            }
        }

        public override string ToString()
        {
            return $"Resource[{Data.Length}](#{Index})";
        }
    }


}
