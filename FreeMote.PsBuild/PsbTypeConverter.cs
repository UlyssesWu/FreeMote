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
    /// <summary>
    /// <see cref="JsonConverter"/> for PSB types
    /// </summary>
    class PsbTypeConverter : JsonConverter
    {
        //internal static List<Type> SupportTypes = new List<Type> {
        //    typeof(PsbNull),typeof(PsbBool),typeof(PsbNumber),
        //    typeof(PsbArray), typeof(PsbString),typeof(PsbResource),
        //    typeof(PsbCollection),typeof(PsbDictionary),
        //};

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
                    writer.WriteValue($"{PsbResCollector.ResourceIdentifier}{res.Index}");
                    break;
                case PsbArray array:
                    writer.WriteValue(array.Value);
                    break;
                case PsbCollection collection:
                    writer.WriteStartArray();
                    if (collection.Count > 0 && !(collection[0] is IPsbCollection))
                    {
                        writer.Formatting = Formatting.None;
                    }
                    foreach (var obj in collection)
                    {
                        WriteJson(writer, obj, serializer);
                    }
                    writer.WriteEndArray();
                    writer.Formatting = Formatting.Indented;
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
            List<PsbString> context = new List<PsbString>();
            JObject obj = JObject.Load(reader);
            return ConvertToken(obj, context);
        }

        internal IPsbValue ConvertToken(JToken token, List<PsbString> context)
        {
            switch (token.Type)
            {
                case JTokenType.Null:
                    return PsbNull.Null;
                case JTokenType.Integer:
                    return new PsbNumber(token.Value<int>());
                case JTokenType.Float:
                    var d = token.Value<double>();
                    var f = token.Value<float>();
                    if (Math.Abs(f - d) < 1E-05) //float //pcc: 1E-05
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
                    string str = token.Value<string>();
                    if (str.StartsWith(PsbResCollector.ResourceIdentifier))
                    {
                        return new PsbResource(uint.Parse(str.Replace(PsbResCollector.ResourceIdentifier, "")));
                    }
                    var psbStr = new PsbString(str, (uint)context.Count);
                    if (context.Contains(psbStr))
                    {
                        return context.Find(ps => ps.Value == str);
                    }
                    else
                    {
                        context.Add(psbStr);
                    }
                    return psbStr;
                case JTokenType.Array:
                    var array = (JArray)token;
                    var collection = new PsbCollection(array.Count);
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
}
