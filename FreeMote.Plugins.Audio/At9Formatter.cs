using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VGAudio.Formats.Atrac9;

namespace FreeMote.Plugins.Audio
{
    [Export(typeof(IPsbAudioFormatter))]
    [ExportMetadata("Name", "FreeMote.At9")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "At9 support via VGAudio.")]
    public class At9Formatter : IPsbAudioFormatter
    {
        public List<string> Extensions => throw new NotImplementedException();

        public bool CanToBytes(byte[] wave, Dictionary<string, object> context = null)
        {
            throw new NotImplementedException();
        }

        public bool CanToWave(in byte[] data, Dictionary<string, object> context = null)
        {
            throw new NotImplementedException();
        }

        public byte[] ToBytes(byte[] bitmap, Dictionary<string, object> context = null)
        {
            throw new NotImplementedException();
        }

        public byte[] ToWave(in byte[] data, Dictionary<string, object> context = null)
        {
            throw new NotImplementedException();
        }
    }
}
