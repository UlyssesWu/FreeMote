using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FreeMote.Psb
{
    internal static class PsbHelper
    {
        //WARN: GetSize should not return 0
        /// <summary>
        /// Black magic to get size hehehe...
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public static int GetSize(this int i)
        {
            bool neg = false;
            if (i < 0)
            {
                neg = true;
                i = Math.Abs(i);
            }
            var hex = i.ToString("X");
            var l = hex.Length;
            bool firstBitOne = hex[0] >= '8' && hex.Length % 2 == 0; //FIXED: Extend size if first bit is 1 //FIXED: 0x0F is +, 0xFF is -, 0x0FFF is +

            if (l % 2 != 0)
            {
                l++;
            }
            l = l / 2;
            if (neg || firstBitOne)
            {
                l++;
            }
            if (l > 4)
            {
                l = 4;
            }
            return l;
        }

        /// <summary>
        /// Black magic to get size hehehe...
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public static int GetSize(this uint i)
        {
            //FIXED: Treat uint as int to prevent overconfidence
            if (i <= Int32.MaxValue)
            {
                return GetSize((int)i);
            }

            var l = i.ToString("X").Length;
            if (l % 2 != 0)
            {
                l++;
            }
            l = l / 2;
            if (l > 4)
            {
                l = 4;
            }
            return l;
        }

        /// <summary>
        /// Black magic... hehehe...
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public static int GetSize(this long i)
        {
            bool neg = false;
            if (i < 0)
            {
                neg = true;
                i = Math.Abs(i);
            }
            var hex = i.ToString("X");
            var l = hex.Length;
            bool firstBitOne = hex[0] >= '8' && hex.Length % 2 == 0; //FIXED: Extend size if first bit is 1 //FIXED: 0x0F is +, 0xFF is -, 0x0FFF is +

            if (l % 2 != 0)
            {
                l++;
            }
            l = l / 2;
            if (neg || firstBitOne)
            {
                l++;
            }
            if (l > 8)
            {
                l = 8;
            }
            return l;
        }

        /// <summary>
        /// Shorten number bytes
        /// </summary>
        /// <param name="i"></param>
        /// <param name="size">Fix size</param>
        /// <returns></returns>
        public static byte[] ZipNumberBytes(this int i, int size = 0)
        {
            return BitConverter.GetBytes(i).Take(size <= 0 ? i.GetSize() : size).ToArray();
        }

        /// <summary>
        /// Shorten number bytes
        /// </summary>
        /// <param name="i"></param>
        /// <param name="size">Fix size</param>
        /// <returns></returns>
        public static byte[] ZipNumberBytes(this long i, int size = 0)
        {
            return BitConverter.GetBytes(i).Take(size <= 0 ? i.GetSize() : size).ToArray();
        }

        /// <summary>
        /// Shorten number bytes
        /// </summary>
        /// <param name="i"></param>
        /// <param name="size">Fix size</param>
        /// <returns></returns>
        public static byte[] ZipNumberBytes(this uint i, int size = 0)
        {
            //FIXED: Treat uint as int to prevent overconfidence
            //if (i <= int.MaxValue)
            //{
            //    return ZipNumberBytes((int) i, size);
            //}
            return BitConverter.GetBytes(i).Take(size <= 0 ? i.GetSize() : size).ToArray();
        }

        public static byte[] UnzipNumberBytes(this byte[] b, int size = 8, bool unsigned = false)
        {
            byte[] r = new byte[size];
            if (!unsigned && (b.Last() >= 0b10000000)) //negative
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

        /// <summary>
        /// Get <see cref="PsbSpec"/>'s default <see cref="PsbPixelFormat"/>
        /// </summary>
        /// <param name="spec"></param>
        /// <returns></returns>
        public static PsbPixelFormat DefaultPixelFormat(this PsbSpec spec)
        {
            switch (spec)
            {
                case PsbSpec.common:
                case PsbSpec.ems:
                    return PsbPixelFormat.CommonRGBA8;
                case PsbSpec.krkr:
                case PsbSpec.win:
                    return PsbPixelFormat.WinRGBA8;
                case PsbSpec.other:
                default:
                    return PsbPixelFormat.None;
            }
        }

        /// <summary>
        /// Get <see cref="PsbPixelFormat"/> from string and <see cref="PsbSpec"/>
        /// </summary>
        /// <param name="typeStr"></param>
        /// <param name="spec"></param>
        /// <returns></returns>
        public static PsbPixelFormat ToPsbPixelFormat(this string typeStr, PsbSpec spec)
        {
            if (String.IsNullOrEmpty(typeStr))
            {
                return PsbPixelFormat.None;
            }
            switch (typeStr.ToUpperInvariant())
            {
                case "DXT5":
                    return PsbPixelFormat.DXT5;
                case "RGBA8":
                    if (spec == PsbSpec.common || spec == PsbSpec.ems)
                        return PsbPixelFormat.CommonRGBA8;
                    else
                        return PsbPixelFormat.WinRGBA8;
                case "RGBA4444":
                    return PsbPixelFormat.WinRGBA4444;
                default:
                    return PsbPixelFormat.None;
            }
        }

        /// <summary>
        /// Get <see cref="PsbType"/>'s default extension
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string DefaultExtension(this PsbType type)
        {
            switch (type)
            {
                case PsbType.Pimg:
                    return "pimg";
                case PsbType.Scn:
                    return "scn";
                case PsbType.Motion:
                default:
                    return "psb";
            }
        }

        /// <summary>
        /// Get name in <see cref="PsbDictionary"/>
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static string GetName(this IPsbChild c)
        {
            if (c?.Parent is PsbDictionary dic)
            {
                var result = dic.FirstOrDefault(pair => Equals(pair.Value, c));
                return result.Value == null ? null : result.Key;
            }

            if (c?.Parent is PsbCollection col)
            {
                var result = col.Value.IndexOf(c);
                if (result < 0)
                {
                    return null;
                }
                return $"[{result}]";
            }

            return null;
        }

        /// <summary>
        /// Get name
        /// </summary>
        /// <param name="c"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static string GetName(this IPsbSingleton c, PsbDictionary parent = null)
        {
            var source = parent ?? c?.Parents.FirstOrDefault(p => p is PsbDictionary) as PsbDictionary;
            var result = source?.FirstOrDefault(pair => Equals(pair.Value, c));
            return result?.Value == null ? null : result.Value.Key;
        }

        /// <summary>
        /// Check if number is not NaN
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public static bool IsFinite(this float num)
        {
            return !Single.IsNaN(num) && !Single.IsInfinity(num);
        }

        /// <summary>
        /// Check if number is not NaN
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public static bool IsFinite(this double num)
        {
            return !Double.IsNaN(num) && !Double.IsInfinity(num);
        }

        public static PsbString ToPsbString(this string s)
        {
            return s == null ? PsbString.Empty : new PsbString(s);
        }

        public static PsbNumber ToPsbNumber(this int i)
        {
            return new PsbNumber(i);
        }

        /// <summary>
        /// If this spec uses RL
        /// </summary>
        /// <param name="spec"></param>
        /// <returns></returns>
        public static PsbCompressType CompressType(this PsbSpec spec)
        {
            switch (spec)
            {
                case PsbSpec.krkr:
                    return PsbCompressType.RL;
                case PsbSpec.ems:
                case PsbSpec.common:
                case PsbSpec.win:
                case PsbSpec.other:
                default:
                    return PsbCompressType.None;
            }
        }

        #region Object Finding

        public static IEnumerable<IPsbValue> FindAllByPath(this PsbDictionary psbObj, string path)
        {
            if (psbObj == null)
                yield break;
            if (path.StartsWith("/"))
            {
                path = new string(path.SkipWhile(c => c == '/').ToArray());
            }
            if (path.Contains("/"))
            {
                var pos = path.IndexOf('/');
                var current = path.Substring(0, pos);
                if (current == "*")
                {
                    if (pos == path.Length - 1) //end
                    {
                        if (psbObj is PsbDictionary dic)
                        {
                            foreach (var dicValue in dic.Values)
                            {
                                yield return dicValue;
                            }
                        }
                    }
                    path = new string(path.SkipWhile(c => c == '*').ToArray());
                    foreach (var val in psbObj.Values)
                    {
                        if (val is PsbDictionary dic)
                        {
                            foreach (var dicValue in dic.FindAllByPath(path))
                            {
                                yield return dicValue;
                            }
                        }
                    }
                }
                if (pos == path.Length - 1 && psbObj[current] != null)
                {
                    yield return psbObj[current];
                }
                var currentObj = psbObj[current];
                if (currentObj is PsbDictionary collection)
                {
                    path = path.Substring(pos);
                    foreach (var dicValue in collection.FindAllByPath(path))
                    {
                        yield return dicValue;
                    }
                }
            }
            if (path == "*")
            {
                foreach (var value in psbObj.Values)
                {
                    yield return value;
                }
            }
            else if (psbObj[path] != null)
            {
                yield return psbObj[path];
            }
        }

        /// <summary>
        /// Find object by path (use index [n] for collection)
        /// <example>e.g. "/object/all_parts/motion/全体構造/layer/[0]"</example>
        /// </summary>
        /// <param name="psbObj"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static IPsbValue FindByPath(this PsbDictionary psbObj, string path)
        {
            if (psbObj == null)
                return null;
            if (path.StartsWith("/"))
            {
                path = new string(path.SkipWhile(c => c == '/').ToArray());
            }

            if (path.Contains("/"))
            {
                var pos = path.IndexOf('/');
                var current = path.Substring(0, pos);
                if (pos == path.Length - 1)
                {
                    return psbObj[current];
                }
                var currentObj = psbObj[current];
                if (currentObj is PsbDictionary dictionary)
                {
                    path = path.Substring(pos);
                    return dictionary.FindByPath(path);
                }

                if (currentObj is PsbCollection collection)
                {
                    path = path.Substring(pos);
                    return collection.FindByPath(path);
                }
            }
            return psbObj[path];
        }

        /// <inheritdoc cref="FindByPath(FreeMote.Psb.PsbDictionary,string)"/>
        public static IPsbValue FindByPath(this PsbCollection psbObj, string path)
        {
            if (psbObj == null)
                return null;
            if (path.StartsWith("/"))
            {
                path = new string(path.SkipWhile(c => c == '/').ToArray());
            }

            if (path.Contains("/"))
            {
                var pos = path.IndexOf('/');
                var current = path.Substring(0, pos);
                IPsbValue currentObj = null;
                if (current == "*")
                {
                    currentObj = psbObj.FirstOrDefault();
                }

                if (current.StartsWith("[") && current.EndsWith("]") && Int32.TryParse(current.Substring(1, current.Length - 2), out var id))
                {
                    currentObj = psbObj[id];
                }

                if (pos == path.Length - 1)
                {
                    return currentObj;
                }

                if (currentObj is PsbDictionary dictionary)
                {
                    path = path.Substring(pos);
                    return dictionary.FindByPath(path);
                }

                if (currentObj is PsbCollection collection)
                {
                    path = path.Substring(pos);
                    return collection.FindByPath(path);
                }
            }
            return null;
        }

        /// <summary>
        /// Find object by MMO style path (based on label)
        /// <example>e.g. "all_parts/全体構造/■全体レイアウト"</example>
        /// </summary> 
        /// <param name="psbObj"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static IPsbValue FindByMmoPath(this IPsbCollection psbObj, string path)
        {
            if (psbObj == null)
                return null;
            if (path.StartsWith("/"))
            {
                path = new string(path.SkipWhile(c => c == '/').ToArray());
            }

            string current = null;
            int pos = -1;
            if (path.Contains("/"))
            {
                pos = path.IndexOf('/');
                current = path.Substring(0, pos);
            }
            else
            {
                current = path;
            }

            IPsbValue currentObj = null;
            if (psbObj is PsbCollection col)
            {
                currentObj = col.FirstOrDefault(c =>
                    c is PsbDictionary d && d.ContainsKey("label") && d["label"] is PsbString s &&
                    s.Value == current);
            }
            else if (psbObj is PsbDictionary dic)
            {
                var dd = dic.Value.FirstOrDefault();
                var children =
                    (PsbCollection)(dic.ContainsKey("layerChildren") ? dic["layerChildren"] : dic["children"]);
                currentObj = children.FirstOrDefault(c =>
                    c is PsbDictionary d && d.ContainsKey("label") && d["label"] is PsbString s &&
                    s.Value == current);
            }
            if (pos == path.Length - 1 || pos < 0)
            {
                return currentObj;
            }

            if (currentObj is IPsbCollection psbCol)
            {
                path = path.Substring(pos);
                return psbCol.FindByMmoPath(path);
            }

            return psbObj[path];
        }

        /// <summary>
        /// Get MMO style path (based on label)
        /// </summary>
        /// <param name="child"></param>
        /// <returns></returns>
        public static string GetMmoPath(this IPsbChild child)
        {
            if (child?.Parent == null)
            {
                if (child is PsbDictionary dic)
                {
                    return dic["label"].ToString();
                }
                return "";
            }
            List<string> paths = new List<string>();

            while (child != null)
            {
                if (child is PsbDictionary current)
                {
                    if (current.ContainsKey("label"))
                    {
                        paths.Add(current["label"].ToString());
                    }
                    else
                    {
                        paths.Add(current.GetName());
                    }
                }
                child = child.Parent;
            }
            paths.Reverse();
            return string.Join("/", paths);
        }

        public static IPsbValue Children(this IPsbValue col, string name)
        {
            while (true)
            {
                switch (col)
                {
                    case PsbDictionary dictionary:
                        return dictionary[name];
                    case PsbCollection collection:
                        col = collection.FirstOrDefault(c => c is PsbDictionary d && d.ContainsKey(name));
                        continue;
                }
                throw new ArgumentException($"{col} doesn't have children.");
            }
        }

        public static IPsbValue Children(this IPsbValue col, int index)
        {
            while (true)
            {
                switch (col)
                {
                    case PsbDictionary dictionary:
                        return dictionary.Values.ElementAt(index);
                    case PsbCollection collection:
                        col = collection[index];
                        continue;
                }
                throw new ArgumentException($"{col} doesn't have children.");
            }
        }

        #endregion
    }

    public class ByteListComparer : IComparer<IList<byte>>
    {
        public int Compare(IList<byte> x, IList<byte> y)
        {
            int result;
            int min = Math.Min(x.Count, y.Count);
            for (int index = 0; index < min; index++)
            {
                result = x[index].CompareTo(y[index]);
                if (result != 0) return result;
            }
            return x.Count.CompareTo(y.Count);
        }
    }

    public class StringListComparer : IComparer<IList<string>>
    {
        public int Compare(IList<string> x, IList<string> y)
        {
            int result;
            int min = Math.Min(x.Count, y.Count);
            for (int index = 0; index < min; index++)
            {
                result = String.Compare(x[index], y[index], StringComparison.Ordinal);
                if (result != 0) return result;
            }
            return x.Count.CompareTo(y.Count);
        }
    }
}
