using System;
using System.Drawing;
using FreeMote.Psb;


namespace FreeMote.PsBuild
{
    public enum PsbCompressType
    {
        /// <summary>
        /// Normal
        /// </summary>
        None,
        /// <summary>
        /// RL
        /// </summary>
        RL,
        /// <summary>
        /// Raw Bitmap
        /// </summary>
        Bmp,
    }

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
        public string Type { get; set; }
        public RectangleF Clip { get; set; }
        public PsbResource Resource { get; set; }
        public byte[] Data => Resource?.Data;
        public PsbSpec Spec { get; set; }

        public PsbPixelFormat PixelFormat
        {
            get
            {
                if (string.IsNullOrEmpty(Type))
                {
                    return PsbPixelFormat.None;
                }
                switch (Type.ToUpperInvariant())
                {
                    case "DXT5":
                        return PsbPixelFormat.DXT5;
                    case "RGBA8":
                        return Spec == PsbSpec.common ? PsbPixelFormat.CommonRGBA8 : PsbPixelFormat.WinRGBA8;
                        default:
                        return PsbPixelFormat.None;
                }
            }
        }

        public override string ToString()
        {
            return $"{Part}_{Name}({Width}*{Height}){(Compress == PsbCompressType.RL ? "[RL]" : "")}";
        }
    }
}
