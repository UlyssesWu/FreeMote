namespace FreeMote.Psb
{
    /// <summary>
    /// Audio Arch Data
    /// </summary>
    public interface IArchData
    {
        uint Index { get; }
        string Extension { get; }
        string WaveExtension { get; set; }
        PsbAudioFormat Format { get; }
        bool CanEncode { get; }
        bool CanDecode { get; }
    }
}
