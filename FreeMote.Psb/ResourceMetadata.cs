using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using FreeMote.Plugins;

namespace FreeMote.Psb
{
    /// <summary>
    /// Compression in PSB
    /// </summary>
    public enum PsbCompressType
    {
        /// <summary>
        /// Normal
        /// </summary>
        None,

        /// <summary>
        /// RLE
        /// </summary>
        RL,

        /// <summary>
        /// Raw Bitmap
        /// </summary>
        Bmp,

        /// <summary>
        /// KRKR TLG
        /// </summary>
        Tlg,

        /// <summary>
        /// By extension
        /// </summary>
        ByName,
    }

    /// <summary>
    /// Information for Resources
    /// </summary>
    [DebuggerDisplay("{" + nameof(DebuggerString) + "}")]
    public class ResourceMetadata
    {
        /// <summary>
        /// Name 1
        /// </summary>
        public string Part { get; set; }

        /// <summary>
        /// Name 2
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Index is a value for tracking resource when compiling. For index appeared in texture name, see <seealso cref="TextureIndex"/>
        /// </summary>
        public uint Index
        {
            get => Resource.Index ?? UInt32.MaxValue;
            set
            {
                if (Resource != null)
                {
                    Resource.Index = value;
                }
            }
        }

        /// <summary>
        /// The texture index. e.g. "tex#001".TextureIndex = 1, "tex".Index = 0
        /// </summary>
        public uint? TextureIndex => GetTextureIndex(Part);

        /// <summary>
        /// The texture index. e.g.
        /// <code>GetTextureIndex("tex#001") = 1</code>
        /// </summary>
        internal static uint? GetTextureIndex(string texName)
        {
            if (texName.EndsWith("tex") || texName.EndsWith("tex#000") || texName.EndsWith("tex000"))
            {
                return 0;
            }

            var texIdx = texName.LastIndexOf("tex", StringComparison.Ordinal);
            if (texIdx < 0)
            {
                return null;
            }

            var isValid = uint.TryParse(
                new string(texName.Skip(texIdx).SkipWhile(c => c < 48 || c > 57).TakeWhile(c => c > 48 || c < 57)
                    .ToArray()), out var index);
            if (!isValid)
            {
                return null;
            }

            return index;
        }

        public PsbCompressType Compress { get; set; }
        public bool Is2D { get; set; } = true;
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>
        /// [Type2]
        /// </summary>
        public int Top { get; set; }

        /// <summary>
        /// [Type2]
        /// </summary>
        public int Left { get; set; }

        public float OriginX { get; set; }
        public float OriginY { get; set; }

        /// <summary>
        /// Pixel Format Type
        /// </summary>
        public string Type => TypeString?.Value;

        public PsbString TypeString { get; set; }
        public RectangleF Clip { get; set; }
        public PsbResource Resource { get; set; }

        public byte[] Data
        {
            get => Resource?.Data;

            internal set
            {
                if (Resource == null)
                {
                    throw new NullReferenceException("Resource is null");
                }

                Resource.Data = value;
            }
        }

        /// <summary>
        /// Additional z-index info
        /// </summary>
        public float ZIndex { get; set; }

        /// <summary>
        /// The Label which this resource belongs to
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Name under object/{part}/motion/
        /// </summary>
        public string MotionName { get; set; }

        public int Opacity { get; set; } = 10;
        public bool Visible { get; set; } = true;

        /// <summary>
        /// Platform
        /// <para>Spec can not be get from source part, so set it before use</para>
        /// </summary>
        public PsbSpec Spec { get; set; } = PsbSpec.other;

        public PsbPixelFormat PixelFormat => Type.ToPsbPixelFormat(Spec);

        private string DebuggerString =>
            $"{(string.IsNullOrWhiteSpace(Part) ? "" : Part + "/")}{Name}({Width}*{Height}){(Compress == PsbCompressType.RL ? "[RL]" : "")}";

        /// <summary>
        /// Convert Resource to Image
        /// <para>Only works if <see cref="Resource"/>.Data is not null</para>
        /// </summary>
        /// <returns></returns>
        public Bitmap ToImage()
        {
            if (Resource.Data == null)
            {
                throw new Exception("Resource data is null");
            }

            switch (Compress)
            {
                case PsbCompressType.RL:
                    return RL.UncompressToImage(Resource.Data, Height, Width, PixelFormat);
                case PsbCompressType.Tlg:
                    using (var ms = new MemoryStream(Resource.Data))
                    {
                        return new TlgImageConverter().Read(new BinaryReader(ms));
                    }
                default:
                    return RL.ConvertToImage(Resource.Data, Height, Width, PixelFormat);
            }
        }

        /// <summary>
        /// Set Image to <see cref="PsbResource.Data"/>
        /// </summary>
        /// <param name="bmp"></param>
        public void SetData(Bitmap bmp)
        {
            switch (Compress)
            {
                case PsbCompressType.RL:
                    Data = RL.CompressImage(bmp, PixelFormat);
                    break;
                case PsbCompressType.Tlg:
                    Data = FreeMount.CreateContext().BitmapToResource(".tlg", bmp);
                    break;
                default:
                    Data = RL.GetPixelBytesFromImage(bmp, PixelFormat);
                    break;
            }
        }

        public override string ToString()
        {
            return $"{Part}/{Name}";
        }

        /// <summary>
        /// Name for export & import
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public string GetFriendlyName(PsbType type)
        {
            if (type == PsbType.Pimg && !string.IsNullOrWhiteSpace(Name))
            {
                return Path.GetFileNameWithoutExtension(Name);
            }

            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Part))
            {
                return Index.ToString();
            }

            return $"{Part}{PsbResCollector.ResourceNameDelimiter}{Name}";
        }
    }
}