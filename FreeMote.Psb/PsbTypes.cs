using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//PSB format is based on psbfile by number201724.

namespace FreeMote.Psb
{
    public enum PsbType
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

    public class Test
    {
        public Test()
        {
        }
    }

    public interface IPsbValue
    {
        string ToString();
    }

    public class PsbNull : IPsbValue
    {
        public object Value => null;

        public override string ToString()
        {
            return "null";
        }
    }

    public class PsbBool : IPsbValue
    {
        public PsbBool(bool value = false)
        {
            Value = value;
        }

        public bool Value { get; set; } = false;

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

    public class PsbNumber : IPsbValue
    {
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
                        return Doublealue;
                    default:
                        return UIntValue;
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
        public double Doublealue
        {
            get => BitConverter.ToDouble(Data, 0);
            set => Data = BitConverter.GetBytes(value);
        }
        public uint UIntValue
        {
            get => BitConverter.ToUInt32(Data, 0);
            set => Data = BitConverter.GetBytes(value);
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public class PsbArray<T> : IPsbValue where T : IPsbValue
    {
        public PsbArray()
        {
            Value = new List<T>();
        }
        public List<T> Value { get; }

        public override string ToString()
        {
            return $"Array<{typeof(T).ToString().Replace("Psb","")}>[{Value.Count}]";
        }
    }

    public class PsbString : IPsbValue
    {
        public PsbString(string value = "")
        {
            Value = value;
        }

        public string Value { get; set; }

        //Maybe we shouldn't keep this info here
        //public uint Index { get; set; } = 0;

        public override string ToString()
        {
            return "\"" + Value + "\"";
        }
    }

    public class PsbObject : IPsbValue
    {
        /// <summary>
        /// Name is unique
        /// </summary>
        public string Name { get; set; }

        public byte[] Data { get; set; }

        public override string ToString()
        {
            return $"<{Name ?? "?"}>";
        }
    }

    public class PsbCollection : IPsbValue
    {
        public PsbCollection()
        {
            Value = new List<IPsbValue>();
        }

        public List<IPsbValue> Value { get; set; }

        public override string ToString()
        {
            return $"Collection[{Value.Count}]";
        }
    }

    public class Psb
    {
        internal PsbHeader Header;
        public OrderedDictionary Names = new OrderedDictionary();
        public OrderedDictionary Strings = new OrderedDictionary();

        public Psb(string path)
        {
            
        }

        public Psb(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream);
            Header = PsbHeader.Load(br);
            br.BaseStream.Seek(Header.OffsetNames, SeekOrigin.Begin);

        }

        private void GetAllNames()
        {
            
        }

        private IPsbValue Unpack(BinaryReader br)
        {
            return null;
        }

    }
}
