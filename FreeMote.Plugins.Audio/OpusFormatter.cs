using System;
using System.Collections.Generic;
using FreeMote.Psb;

namespace FreeMote.Plugins.Audio
{
    class OpusFormatter : IPsbAudioFormatter
    {
        public List<string> Extensions { get; } = new List<string>{ ".opus" };
        public bool CanToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            throw new NotImplementedException();
        }

        public bool CanToArchData(byte[] wave, Dictionary<string, object> context = null)
        {
            throw new NotImplementedException();
        }

        public byte[] ToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            throw new NotImplementedException();
        }

        public IArchData ToArchData(byte[] wave, string waveExt, Dictionary<string, object> context = null)
        {
            throw new NotImplementedException();
        }

        public bool TryGetArchData(PSB psb, PsbDictionary dic, out IArchData data)
        {
            throw new NotImplementedException();
        }
    }
}
