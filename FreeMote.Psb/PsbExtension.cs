using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FreeMote.Plugins;

namespace FreeMote.Psb
{
    public static class PsbExtension
    {
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

        /// <summary>
        /// <paramref name="compress"/> to its file extension
        /// </summary>
        /// <param name="compress"></param>
        /// <returns></returns>
        public static string ToExtensionString(this PsbCompressType compress)
        {
            switch (compress)
            {
                case PsbCompressType.Tlg:
                    return ".tlg";
                case PsbCompressType.Astc:
                    return ".astc";
                case PsbCompressType.RL:
                    return ".rl";
                default:
                    return "";
            }
        }

        private static bool ApplyDefaultMotionMetadata(PSB psb, PsbDictionary metadata)
        {
            List<(string Chara, string Motion)> knownMotions = new List<(string Chara, string Motion)>
            {
                ("body_parts", "全身変形基礎"),
                ("body_parts", "下半身変形基礎"),
                ("head_parts", "頭部変形基礎"),
                ("head_parts", "頭部全体変形"),
                ("all_parts", "タイムライン構造"),
                ("all_parts", "全体構造")
            };

            //Find a known pattern
            foreach (var knownMotion in knownMotions)
            {
                if (psb.Objects.FindByPath($"/object/{knownMotion.Chara}") is PsbDictionary knownDic && knownDic["motion"].Children(knownMotion.Motion) is PsbDictionary motionDic && motionDic.Count > 0)
                {
                    metadata["chara"] = knownMotion.Chara.ToPsbString();
                    metadata["motion"] = knownMotion.Motion.ToPsbString();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Fix [metadata/base/motion] missing issue for <seealso cref="PsbType.Motion"/> PSB 
        /// </summary>
        /// <param name="psb"></param>
        /// <returns>Whether the PSB is confirmed to be fine already or fine after fixed</returns>
        public static bool FixMotionMetadata(this PSB psb)
        {
            if (psb.Objects.FindByPath("/metadata/base") is PsbDictionary dic && dic["chara"] is PsbString chara && dic["motion"] is PsbString motion)
            {
                var realChara = psb.Objects.FindByPath($"/object/{chara}") as PsbDictionary;
                if (realChara == null) //chara not exist
                {
                    //dic.Clear();
                    return ApplyDefaultMotionMetadata(psb, dic);
                }

                var realMotion = realChara["motion"] as PsbDictionary;

                if (realMotion == null || realMotion.Count == 0) //motion not exist and nothing can replace it
                {
                    return ApplyDefaultMotionMetadata(psb, dic);
                    //TODO: find a nice replacement, usually head_parts/頭部変形基礎(1st) or 頭部全体変形(2nd)
                    //TODO: Build a tree and pick the top
                }

                if (realMotion.ContainsKey(motion)) //ok, no need to fix
                {
                    return true;
                }

                dic["motion"] = realMotion.Keys.Last().ToPsbString(); //pick the last motion to replace it, the last usually covers most 
                return true;
            }

            return false;
        }

        /// <summary>
        /// Try to measure EMT PSB Canvas Size
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns>True: The canvas size can be measured; False: can not get canvas size</returns>
        public static bool TryGetCanvasSize(this PSB psb, out int width, out int height)
        {
            //Get from CharProfile
            if (psb.Objects["metadata"] is PsbDictionary md && md["charaProfile"] is PsbDictionary cp &&
                cp["pixelMarker"] is PsbDictionary pm
                && pm["boundsBottom"] is PsbNumber b && pm["boundsTop"] is PsbNumber t &&
                pm["boundsLeft"] is PsbNumber l && pm["boundsRight"] is PsbNumber r)
            {
                height = (int)Math.Abs(b.AsFloat - t.AsFloat);
                width = (int)Math.Abs(r.AsFloat - l.AsFloat);
                return true;
            }

            //not really useful
            var resList = psb.CollectResources<ImageMetadata>();
            width = resList.Max(data => data.Width);
            height = resList.Max(data => data.Height);
            return false;
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

            if (c?.Parent is PsbList col)
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

        public static PsbString ToPsbString(this string s)
        {
            return s == null ? PsbString.Empty : new PsbString(s);
        }

        public static PsbNumber ToPsbNumber(this int i)
        {
            return new(i);
        }

        /// <summary>
        /// Set archData value to archData object
        /// </summary>
        /// <param name="archData"></param>
        /// <param name="val"></param>
        public static void SetPsbArchData(this IArchData archData, IPsbValue val)
        {
            archData.PsbArchData["archData"] = val;
        }

        #region Object Finding

        /// <summary>
        /// Quickly fetch children (use at your own risk)
        /// </summary>
        /// <param name="col"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static IPsbValue Children(this IPsbValue col, string name)
        {
            while (true)
            {
                switch (col)
                {
                    case PsbDictionary dictionary:
                        return dictionary[name];
                    case PsbList collection:
                        col = collection.FirstOrDefault(c => c is PsbDictionary d && d.ContainsKey(name));
                        continue;
                }

                throw new ArgumentException($"{col} doesn't have children.");
            }
        }

        /// <summary>
        /// Quickly fetch number (use at your own risk)
        /// </summary>
        /// <param name="col"></param>
        /// <returns></returns>
        public static int GetInt(this IPsbValue col)
        {
            return ((PsbNumber)col).AsInt;
        }

        /// <summary>
        /// Quickly fetch number (use at your own risk)
        /// </summary>
        /// <param name="col"></param>
        /// <returns></returns>
        public static float GetFloat(this IPsbValue col)
        {
            return ((PsbNumber)col).AsFloat;
        }

        /// <summary>
        /// Quickly fetch number (use at your own risk)
        /// </summary>
        /// <param name="col"></param>
        /// <returns></returns>
        public static double GetDouble(this IPsbValue col)
        {
            return ((PsbNumber)col).AsDouble;
        }

        /// <summary>
        /// Quickly fetch children (use at your own risk)
        /// </summary>
        /// <param name="col"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static IPsbValue Children(this IPsbValue col, int index)
        {
            switch (col)
            {
                case PsbDictionary dictionary:
                    return dictionary.Values.ElementAt(index);
                case PsbList collection:
                    return collection[index];
            }

            throw new ArgumentException($"{col} doesn't have children.");
        }

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
                        foreach (var dicValue in psbObj.Values)
                        {
                            yield return dicValue;
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

                if (currentObj is PsbList collection)
                {
                    path = path.Substring(pos);
                    return collection.FindByPath(path);
                }
            }

            return psbObj[path];
        }

        /// <inheritdoc cref="FindByPath(FreeMote.Psb.PsbDictionary,string)"/>
        public static IPsbValue FindByPath(this PsbList psbObj, string path)
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

                if (current.StartsWith("[") && current.EndsWith("]") &&
                    Int32.TryParse(current.Substring(1, current.Length - 2), out var id))
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

                if (currentObj is PsbList collection)
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
            if (psbObj is PsbList col)
            {
                currentObj = col.FirstOrDefault(c =>
                    c is PsbDictionary d && d.ContainsKey("label") && d["label"] is PsbString s &&
                    s.Value == current);
            }
            else if (psbObj is PsbDictionary dic)
            {
                //var dd = dic.Value.FirstOrDefault();
                var children =
                    (PsbList)(dic.ContainsKey("layerChildren") ? dic["layerChildren"] : dic["children"]);
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

        #endregion

        #region Context

        internal static bool UseFlattenArray(this FreeMountContext context)
        {
            return context.Context.ContainsKey(Consts.Context_UseFlattenArray) && context.Context[Consts.Context_UseFlattenArray] is bool use && use == true;
        }

        internal static void SetUseFlattenArray(this FreeMountContext context, bool use)
        {
            context.Context[Consts.Context_UseFlattenArray] = use;
        }

        #endregion

        #region MDF

        /// <summary>
        /// Save PSB as pure MDF file
        /// </summary>
        /// <remarks>can not save as impure MDF (such as MT19937 MDF)</remarks>
        /// <param name="psb"></param>
        /// <param name="path"></param>
        /// <param name="key"></param>
        public static void SaveAsMdfFile(this PSB psb, string path, uint? key = null)
        {
            psb.Merge();
            var bytes = psb.Build();
            Adler32 adler = new Adler32();
            uint checksum = 0;
            if (key == null)
            {
                adler.Update(bytes);
                checksum = (uint)adler.Checksum;
            }

            MemoryStream ms = new MemoryStream(bytes);
            using (Stream fs = new FileStream(path, FileMode.Create))
            {
                if (key != null)
                {
                    MemoryStream nms = new MemoryStream((int)ms.Length);
                    PsbFile.Encode(key.Value, EncodeMode.Encrypt, EncodePosition.Auto, ms, nms);
                    ms.Dispose();
                    ms = nms;
                    var pos = ms.Position;
                    adler.Update(ms);
                    checksum = (uint)adler.Checksum;
                    ms.Position = pos;
                }

                BinaryWriter bw = new BinaryWriter(fs);
                bw.WriteStringZeroTrim(MdfFile.Signature);
                bw.Write((uint)ms.Length);
                //bw.Write(ZlibCompress.Compress(ms));
                ZlibCompress.CompressToBinaryWriter(bw, ms);
                bw.WriteBE(checksum);
                ms.Dispose();
                bw.Flush();
            }
        }

        /// <summary>
        /// Save as pure MDF
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static byte[] SaveAsMdf(this PSB psb, uint? key = null)
        {
            psb.Merge();
            var bytes = psb.Build();
            Adler32 adler = new Adler32();
            uint checksum = 0;
            if (key == null)
            {
                adler.Update(bytes);
                checksum = (uint)adler.Checksum;
            }

            MemoryStream ms = new MemoryStream(bytes);
            using (MemoryStream fs = new MemoryStream())
            {
                if (key != null)
                {
                    MemoryStream nms = new MemoryStream((int)ms.Length);
                    PsbFile.Encode(key.Value, EncodeMode.Encrypt, EncodePosition.Auto, ms, nms);
                    ms.Dispose();
                    ms = nms;
                    var pos = ms.Position;
                    adler.Update(ms);
                    checksum = (uint)adler.Checksum;
                    ms.Position = pos;
                }

                BinaryWriter bw = new BinaryWriter(fs);
                bw.WriteStringZeroTrim(MdfFile.Signature);
                bw.Write((uint)ms.Length);
                //bw.Write(ZlibCompress.Compress(ms));
                ZlibCompress.CompressToBinaryWriter(bw, ms);
                bw.WriteBE(checksum);
                ms.Dispose();
                bw.Flush();
                return fs.ToArray();
            }
        }

        #endregion

        #region PSB Parser

        public static bool ByteArrayEqual(this byte[] a1, byte[] a2)
        {
            if (a1 == null && a2 == null)
            {
                return true;
            }
            if (a1 == null || a2 == null)
            {
                return false;
            }
            if (a1.Length != a2.Length)
            {
                return false;
            }
            return ByteSpanEqual(a1, a2);
        }

        /// <summary>
        /// Fast compare byte array
        /// </summary>
        /// <param name="a1"></param>
        /// <param name="a2"></param>
        /// <returns></returns>
        public static bool ByteSpanEqual(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2)
        {
            return a1.SequenceEqual(a2);
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
            bool firstBitOne =
                hex[0] >= '8' &&
                hex.Length % 2 == 0; //FIXED: Extend size if first bit is 1 //FIXED: 0x0F is +, 0xFF is -, 0x0FFF is +

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
            bool firstBitOne =
                hex[0] >= '8' &&
                hex.Length % 2 == 0; //FIXED: Extend size if first bit is 1 //FIXED: 0x0F is +, 0xFF is -, 0x0FFF is +

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

        public static uint ReadCompactUInt(this BinaryReader br, byte size)
        {
            return br.ReadBytes(size).UnzipUInt();
        }

        public static void ReadAndUnzip(this BinaryReader br, byte size, byte[] data, bool unsigned = false)
        {
            br.Read(data, 0, size);

            byte fill = 0x0;
            if (!unsigned && (data[size - 1] >= 0b10000000)) //negative
            {
                fill = 0xFF;
            }

            for (int i = 0; i < data.Length; i++)
            {
                data[i] = i < size ? data[i] : fill;
            }
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
            var span = BitConverter.GetBytes(i);

            return span.Take(size <= 0 ? i.GetSize() : size).ToArray();
        }

        public static byte[] UnzipNumberBytes(this byte[] b, int size = 8, bool unsigned = false)
        {
            byte[] r = new byte[size];
            if (!unsigned && (b[b.Length - 1] >= 0b10000000)) //negative
            {
                for (int i = 0; i < r.Length; i++)
                {
                    r[i] = (0xFF);
                }
            }

            b.CopyTo(r, 0);

            return r;
        }

        public static void UnzipNumberBytes(this byte[] b, byte[] data, bool unsigned = false)
        {
            if (!unsigned && (b[b.Length - 1] >= 0b10000000)) //negative
            {
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = (0xFF);
                }
            }

            b.CopyTo(data, 0);
        }

        public static long UnzipNumber(this byte[] b)
        {
            return BitConverter.ToInt64(b.UnzipNumberBytes(), 0);
        }

        public static uint UnzipUInt(this byte[] b)
        {
            //return BitConverter.ToUInt32(b.UnzipNumberBytes(4, true), 0);

            //optimized with Span<T>
            Span<byte> span = stackalloc byte[4];
            for (int i = 0; i < Math.Min(b.Length, 4); i++)
            {
                span[i] = b[i];
            }

            return MemoryMarshal.Read<uint>(span);
        }

        public static uint UnzipUInt(this byte[] b, int start, byte size)
        {
            //return BitConverter.ToUInt32(b.UnzipNumberBytes(4, true), 0);

            //optimized with Span<T>
            Span<byte> span = stackalloc byte[4];
            for (int i = 0; i < Math.Min(size, (byte)4); i++)
            {
                span[i] = b[start + i];
            }

            return MemoryMarshal.Read<uint>(span);
        }

        #endregion

        #region Archive

        /// <summary>
        /// Remove suffix for file name in archive info file_info
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="suffix"></param>
        /// <returns></returns>
        public static string ArchiveInfoGetFileNameRemoveSuffix(string fileName, string suffix)
        {
            if (string.IsNullOrEmpty(suffix))
            {
                return fileName;
            }

            if (fileName.EndsWith(suffix, true, CultureInfo.InvariantCulture))
            {
                return fileName.Remove(fileName.Length - suffix.Length, suffix.Length);
            }

            return fileName;
        }

        /// <summary>
        /// Append suffix for file name in archive info file_info
        /// </summary>
        /// <param name="name"></param>
        /// <param name="suffix"></param>
        /// <returns></returns>
        public static string ArchiveInfoGetFileNameAppendSuffix(string name, string suffix)
        {
            //if a file name ends with .xxx.m (like abc.nut.m), it's a naughty bad file with its own suffix. However, abc.m is not considered as such
            if ((name.EndsWith(".m") && name.Count(c => c == '.') > 1) || name.EndsWith(".psb"))
            {
                return name;
            }

            return name + suffix;

        }

        /* the "amazing" design of archive psb:
        "expire_suffix_list": [".psb.m"]
        "image/man003" -> packed with key man003.psb.m
        "scenario/ca01_06.txt.scn.m" -> packed with key ca01_06.txt.scn.m (?)
        "script/ikusei.nut.m" -> packed with key ikusei.nut.m (ok fine)
        "sound/bgm.psb" -> not packed (??)
        "bg_c_whit.m" -> packed with key bg_c_whit.m.psb.m (???)
         */

        /// <summary>
        /// get all possible file names (used as key) for items in archive psb, the first one is default (most confident)
        /// </summary>
        /// <param name="name"></param>
        /// <param name="suffix"></param>
        /// <returns></returns>
        public static List<string> ArchiveInfoGetAllPossibleFileNames(string name, string suffix)
        {
            List<string> exts = new List<string>();
            var name2 = name;
            while (Path.GetExtension(name2) != string.Empty)
            {
                exts.Add(Path.GetExtension(name2));
                name2 = Path.ChangeExtension(name2, null);
            }

            List<string> results = new List<string>();
            if (exts.Count == 0)
            {
                //"image/man003" -> man003.psb.m
                results.Add(name + suffix);
                results.Add(name);
            }
            else //exts.Count > 0
            {
                if (exts[0].ToLowerInvariant() == ".m" && exts.Count == 1)
                {
                    //"bg_c_whit.m" -> bg_c_whit.m.psb.m
                    results.Add(name + suffix);
                    results.Add(name);
                    results.Add(Path.ChangeExtension(name, null));
                }
                else
                {
                    //"scenario/ca01_06.txt.scn.m" -> ca01_06.txt.scn.m
                    //"script/ikusei.nut.m" -> ikusei.nut.m
                    //"sound/bgm.psb" -> null
                    results.Add(name);
                    results.Add(name + suffix);
                    name2 = name;
                    while (Path.GetExtension(name2) != string.Empty)
                    {
                        name2 = Path.ChangeExtension(name2, null);
                        results.Add(name2);
                        results.Add(name2 + suffix);
                    }
                }
            }

            //stress test
            //results.Reverse();

            return results;
        }

        /// <summary>
        /// Collect (possible) file names in archive info file_info
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="suffix"></param>
        /// <returns></returns>
        public static IEnumerable<string> ArchiveInfoCollectFiles(PSB psb, string suffix)
        {
            if (psb.Objects.ContainsKey("file_info") && psb.Objects["file_info"] is PsbDictionary fileInfo)
            {
                foreach (var name in fileInfo.Keys)
                {
                    //yield return ArchiveInfoGetFileNameAppendSuffix(name, suffix);
                    //foreach (var fileName in ArchiveInfoGetAllPossibleFileNames(name, suffix))
                    //{
                    //    yield return fileName;
                    //}
                    yield return ArchiveInfoGetAllPossibleFileNames(name, suffix).FirstOrDefault();
                }
            }
        }

        /// <summary>
        /// Get suffix in archive info
        /// </summary>
        /// <param name="psb"></param>
        /// <returns></returns>
        public static string ArchiveInfoGetSuffix(PSB psb)
        {
            var suffix = "";
            if (psb.Objects.ContainsKey("expire_suffix_list") &&
                psb.Objects["expire_suffix_list"] is PsbList col && col[0] is PsbString s)
            {
                suffix = s;
            }

            return suffix;
        }

        /// <summary>
        /// Get package name from a string like {package name}_info.psb.m
        /// </summary>
        /// <param name="fileName">e.g. {package name}_info.psb.m</param>
        /// <returns>{package name}, can be null if failed</returns>
        public static string ArchiveInfoGetPackageName(string fileName)
        {
            var nameSlicePos = fileName.IndexOf("_info.", StringComparison.Ordinal);
            string name = null;
            if (nameSlicePos > 0)
            {
                name = fileName.Substring(0, nameSlicePos);
            }
            else
            {
                nameSlicePos = fileName.IndexOf(".", StringComparison.Ordinal);
                if (nameSlicePos > 0)
                {
                    name = fileName.Substring(0, nameSlicePos);
                }
            }

            return name;
        }

        #endregion

        /// <summary>
        /// Get the second file name extension.
        /// <example>e.g. get ".vag" from "audio.vag.wav"</example>
        /// </summary>
        /// <param name="path"></param>
        /// <returns>null if input is null; <see cref="String.Empty"/> if no second extension</returns>
        internal static string GetSecondExtension(this string path)
        {
            return Path.GetExtension(Path.GetFileNameWithoutExtension(path))?.ToLowerInvariant();
        }
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