namespace FreeMote
{
    /// <summary>
    /// Audio Arch Data
    /// </summary>
    public interface IArchData
    {
        string Extension { get; }
        PsbAudioFormat Format { get; }
        bool CanEncode { get; }
        bool CanDecode { get; }
    }
}
