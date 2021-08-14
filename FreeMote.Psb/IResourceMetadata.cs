using FreeMote.Plugins;

namespace FreeMote.Psb
{
    public interface IResourceMetadata
    {
        string Name { get; set; }
        public uint Index { get; }
        PsbSpec Spec { get; set; }
        public PsbType PsbType { get; set; }
        void Link(string fullPath, FreeMountContext context);
    }

    /// <summary>
    /// Texture Link Order
    /// </summary>
    public enum PsbLinkOrderBy
    {
        /// <summary>
        /// The image name should be FreeMote style: {part}-{name}.{ext}
        /// </summary>
        Convention = 0,

        /// <summary>
        /// The image name should be EMT Editor style: {name}_tex#{no:D3}.{ext}
        /// </summary>
        Name = 1,

        /// <summary>
        /// The order in list matters
        /// </summary>
        Order = 2,
    }

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
        /// ASTC
        /// </summary>
        Astc,

        /// <summary>
        /// By extension
        /// </summary>
        ByName,
    }
}
