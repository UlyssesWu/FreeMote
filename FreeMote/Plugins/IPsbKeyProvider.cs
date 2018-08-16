using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FreeMote.Plugins
{
    /// <summary>
    /// Get PSB CryptKey
    /// </summary>
    interface IPsbKeyProvider : IPsbPlugin
    {
        uint? GetKey(Stream stream, Dictionary<string, object> context = null);
    }
}
