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
        public static void CompileToFile(string inputPath, string outputPath, ushort version = 3, uint? cryptKey = null, string platform = null)
        {
            PSB psb = Parse(inputPath, version);
        }


        internal static PSB Parse(string inputPath, ushort version)
        {
            PSB psb = new PSB(version)
            {
                Objects = JsonConvert.DeserializeObject<PsbDictionary>(File.ReadAllText(inputPath), new PsbTypeConverter())
            };
            return psb;
        }

        internal void Link(PSB psb, string resInputPath)
        {
            List<PsbResource> resources = new List<PsbResource>();
        }
    }
}
