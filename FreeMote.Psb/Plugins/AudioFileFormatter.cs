using System.Collections.Generic;
using System.ComponentModel.Composition;
using FreeMote.Psb;

// ReSharper disable once CheckNamespace
namespace FreeMote.Plugins
{
    [Export(typeof(IPsbAudioFormatter))]
    [ExportMetadata("Name", "FreeMote.Audio.File")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "Export/Import audio file.")]
    internal class AudioFileFormatter : IPsbAudioFormatter
    {
        public List<string> Extensions { get; } = new() { ".wav", ".ogg" };
        public bool CanToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            return archData is AudioFileArchData;
        }

        public bool CanToArchData(byte[] wave, Dictionary<string, object> context = null)
        {
            if (wave == null || wave.Length < 4)
            {
                return false;
            }

            if (wave[0] == 'O' && wave[1] == 'g' && wave[2] == 'g') return true;
            if (wave[0] == 'R' && wave[1] == 'I' && wave[2] == 'F' && wave[3] == 'F') return true;

            return false;
        }

        public byte[] ToWave(AudioMetadata md, IArchData archData, string fileName = null, Dictionary<string, object> context = null)
        {
            var arch = archData as AudioFileArchData;
            return arch?.Data?.Data;
        }

        public bool ToArchData(AudioMetadata md, IArchData archData, in byte[] wave, string fileName, string waveExt, Dictionary<string, object> context = null)
        {
            if (archData is not AudioFileArchData fileArchData)
            {
                return false;
            }

            if (wave[0] == 'O' && wave[1] == 'g' && wave[2] == 'g')
            {
                fileArchData.Format = PsbAudioFormat.OGG;
            }
            else //if (wave[0] == 'R' && wave[1] == 'I' && wave[2] == 'F' && wave[3] == 'F')
            {
                fileArchData.Format = PsbAudioFormat.WAV;
            }

            if (fileArchData.Data == null)
            {
                fileArchData.Data = new PsbResource { Data = wave };
            }
            else
            {
                fileArchData.Data.Data = wave;
            }
            return true;
        }

        public bool TryGetArchData(AudioMetadata md, PsbDictionary channel, out IArchData data, Dictionary<string, object> context = null)
        {
            data = null;
            if (channel.Count == 1 && channel["archData"] is PsbDictionary archDic && archDic.TryGetPsbValue<PsbBool>("background", out var background) && archDic.TryGetPsbValue<PsbString>("filetype", out var fileType) && archDic.TryGetPsbValue<PsbString>("ext", out var ext) && archDic.TryGetPsbValue<PsbResource>("data", out var aData))
            {
                var newData = new AudioFileArchData
                {
                    Data = aData,
                    Background = background,
                    Format = ext == ".ogg" ? PsbAudioFormat.OGG : PsbAudioFormat.WAV
                };

                if (archDic["loop"] is PsbList aLoop)
                {
                    newData.Loop = aLoop;
                }

                data = newData;
                return true;
            }

            return false;
        }
    }
}
