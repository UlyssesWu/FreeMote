using System.Collections.Generic;
using System.IO;

namespace FreeMote.Plugins
{
    public interface IPsbShell : IPsbPlugin
    {
        string Name { get; }
        bool IsInShell(Stream stream, Dictionary<string, object> context = null);
        MemoryStream ToPsb(Stream stream, Dictionary<string, object> context = null);
        MemoryStream ToShell(Stream stream, Dictionary<string, object> context = null);
        /// <summary>
        /// If no signature, <see cref="IPsbShell"/> should set it to null, use <see cref="IsInShell"/> instead
        /// </summary>
        byte[] Signature { get; }
    }
}
