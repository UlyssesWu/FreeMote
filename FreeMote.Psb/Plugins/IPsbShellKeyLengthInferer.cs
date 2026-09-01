using System.Collections.Generic;
using System.IO;

namespace FreeMote.Plugins
{
    /// <summary>
    /// Optional capability for shell plugins that can infer a repeating MPack key length.
    /// </summary>
    public interface IPsbShellKeyLengthInferer
    {
        MemoryStream ToPsbWithInferredKeyLength(Stream stream, string key, out int keyLength,
            Dictionary<string, object> context = null);
    }
}
