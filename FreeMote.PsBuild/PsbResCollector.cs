using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using FreeMote.Psb;
using FreeMote.PsBuild.Converters;

namespace FreeMote.PsBuild
{
    public static class PsbResCollector
    {
        public const string ResourceIdentifier = "#resource#";
        public const string ResourceKey = "pixel";
        public const string SourceKey = "source";
        /// <summary>
        /// delimiter for output texture filename
        /// </summary>
        internal const string ResourceNameDelimiter = "-";

        /// <summary>
        /// Get all resources with necessary info
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="deDuplication">if true, We focus on Resource itself </param>
        /// <returns></returns>
        public static List<ResourceMetadata> CollectResources(this PSB psb, bool deDuplication = true)
        {
            List<ResourceMetadata> resourceList = psb.Resources == null ? new List<ResourceMetadata>() : new List<ResourceMetadata>(psb.Resources.Count);

            FindResources(resourceList, psb.Objects[SourceKey], deDuplication);
            //Set Spec
            resourceList.ForEach(r => r.Spec = psb.Platform);
            resourceList.Sort((md1, md2) => (int)(md1.Index - md2.Index));

            return resourceList;
        }

        private static void FindResources(List<ResourceMetadata> list, IPsbValue obj, bool deDuplication = true)
        {
            switch (obj)
            {
                case PsbCollection c:
                    c.ForEach(o => FindResources(list, o, deDuplication));
                    break;
                case PsbDictionary d:
                    if (d[ResourceKey] is PsbResource r)
                    {
                        if (!deDuplication)
                        {
                            list.Add(GenerateResourceMetadata(d, r));
                        }
                        else if (r.Index == null || list.FirstOrDefault(md => md.Index == r.Index.Value) == null)
                        {
                            list.Add(GenerateResourceMetadata(d, r));
                        }
                    }
                    foreach (var o in d.Values)
                    {
                        FindResources(list, o, deDuplication);
                    }
                    break;
            }
        }

        /// <summary>
        /// Extract resource info
        /// </summary>
        /// <param name="d">PsbObject which contains "pixel"</param>
        /// <param name="r">Resource</param>
        /// <returns></returns>
        internal static ResourceMetadata GenerateResourceMetadata(PsbDictionary d, PsbResource r = null)
        {
            if (r == null)
            {
                r = d.Values.FirstOrDefault(v => v is PsbResource) as PsbResource;
            }
            bool is2D = false;
            var part = d.GetPartName();
            var name = d.GetName();
            RectangleF clip = RectangleF.Empty;

            if (d["clip"] is PsbDictionary clipDic && clipDic.Count > 0)
            {
                is2D = true;
                clip = RectangleF.FromLTRB(
                    left: clipDic["left"] == null ? 0f : (float)(PsbNumber)clipDic["left"],
                    top: clipDic["top"] == null ? 0f : (float)(PsbNumber)clipDic["top"],
                    right: clipDic["right"] == null ? 1f : (float)(PsbNumber)clipDic["right"],
                    bottom: clipDic["bottom"] == null ? 1f : (float)(PsbNumber)clipDic["bottom"]
                );
            }
            var compress = PsbCompressType.None;
            if (d["compress"] is PsbString sc)
            {
                is2D = true;
                if (sc.Value.ToUpperInvariant() == "RL")
                {
                    compress = PsbCompressType.RL;
                }
            }
            int width = 1, height = 1;
            float originX = 0, originY = 0;
            if (d["width"] is PsbNumber nw)
            {
                is2D = true;
                width = (int)nw;
            }
            if (d["height"] is PsbNumber nh)
            {
                is2D = true;
                height = (int)nh;
            }
            if (d["originX"] is PsbNumber nx)
            {
                is2D = true;
                originX = (float)nx;
            }
            if (d["originY"] is PsbNumber ny)
            {
                is2D = true;
                originY = (float)ny;
            }
            string type = null;
            PsbString typeString = null;
            if (d["type"] is PsbString typeStr)
            {
                type = typeStr.Value;
                typeString = typeStr;
            }
            int top = 0, left = 0;
            if (d["top"] is PsbNumber nt)
            {
                is2D = true;
                top = (int)nt;
            }
            if (d["left"] is PsbNumber nl)
            {
                is2D = true;
                left = (int)nl;
            }
            var md = new ResourceMetadata()
            {
                Index = r.Index ?? int.MaxValue,
                Compress = compress,
                Name = name,
                Part = part,
                Clip = clip,
                Is2D = is2D,
                OriginX = originX,
                OriginY = originY,
                Top = top,
                Left = left,
                Width = width,
                Height = height,
                Type = type,
                TypeString = typeString,
                Resource = r,
            };
            return md;
        }

        /// <summary>
        /// Get related name on depth 3
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        private static string GetPartName(this IPsbChild c)
        {
            while (c != null)
            {
                if (c.Parent?.Parent?.Parent == null)
                {
                    return c.GetName();
                }
                c = c.Parent;
            }
            return null;
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
                case PsbSpec.common:
                case PsbSpec.win:
                case PsbSpec.other:
                default:
                    return PsbCompressType.None;
            }
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
                if (currentObj is PsbDictionary collection)
                {
                    path = path.Substring(pos);
                    return collection.FindByPath(path);
                }
            }
            return psbObj[path];
        }

        internal static IPsbValue Children(this IPsbValue col, string name)
        {
            while (true)
            {
                switch (col)
                {
                    case PsbDictionary dictionary:
                        return dictionary[name];
                    case PsbCollection collection:
                        col = collection.FirstOrDefault(c => c is PsbDictionary);
                        continue;
                }
                throw new ArgumentException($"{col} doesn't have children.");
            }
        }

        /// <summary>
        /// Try to switch Spec
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="targetSpec"></param>
        /// <param name="pixelFormat"></param>
        public static void SwitchSpec(this PSB psb, PsbSpec targetSpec, PsbPixelFormat pixelFormat = PsbPixelFormat.None)
        {
            if (targetSpec == PsbSpec.other)
            {
                return;
            }
            var original = psb.Platform;

            if (targetSpec == PsbSpec.krkr) //krkr can not select pixel format
            {
                switch (original)
                {
                    case PsbSpec.win:
                    {
                        Common2KrkrConverter converter = new Common2KrkrConverter();
                        converter.Convert(psb);
                        break;
                    }
                    case PsbSpec.common:
                    {
                        Common2KrkrConverter converter = new Common2KrkrConverter();
                        converter.Convert(psb);
                        break;
                    }
                    default:
                        psb.Platform = targetSpec;
                        break;
                }
            }

            if (targetSpec == PsbSpec.win)
            {
                switch (original)
                {
                    case PsbSpec.krkr:
                        Krkr2CommonConverter krkr2Common = new Krkr2CommonConverter(true);
                        krkr2Common.Convert(psb);
                        break;
                    case PsbSpec.common:
                        CommonWinConverter commonWin = new CommonWinConverter();
                        commonWin.Convert(psb);
                        break;
                    default:
                        psb.Platform = targetSpec;
                        break;
                }
            }

            if (targetSpec == PsbSpec.common)
            {
                switch (original)
                {
                    case PsbSpec.krkr:
                        Krkr2CommonConverter converter = new Krkr2CommonConverter();
                        converter.Convert(psb);
                        break;
                    case PsbSpec.win:
                        CommonWinConverter commonWin = new CommonWinConverter();
                        commonWin.Convert(psb);
                        break;
                    default:
                        psb.Platform = targetSpec;
                        break;
                }
            }
        }
    }
}
