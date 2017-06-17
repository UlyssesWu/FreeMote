using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreeMote.Psb;
using Newtonsoft.Json;

namespace FreeMote.PsBuild
{
    /// <summary>
    /// Compile PSB File
    /// </summary>
    public class PsbCompiler
    {
        /// <summary>
        /// Compile to file
        /// </summary>
        /// <param name="inputPath"></param>
        /// <param name="outputPath"></param>
        public static void CompileToFile(string inputPath, string outputPath, ushort version = 3)
        {
            PSB psb = new PSB(version);
            psb.Objects = JsonConvert.DeserializeObject<PsbDictionary>(File.ReadAllText(inputPath));
        }

        internal static void Parse()
        { }

        internal void Link()
        { }
    }
}
