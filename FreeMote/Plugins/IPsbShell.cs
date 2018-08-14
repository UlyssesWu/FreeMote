using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FreeMote.Plugins
{
    [InheritedExport]
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
