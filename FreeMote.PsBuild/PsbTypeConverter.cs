using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreeMote.Psb;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FreeMote.PsBuild
{
    class PsbTypeConverter : JsonConverter
    {
        internal static List<Type> SupportTypes = new List<Type> {
            typeof(PsbNull),typeof(PsbBool),typeof(PsbNumber),
            typeof(PsbArray), typeof(PsbString),typeof(PsbResource),
            typeof(PsbCollection),typeof(PsbDictionary),
        };

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
                        case PsbNumberType.Float:
                            writer.WriteValue(num.FloatValue);
                            //writer.WriteRawValue(num.FloatValue.ToString("R"));
                            break;
                        case PsbNumberType.Double:
                            writer.WriteValue(num.DoubleValue);
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
                    writer.WriteValue($"{PsbHelper.ResourceIdentifier}{res.Index}");
                    break;
                case PsbArray array:
                    writer.WriteValue(array.Value);
                    break;
                case PsbCollection collection:
                    writer.WriteStartArray();
                    if (collection.Value.Count > 0 && !(collection[0] is PsbCollection) && !(collection[0] is PsbDictionary))
                    {
                        writer.Formatting = Formatting.None;
                    }
                    foreach (var obj in collection.Value)
                    {
                        WriteJson(writer, obj, serializer);
                    }
                    writer.WriteEndArray();
                    writer.Formatting = Formatting.Indented;
                    break;
                case PsbDictionary dictionary:
                    writer.WriteStartObject();
                    foreach (var obj in dictionary.Value)
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
            JObject obj = JObject.Load(reader);
            return ConvertToken(obj);
        }

        internal IPsbValue ConvertToken(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Null:
                    return new PsbNull();
                case JTokenType.Integer:
                    return new PsbNumber(token.Value<int>());
                case JTokenType.Float:
                    var d = token.Value<double>();
                    var f = token.Value<float>();
                    if (Math.Abs(f - d) < double.Epsilon) //float
                    {
                        return new PsbNumber(token.Value<float>());
                    }
                    //if (d < float.MaxValue && d > float.MinValue)
                    //{
                    //    return new PsbNumber(token.Value<float>());
                    //}
                    return new PsbNumber(d);
                case JTokenType.Boolean:
                    return new PsbBool(token.Value<bool>());
                case JTokenType.String:
                    string s = token.Value<string>();
                    if (s.StartsWith(PsbHelper.ResourceIdentifier))
                    {
                        return new PsbResource(uint.Parse(s.Replace(PsbHelper.ResourceIdentifier, "")));
                    }
                    return new PsbString(s);
                case JTokenType.Array:
                    var array = (JArray)token;
                    var collection = new PsbCollection(array.Count);
                    foreach (var val in array)
                    {
                        collection.Value.Add(ConvertToken(val));
                    }
                    return collection;
                case JTokenType.Object:
                    var obj = (JObject)token;
                    var dictionary = new PsbDictionary(obj.Count);
                    foreach (var val in obj)
                    {
                        dictionary.Value.Add(val.Key, ConvertToken(val.Value));
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
}
