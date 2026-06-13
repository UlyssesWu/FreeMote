using FreeMote.Psb.Textures;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace FreeMote.Psb
{
    internal class StaticMotionPainterCore
    {
        private const int TimelineFrameTypeNull = 0;
        private const int TimelineFrameTypeSingle = 1;
        private const int TimelineFrameTypeContinuous = 2;
        private const int TimelineFrameTypeTween = 3;

        private const int TransformFlip = 0;
        private const int TransformAngle = 1;
        private const int TransformZoom = 2;
        private const int TransformSlant = 3;

        private readonly PSB _source;
        private readonly List<ImageMetadata> _allResources;
        private readonly Dictionary<string, float> _representativeTimeCache = new Dictionary<string, float>();

        public StaticMotionPainterCore(PSB source, List<ImageMetadata> allResources)
        {
            _source = source;
            _allResources = allResources;
        }

        public List<string> GetMotionNames(string chara)
        {
            var list = new List<string>();
            var motionDic = GetMotionDictionary(chara);
            if (motionDic == null)
            {
                return list;
            }

            list.AddRange(motionDic.Keys);
            return list;
        }

        public Bitmap DrawMotion(string chara, string motion, int width = 0, int height = 0)
        {
            return DrawMotionAt(chara, motion, GetRepresentativeTime(chara, motion), width, height);
        }

        public Bitmap DrawMotionAt(string chara, string motion, float time, int width = 0, int height = 0)
        {
            return DrawResources(CollectResources(chara, motion, time, NestedMotionTimeMode.Representative), width, height);
        }

        public List<StaticDrawableResource> CollectAllMotionsAtRepresentativeTime(string chara)
        {
            var result = new List<StaticDrawableResource>();
            foreach (var motion in GetMotionNames(chara))
            {
                result.AddRange(CollectResources(chara, motion, GetRepresentativeTime(chara, motion), NestedMotionTimeMode.Representative));
            }

            return result.OrderBy(res => res.Z).ToList();
        }

        public List<StaticDrawableResource> CollectAllMotionsAtTime(string chara, float time)
        {
            var result = new List<StaticDrawableResource>();
            foreach (var motion in GetMotionNames(chara))
            {
                result.AddRange(CollectResources(chara, motion, time, NestedMotionTimeMode.Initial));
            }

            return result.OrderBy(res => res.Z).ToList();
        }

        public List<StaticDrawableResource> CollectAllMotionsAtDefaultTime(string chara)
        {
            var result = new List<StaticDrawableResource>();
            foreach (var motion in GetMotionNames(chara))
            {
                result.AddRange(CollectResources(chara, motion, GetDefaultTime(chara, motion), NestedMotionTimeMode.Default));
            }

            return result.OrderBy(res => res.Z).ToList();
        }

        public Bitmap DrawResources(IEnumerable<StaticDrawableResource> resources, int width, int height)
        {
            bool autoSize = width <= 0 && height <= 0;
            var drawRes = FilterDrawableResources(resources).ToList();

            if (drawRes.Count == 0)
            {
                return null;
            }

            Bitmap bmp;
            float xOffset;
            float yOffset;
            if (autoSize)
            {
                var bounds = GetBounds(drawRes);
                xOffset = -bounds.Left;
                yOffset = -bounds.Top;

                width = Math.Max(1, (int)Math.Ceiling(bounds.Right - bounds.Left));
                height = Math.Max(1, (int)Math.Ceiling(bounds.Bottom - bounds.Top));
                bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            }
            else
            {
                xOffset = width / 2f;
                yOffset = height / 2f;
                bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            }

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CompositingMode = CompositingMode.SourceOver;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;

                foreach (var res in drawRes)
                {
                    DrawResource(g, res, xOffset, yOffset);
                }
            }

            return bmp;
        }

        public (int Width, int Height, float OffsetX, float OffsetY) GetCanvasSize(IEnumerable<StaticDrawableResource> resources)
        {
            var drawRes = FilterDrawableResources(resources).ToList();
            if (drawRes.Count == 0)
            {
                return (0, 0, 0, 0);
            }

            var bounds = GetBounds(drawRes);
            return (
                Math.Max(1, (int)Math.Ceiling(bounds.Right - bounds.Left)),
                Math.Max(1, (int)Math.Ceiling(bounds.Bottom - bounds.Top)),
                -bounds.Left,
                -bounds.Top);
        }

        public List<StaticDrawableResource> CollectResources(string chara, string motion, float time)
        {
            return CollectResources(chara, motion, time, NestedMotionTimeMode.Representative);
        }

        public List<StaticDrawableResource> CollectResourcesAtInitialTime(string chara, string motion, float time)
        {
            return CollectResources(chara, motion, time, NestedMotionTimeMode.Initial);
        }

        public List<StaticDrawableResource> CollectResourcesAtDefaultTime(string chara, string motion)
        {
            return CollectResources(chara, motion, GetDefaultTime(chara, motion), NestedMotionTimeMode.Default);
        }

        private List<StaticDrawableResource> CollectResources(string chara, string motion, float time, NestedMotionTimeMode nestedMotionTimeMode)
        {
            var result = new List<StaticDrawableResource>();
            var layerCol = GetLayerCollection(chara, motion);
            if (layerCol == null)
            {
                return result;
            }

            var ctx = new StaticRenderContext();
            Travel(layerCol, chara, motion, time, ctx, new HashSet<string>());

            return result.OrderBy(metadata => metadata.Z).ToList();

            void Travel(IPsbValue collection, string motionChara, string motionName, float renderTime, StaticRenderContext parentCtx, HashSet<string> stack)
            {
                if (collection is PsbDictionary dic)
                {
                    if (dic.TryGetValue("frameList", out var frameListValue) && frameListValue is PsbList frameList)
                    {
                        var currentCtx = parentCtx;
                        var frame = GetCompleteFrameContent(frameList, renderTime);
                        if (frame != null)
                        {
                            currentCtx = BuildContext(dic, frame, parentCtx);
                            TryAddResource(dic, frame, motionChara, motionName, currentCtx, stack);
                        }

                        if (dic.TryGetValue("children", out var children) && children is PsbList childList && IsExportSelf(dic))
                        {
                            Travel(childList, motionChara, motionName, renderTime, currentCtx, stack);
                        }

                        if (dic.TryGetValue("layer", out var layer) && layer is PsbList layerList && IsExportSelf(dic))
                        {
                            Travel(layerList, motionChara, motionName, renderTime, currentCtx, stack);
                        }

                        return;
                    }

                    if (dic.TryGetValue("children", out var ccol) && ccol is PsbList ccolList && IsExportSelf(dic))
                    {
                        Travel(ccolList, motionChara, motionName, renderTime, parentCtx, stack);
                    }

                    if (dic.TryGetValue("layer", out var ccoll) && ccoll is PsbList ccollList && IsExportSelf(dic))
                    {
                        Travel(ccollList, motionChara, motionName, renderTime, parentCtx, stack);
                    }
                }
                else if (collection is PsbList list)
                {
                    foreach (var cc in list)
                    {
                        Travel(cc, motionChara, motionName, renderTime, parentCtx, stack);
                    }
                }
            }

            void TryAddResource(PsbDictionary layer, StaticFrameContent frame, string motionChara, string motionName, StaticRenderContext ctx, HashSet<string> stack)
            {
                if (string.IsNullOrEmpty(frame.Source))
                {
                    return;
                }

                var labelName = layer.TryGetValue("label", out var label) ? label.ToString() : string.Empty;
                var src = frame.Source;

                if (src.StartsWith("src/"))
                {
                    var iconName = src.Substring(src.LastIndexOf('/') + 1);
                    var partName = new string(src.SkipWhile(c => c != '/').Skip(1).TakeWhile(c => c != '/').ToArray());
                    var res = _allResources.FirstOrDefault(resMd => resMd.Part == partName && resMd.Name == iconName);
                    if (res != null)
                    {
                        result.Add(new StaticDrawableResource(res, labelName, motionName, ctx.X, ctx.Y, ctx.Z, ctx.Matrix, frame.Ox, frame.Oy, ctx.Opacity, ctx.Visible));
                    }
                }
                else if (src.StartsWith("motion/"))
                {
                    var ps = src.Substring("motion/".Length).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (ps.Length >= 2)
                    {
                        TravelMotion(ps[0], ps[1], ctx, stack);
                    }
                }
                else if (!string.IsNullOrEmpty(frame.Icon) && _source.Objects["source"] is PsbDictionary sourceDic && sourceDic.ContainsKey(src))
                {
                    var res = _allResources.FirstOrDefault(resMd => resMd.Part == src && resMd.Name == frame.Icon);
                    if (res != null)
                    {
                        result.Add(new StaticDrawableResource(res, labelName, motionName, ctx.X, ctx.Y, ctx.Z, ctx.Matrix, frame.Ox, frame.Oy, ctx.Opacity, ctx.Visible));
                    }
                }
                else if (!string.IsNullOrEmpty(frame.Icon) && _source.Objects["object"] is PsbDictionary objectDic && objectDic.ContainsKey(src))
                {
                    TravelMotion(src, frame.Icon, ctx, stack);
                }
            }

            void TravelMotion(string motionChara, string motionName, StaticRenderContext ctx, HashSet<string> stack)
            {
                var key = $"{motionChara}/{motionName}";
                if (!stack.Add(key))
                {
                    return;
                }

                var motionLayer = GetLayerCollection(motionChara, motionName);
                if (motionLayer != null)
                {
                    // E-mote model timelines are often parameter axes, not elapsed time. Frame 0 can be
                    // the minimum value of a variable; default display should map variable 0 to the axis.
                    var nestedTime = nestedMotionTimeMode == NestedMotionTimeMode.Initial ? 0 :
                        nestedMotionTimeMode == NestedMotionTimeMode.Default ? GetDefaultTime(motionChara, motionName) :
                        GetRepresentativeTime(motionChara, motionName);
                    Travel(motionLayer, motionChara, motionName, nestedTime, ctx, stack);
                }

                stack.Remove(key);
            }
        }

        private IEnumerable<StaticDrawableResource> FilterDrawableResources(IEnumerable<StaticDrawableResource> resources)
        {
            return resources.Where(res =>
                res.Visible &&
                res.Opacity > 0 &&
                (!res.Metadata.Name.StartsWith("icon") || res.Metadata.Name == "icon1"));
        }

        private void DrawResource(Graphics g, StaticDrawableResource res, float xOffset, float yOffset)
        {
            using (var image = res.Metadata.ToImage())
            {
                var points = res.GetDestPoints(xOffset, yOffset);
                Debug.WriteLine($"Drawing {res.Metadata} at {points[0]} opacity:{res.Opacity} z:{res.Z}");

                if (res.Opacity >= 255)
                {
                    g.DrawImage(image, points);
                    return;
                }

                var matrix = new ColorMatrix();
                using (var attributes = new ImageAttributes())
                {
                    matrix.Matrix33 = Math.Max(0, Math.Min(255, res.Opacity)) / 255.0f;
                    attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    g.DrawImage(image, points, new RectangleF(0, 0, image.Width, image.Height), GraphicsUnit.Pixel, attributes);
                }
            }
        }

        private RectangleF GetBounds(List<StaticDrawableResource> drawRes)
        {
            var first = true;
            float minX = 0, minY = 0, maxX = 0, maxY = 0;
            foreach (var res in drawRes)
            {
                foreach (var p in res.GetCorners())
                {
                    if (first)
                    {
                        minX = maxX = p.X;
                        minY = maxY = p.Y;
                        first = false;
                    }
                    else
                    {
                        minX = Math.Min(minX, p.X);
                        minY = Math.Min(minY, p.Y);
                        maxX = Math.Max(maxX, p.X);
                        maxY = Math.Max(maxY, p.Y);
                    }
                }
            }

            return RectangleF.FromLTRB(minX, minY, maxX, maxY);
        }

        private float GetRepresentativeTime(string chara, string motion)
        {
            var key = $"{chara}/{motion}";
            if (_representativeTimeCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var layerCol = GetLayerCollection(chara, motion);
            if (layerCol == null)
            {
                _representativeTimeCache[key] = 0;
                return 0;
            }

            _representativeTimeCache[key] = 0;

            var candidates = new SortedSet<float> { 0 };
            CollectCandidateTimes(layerCol, candidates);

            float bestTime = 0;
            StaticRenderScore bestScore = default;
            foreach (var time in candidates)
            {
                // For MTN thumbnails frame 0 is often only a sparse setup frame, so choose the frame
                // with the most visible drawable content instead of blindly taking the timeline start.
                var score = ScoreResource(CollectResources(chara, motion, time, NestedMotionTimeMode.Representative));
                if (score.CompareTo(bestScore) > 0)
                {
                    bestScore = score;
                    bestTime = time;
                }
            }

            _representativeTimeCache[key] = bestTime;
            return bestTime;
        }

        private float GetDefaultTime(string chara, string motion)
        {
            var motionValue = GetMotionValue(chara, motion);
            var parameterList = motionValue?.Children("parameter") as PsbList;
            if (parameterList == null)
            {
                return 0;
            }

            foreach (var parameterValue in parameterList.OfType<PsbDictionary>())
            {
                if (!GetBool(parameterValue, "enabled", true))
                {
                    continue;
                }

                var rangeBegin = GetFloat(parameterValue, "rangeBegin", 0);
                var rangeEnd = GetFloat(parameterValue, "rangeEnd", 0);
                var division = GetFloat(parameterValue, "division", 0);
                if (Math.Abs(rangeEnd - rangeBegin) < 0.0001f || division <= 0)
                {
                    continue;
                }

                if (rangeBegin <= 0 && rangeEnd >= 0)
                {
                    return (0 - rangeBegin) * division / (rangeEnd - rangeBegin);
                }
            }

            return 0;
        }

        private StaticRenderScore ScoreResource(List<StaticDrawableResource> resources)
        {
            var score = new StaticRenderScore();
            foreach (var res in resources)
            {
                if (!res.Visible || res.Opacity <= 0)
                {
                    continue;
                }

                score.Count++;
                score.Area += (long)res.Metadata.Width * res.Metadata.Height;
                score.Opacity += res.Opacity;
            }

            return score;
        }

        private void CollectCandidateTimes(IPsbValue collection, SortedSet<float> candidates)
        {
            if (collection is PsbDictionary dic)
            {
                if (dic.TryGetValue("frameList", out var frameListValue) && frameListValue is PsbList frameList)
                {
                    foreach (var frame in frameList.OfType<PsbDictionary>())
                    {
                        candidates.Add(GetFrameTime(frame));
                    }
                }

                if (dic.TryGetValue("children", out var children) && children is PsbList childList)
                {
                    CollectCandidateTimes(childList, candidates);
                }

                if (dic.TryGetValue("layer", out var layer) && layer is PsbList layerList)
                {
                    CollectCandidateTimes(layerList, candidates);
                }
            }
            else if (collection is PsbList list)
            {
                foreach (var item in list)
                {
                    CollectCandidateTimes(item, candidates);
                }
            }
        }

        private PsbDictionary GetMotionDictionary(string chara)
        {
            var objects = _source.Objects["object"] as PsbDictionary;
            if (objects == null || !objects.TryGetValue(chara, out var charaValue))
            {
                return null;
            }

            return charaValue.Children("motion") as PsbDictionary;
        }

        private PsbList GetLayerCollection(string chara, string motion)
        {
            return GetMotionValue(chara, motion)?.Children("layer") as PsbList;
        }

        private IPsbValue GetMotionValue(string chara, string motion)
        {
            var motionDic = GetMotionDictionary(chara);
            if (motionDic == null || !motionDic.TryGetValue(motion, out var motionValue))
            {
                return null;
            }

            return motionValue;
        }

        private StaticRenderContext BuildContext(PsbDictionary layer, StaticFrameContent frame, StaticRenderContext parent)
        {
            var local = CalcAffineMatrix(GetTransformOrder(layer), frame.FlipX, frame.FlipY, frame.Angle, frame.ZoomX, frame.ZoomY, frame.SlantX, frame.SlantY);
            var ctx = new StaticRenderContext();
            var transformed = parent.Matrix.Transform(frame.CoordX, frame.CoordY);

            ctx.X = parent.X + transformed.X;
            ctx.Y = parent.Y + transformed.Y;
            ctx.Z = parent.Z + frame.CoordZ;
            ctx.Visible = parent.Visible;
            ctx.Opacity = GetBool(layer, "inheritOpacity", true)
                ? (int)Math.Round(parent.Opacity * frame.Opacity / 255.0)
                : frame.Opacity;
            // The editor has per-transform inherit flags. When they are not all enabled, reproducing the
            // exact mixed parent/local transform needs much more state, so keep the local affine fallback.
            ctx.Matrix = InheritAllAffine(layer) ? StaticMotionMatrix2.Multiply(parent.Matrix, local) : local;

            return ctx;
        }

        private StaticFrameContent GetCompleteFrameContent(PsbList frameList, float time)
        {
            var frameIndex = FindFrame(frameList, time);
            if (frameIndex < 0)
            {
                return null;
            }

            var frame = frameList[frameIndex] as PsbDictionary;
            if (frame == null)
            {
                return null;
            }

            var frameType = GetFrameType(frame);
            switch (frameType)
            {
                case TimelineFrameTypeNull:
                    return null;
                case TimelineFrameTypeSingle:
                    return Math.Abs(GetFrameTime(frame) - time) < 0.001f ? ReadFrameContent(frame) : null;
                case TimelineFrameTypeContinuous:
                case TimelineFrameTypeTween:
                    return ReadFrameContent(frame);
                default:
                    return ReadFrameContent(frame);
            }
        }

        private int FindFrame(PsbList frameList, float time)
        {
            var result = -1;
            for (var i = 0; i < frameList.Count; i++)
            {
                if (!(frameList[i] is PsbDictionary frame))
                {
                    continue;
                }

                if (GetFrameTime(frame) <= time)
                {
                    result = i;
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        private StaticFrameContent ReadFrameContent(PsbDictionary frame)
        {
            if (!frame.TryGetValue("content", out var contentValue) || !(contentValue is PsbDictionary content))
            {
                return null;
            }

            var coord = content.TryGetValue("coord", out var coordValue) ? coordValue as PsbList : null;
            return new StaticFrameContent
            {
                Source = content.TryGetValue("src", out var src) && src is PsbString srcString ? srcString.Value : string.Empty,
                Icon = content.TryGetValue("icon", out var icon) ? icon.ToString() : null,
                Ox = GetFloat(content, "ox", 0),
                Oy = GetFloat(content, "oy", 0),
                CoordX = GetListFloat(coord, 0, 0),
                CoordY = GetListFloat(coord, 1, 0),
                CoordZ = GetListFloat(coord, 2, 0),
                Opacity = GetInt(content, "opa", 255),
                FlipX = GetInt(content, "fx", 0) != 0,
                FlipY = GetInt(content, "fy", 0) != 0,
                Angle = GetFloat(content, "angle", 0),
                ZoomX = GetFloat(content, "zx", 1),
                ZoomY = GetFloat(content, "zy", 1),
                SlantX = GetFloat(content, "sx", 0),
                SlantY = GetFloat(content, "sy", 0)
            };
        }

        private int[] GetTransformOrder(PsbDictionary layer)
        {
            if (layer.TryGetValue("transformOrder", out var orderValue) && orderValue is PsbList list && list.Count > 0)
            {
                return list.OfType<PsbNumber>().Select(n => n.IntValue).ToArray();
            }

            return new[] { TransformFlip, TransformSlant, TransformZoom, TransformAngle };
        }

        private bool InheritAllAffine(PsbDictionary layer)
        {
            return GetBool(layer, "inheritFlipX", true) &&
                   GetBool(layer, "inheritFlipY", true) &&
                   GetBool(layer, "inheritAngle", true) &&
                   GetBool(layer, "inheritZoomX", true) &&
                   GetBool(layer, "inheritZoomY", true) &&
                   GetBool(layer, "inheritSlantX", true) &&
                   GetBool(layer, "inheritSlantY", true);
        }

        private bool IsExportSelf(PsbDictionary layer)
        {
            return GetInt(layer, "exportSelf", 1) != 0;
        }

        private int GetFrameType(PsbDictionary frame)
        {
            return GetInt(frame, "type", TimelineFrameTypeNull);
        }

        private float GetFrameTime(PsbDictionary frame)
        {
            return GetFloat(frame, "time", 0);
        }

        private int GetInt(PsbDictionary dic, string key, int defaultValue)
        {
            return dic.TryGetValue(key, out var value) && value is PsbNumber number ? number.IntValue : defaultValue;
        }

        private bool GetBool(PsbDictionary dic, string key, bool defaultValue)
        {
            return dic.TryGetValue(key, out var value) && value is PsbNumber number ? number.IntValue != 0 : defaultValue;
        }

        private float GetFloat(PsbDictionary dic, string key, float defaultValue)
        {
            return dic.TryGetValue(key, out var value) && value is PsbNumber number ? number.AsFloat : defaultValue;
        }

        private float GetListFloat(PsbList list, int index, float defaultValue)
        {
            return list != null && list.Count > index && list[index] is PsbNumber number ? number.AsFloat : defaultValue;
        }

        private StaticMotionMatrix2 CalcAffineMatrix(int[] transformOrder, bool flipX, bool flipY, float angle, float zoomX, float zoomY, float slantX, float slantY)
        {
            var matrix = StaticMotionMatrix2.Identity;
            foreach (var transform in transformOrder)
            {
                switch (transform)
                {
                    case TransformFlip:
                        if (flipX)
                        {
                            matrix.M11 = -matrix.M11;
                            matrix.M12 = -matrix.M12;
                        }
                        if (flipY)
                        {
                            matrix.M21 = -matrix.M21;
                            matrix.M22 = -matrix.M22;
                        }
                        break;
                    case TransformZoom:
                        matrix.M11 *= zoomX;
                        matrix.M12 *= zoomX;
                        matrix.M21 *= zoomY;
                        matrix.M22 *= zoomY;
                        break;
                    case TransformAngle:
                        if (Math.Abs(angle) > 0.0001f)
                        {
                            var rad = angle * Math.PI * 2 / 360;
                            var cos = (float)Math.Cos(rad);
                            var sin = (float)Math.Sin(rad);
                            matrix = new StaticMotionMatrix2(
                                cos * matrix.M11 + -sin * matrix.M21,
                                cos * matrix.M12 + -sin * matrix.M22,
                                sin * matrix.M11 + cos * matrix.M21,
                                sin * matrix.M12 + cos * matrix.M22);
                        }
                        break;
                    case TransformSlant:
                        if (Math.Abs(slantX) > 0.0001f || Math.Abs(slantY) > 0.0001f)
                        {
                            matrix = new StaticMotionMatrix2(
                                matrix.M11 + slantX * matrix.M21,
                                matrix.M12 + slantX * matrix.M22,
                                slantY * matrix.M11 + matrix.M21,
                                slantY * matrix.M12 + matrix.M22);
                        }
                        break;
                }
            }

            return matrix;
        }

        private class StaticFrameContent
        {
            public string Source;
            public string Icon;
            public float Ox;
            public float Oy;
            public float CoordX;
            public float CoordY;
            public float CoordZ;
            public int Opacity;
            public bool FlipX;
            public bool FlipY;
            public float Angle;
            public float ZoomX;
            public float ZoomY;
            public float SlantX;
            public float SlantY;
        }

        private struct StaticRenderScore : IComparable<StaticRenderScore>
        {
            public int Count;
            public long Area;
            public int Opacity;

            public int CompareTo(StaticRenderScore other)
            {
                var countCompare = Count.CompareTo(other.Count);
                if (countCompare != 0)
                {
                    return countCompare;
                }

                var areaCompare = Area.CompareTo(other.Area);
                if (areaCompare != 0)
                {
                    return areaCompare;
                }

                return Opacity.CompareTo(other.Opacity);
            }
        }

        private class StaticRenderContext
        {
            public float X;
            public float Y;
            public float Z;
            public int Opacity = 255;
            public bool Visible = true;
            public StaticMotionMatrix2 Matrix = StaticMotionMatrix2.Identity;
        }
    }

    internal enum NestedMotionTimeMode
    {
        Initial,
        Default,
        Representative
    }

    internal struct StaticMotionMatrix2
    {
        public static readonly StaticMotionMatrix2 Identity = new StaticMotionMatrix2(1, 0, 0, 1);

        public float M11;
        public float M12;
        public float M21;
        public float M22;

        public StaticMotionMatrix2(float m11, float m12, float m21, float m22)
        {
            M11 = m11;
            M12 = m12;
            M21 = m21;
            M22 = m22;
        }

        public PointF Transform(float x, float y)
        {
            return new PointF(M11 * x + M12 * y, M21 * x + M22 * y);
        }

        public static StaticMotionMatrix2 Multiply(StaticMotionMatrix2 a, StaticMotionMatrix2 b)
        {
            return new StaticMotionMatrix2(
                a.M11 * b.M11 + a.M12 * b.M21,
                a.M11 * b.M12 + a.M12 * b.M22,
                a.M21 * b.M11 + a.M22 * b.M21,
                a.M21 * b.M12 + a.M22 * b.M22);
        }
    }

    internal class StaticDrawableResource
    {
        public ImageMetadata Metadata { get; }
        public string Label { get; }
        public string MotionName { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public StaticMotionMatrix2 Matrix { get; }
        public float OriginX { get; }
        public float OriginY { get; }
        public int Opacity { get; }
        public bool Visible { get; }

        public StaticDrawableResource(ImageMetadata metadata, string label, string motionName, float x, float y, float z, StaticMotionMatrix2 matrix, float originX, float originY, int opacity, bool visible)
        {
            Metadata = metadata;
            Label = label;
            MotionName = motionName;
            X = x;
            Y = y;
            Z = z;
            Matrix = matrix;
            // Emote draws source vertices relative to frame ox/oy plus the source origin stored in metadata.
            // Treating coord as the image center is the old bug that made limbs detach on static renders.
            OriginX = originX + metadata.OriginX;
            OriginY = originY + metadata.OriginY;
            Opacity = opacity;
            Visible = visible;
        }

        public ImageMetadata ToImageMetadata()
        {
            var clone = Metadata.Clone();
            clone.Label = Label;
            clone.MotionName = MotionName;
            clone.OriginX = X;
            clone.OriginY = Y;
            clone.ZIndex = Z;
            clone.Opacity = Opacity;
            clone.Visible = Visible;
            return clone;
        }

        public PointF[] GetCorners()
        {
            return new[]
            {
                TransformPoint(0, 0, 0, 0),
                TransformPoint(Metadata.Width, 0, 0, 0),
                TransformPoint(Metadata.Width, Metadata.Height, 0, 0),
                TransformPoint(0, Metadata.Height, 0, 0)
            };
        }

        public PointF[] GetDestPoints(float xOffset, float yOffset)
        {
            return new[]
            {
                TransformPoint(0, 0, xOffset, yOffset),
                TransformPoint(Metadata.Width, 0, xOffset, yOffset),
                TransformPoint(0, Metadata.Height, xOffset, yOffset)
            };
        }

        private PointF TransformPoint(float x, float y, float xOffset, float yOffset)
        {
            var p = Matrix.Transform(x - OriginX, y - OriginY);
            return new PointF(X + p.X + xOffset, Y + p.Y + yOffset);
        }
    }
}
