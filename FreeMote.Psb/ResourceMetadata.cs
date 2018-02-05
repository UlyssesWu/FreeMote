using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;

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
        public string Type => TypeString?.Value;
        public PsbString TypeString { get; set; }
        public RectangleF Clip { get; set; }
        public PsbResource Resource { get; set; }
        public byte[] Data => Resource?.Data;
        /// <summary>
        /// Additional z-index info
        /// </summary>
        public float ZIndex { get; set; }
        /// <summary>
        /// Additional Name
        /// </summary>
        public string Description { get; set; }

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

        public override string ToString()
        {
            return $"{Part}/{Name}";
        }
    }
}
