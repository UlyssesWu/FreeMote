using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FreeMote.Plugins
{
    public interface IPsbShell : IPsbPlugin
    {
        string Name { get; }
        bool IsInShell(Stream stream, Dictionary<string, object> context = null);
        Stream ToPsb(Stream stream, Dictionary<string, object> context = null);
        Stream ToShell(Stream stream, Dictionary<string, object> context = null);
    }
}
