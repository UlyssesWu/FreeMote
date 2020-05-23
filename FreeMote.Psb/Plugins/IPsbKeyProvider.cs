using System.Collections.Generic;
using System.IO;

namespace FreeMote.Plugins
{
    /// <summary>
    /// Get PSB CryptKey
    /// </summary>
    public interface IPsbKeyProvider : IPsbPlugin
    {
        /// <summary>
        /// Try to get PSB key, do not lift the stream position
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="context"></param>
        /// <returns>null if no key detected; otherwise give the key</returns>
        uint? GetKey(Stream stream, Dictionary<string, object> context = null);
    }
}
