using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FreeMote.Psb;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FreeMote.PsBuild
{
    /// <summary>
    /// <see cref="JsonConverter"/> for PSB types
    /// </summary>
    class PsbJsonConverter : JsonConverter
    {
        //WARN: some tricks are used to keep original values as exact
        //"0x0000C0FFf" will be convert to (float)NaN

        //internal static List<Type> SupportTypes = new List<Type> {
        //    typeof(PsbNull),typeof(PsbBool),typeof(PsbNumber),
        //    typeof(PsbArray), typeof(PsbString),typeof(PsbResource),
        //    typeof(PsbList),typeof(PsbDictionary),
        //};

        public bool UseDoubleOnly { get; set; }
        public bool UseHexNumber { get; set; }

        public PsbJsonConverter(bool useDoubleOnly = false, bool useHexNumber = false)
        {
            UseDoubleOnly = useDoubleOnly;
            UseHexNumber = useHexNumber;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            switch (value)
            {
                case PsbNull _:
                    writer.WriteNull();
                    break;
                case PsbBool b:
                    writer.WriteValue(b.Value);
                    break;
                case PsbNumber num:
                    switch (num.NumberType)
                    {
                        case PsbNumberType.Int:
                            writer.WriteValue(num.IntValue);
                            break;
                        case PsbNumberType.Long:
                            writer.WriteValue(num.LongValue);
                            break;
                        case PsbNumberType.Float:
                            if (num.FloatValue.IsFinite() && !UseHexNumber)
                            {
                                writer.WriteValue(num.FloatValue);
                            }
                            else
                            {
                                writer.WriteValue($"{Consts.NumberStringPrefix}{num.IntValue:X8}f");
                            }
                            //writer.WriteRawValue(num.FloatValue.ToString("R"));
                            break;
                        case PsbNumberType.Double:
                            if (num.DoubleValue.IsFinite() && !UseHexNumber)
                            {
                                writer.WriteValue(num.DoubleValue);
                            }
                            else
                            {
                                writer.WriteValue($"{Consts.NumberStringPrefix}{num.LongValue:X16}d");
                            }
                            break;
                        default:
                            writer.WriteValue(num.LongValue);
                            break;
                    }
                    break;
                case PsbString str:
                    writer.WriteValue(str.Value);
                    break;
                case PsbResource res:
                    //writer.WriteValue(Convert.ToBase64String(res.Data, Base64FormattingOptions.None));
                    writer.WriteValue($"{res.ResourceIdentifier}{res.Index}");
                    break;
                case PsbArray array:
                    writer.WriteValue(array.Value);
                    break;
                case PsbList collection:
                    writer.WriteStartArray();
                    foreach (var obj in collection)
                    {
                        WriteJson(writer, obj, serializer);
                    }
                    writer.WriteEndArray();
                    break;
                case PsbDictionary dictionary:
                    writer.WriteStartObject();
                    foreach (var obj in dictionary)
                    {
                        writer.WritePropertyName(obj.Key);
                        WriteJson(writer, obj.Value, serializer);
                    }
                    writer.WriteEndObject();
                    break;
                default:
                    break;
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Dictionary<string, PsbString> context = new Dictionary<string, PsbString>();
            JToken obj;
            if (reader.TokenType == JsonToken.StartArray)
            {
                obj = JArray.Load(reader);
            }
            else
            {
                obj = JObject.Load(reader);
            }
            return ConvertToken(obj, context);
        }

        internal IPsbValue ConvertToken(JToken token, Dictionary<string, PsbString> context)
        {
            switch (token.Type)
            {
                case JTokenType.Null:
                    return PsbNull.Null;
                case JTokenType.Integer:
                    var l = token.Value<long>();
                    if (l > Int32.MaxValue || l < Int32.MinValue)
                    {
                        return new PsbNumber(l);
                    }
                    return new PsbNumber(token.Value<int>());
                case JTokenType.Float:
                    //var numberStr = token.Value<string>();
                    var d = token.Value<double>();
                    if (UseDoubleOnly)
                    {
                        return new PsbNumber(d);
                    }
                    var f = token.Value<float>();
                    if (Math.Abs(f - d) < 1E-08) //float //pcc: 1E-05
                    //if (Math.Abs(f - d) < float.Epsilon)
                    {
                        return new PsbNumber(f);
                    }
                    //if (d < float.MaxValue && d > float.MinValue)
                    //{
                    //    return new PsbNumber(token.Value<float>());
                    //}
                    return new PsbNumber(d);
                case JTokenType.Boolean:
                    return new PsbBool(token.Value<bool>());
                case JTokenType.String:
                    string str = token.Value<string>();
                    if (str.StartsWith(Consts.NumberStringPrefix))
                    {
                        var prefixLen = Consts.NumberStringPrefix.Length;
                        if (str.EndsWith("f"))
                        {
                            return new PsbNumber(int.Parse(str.Substring(prefixLen, 8), NumberStyles.AllowHexSpecifier)) { NumberType = PsbNumberType.Float };
                        }
                        if (str.EndsWith("d"))
                        {
                            return new PsbNumber(long.Parse(str.Substring(prefixLen, 16), NumberStyles.AllowHexSpecifier)) { NumberType = PsbNumberType.Double };
                        }
                        return new PsbNumber(long.Parse(str.Substring(prefixLen), NumberStyles.AllowHexSpecifier));
                    }

                    if (str.StartsWith(Consts.ExtraResourceIdentifier))
                    {
                        return new PsbResource(uint.Parse(str.Replace(Consts.ExtraResourceIdentifier, "")), true);
                    }

                    if (str.StartsWith(Consts.ResourceIdentifier))
                    {
                        return new PsbResource(uint.Parse(str.Replace(Consts.ResourceIdentifier, "")));
                    }
                    var psbStr = new PsbString(str, (uint)context.Count);
                    if (context.ContainsKey(str))
                    {
                        return context[str];
                    }
                    else
                    {
                        context.Add(str, psbStr);
                    }
                    return psbStr;
                case JTokenType.Array:
                    var array = (JArray)token;
                    var collection = new PsbList(array.Count);
                    foreach (var val in array)
                    {
                        var o = ConvertToken(val, context);
                        if (o is IPsbChild c)
                        {
                            c.Parent = collection;
                        }
                        if (o is IPsbSingleton s)
                        {
                            s.Parents.Add(collection);
                        }
                        collection.Add(o);
                    }
                    return collection;
                case JTokenType.Object:
                    var obj = (JObject)token;
                    var dictionary = new PsbDictionary(obj.Count);
                    foreach (var val in obj)
                    {
                        var o = ConvertToken(val.Value, context);
                        if (o is IPsbChild c)
                        {
                            c.Parent = dictionary;
                        }
                        if (o is IPsbSingleton s)
                        {
                            s.Parents.Add(dictionary);
                        }
                        dictionary.Add(val.Key, o);
                    }
                    return dictionary;
                default:
                    throw new FormatException("Unsupported json element");
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType.GetInterface("IPsbValue") != null;
            //return SupportTypes.Contains(objectType);
        }
    }

    public class ArrayCollapseJsonTextWriter : JsonTextWriter
    {
        public bool AddIndentSpace { get; set; } = false;

        public ArrayCollapseJsonTextWriter(TextWriter writer) : base(writer)
        {
        }

        protected override void WriteIndent()
        {
            if (WriteState != WriteState.Array)
            {
                base.WriteIndent();
            }
            else
            {
                if (AddIndentSpace)
                {
                    WriteIndentSpace();
                }
            }
        }

        public static string SerializeObject(object obj, JsonConverter converter = null)
        { 
            using StringWriter sw = new StringWriter();
            using JsonWriter jw = new ArrayCollapseJsonTextWriter(sw) {Formatting = Formatting.Indented};
            var ser = new JsonSerializer();
            if (converter != null)
            {
                ser.Converters.Add(converter);
            }
            ser.Serialize(jw, obj);
            return sw.ToString();
        }
    }
}
