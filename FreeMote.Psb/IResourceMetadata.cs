namespace FreeMote.Psb
{
    interface IResourceMetadata
    {
        PsbSpec Spec { get; set; }
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
        /// By extension
        /// </summary>
        ByName,
    }
}
