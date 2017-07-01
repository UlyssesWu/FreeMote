using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreeMote;
using FreeMote.Psb;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
// ReSharper disable AssignNullToNotNullAttribute

namespace FreeMote.PsBuild
{
    /// <summary>
    /// Decompile PSB(/MMO) File
    /// </summary>
    public static class PsbDecompiler
    {
        /// <summary>
        /// Decompile Pure PSB as Json
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string Decompile(string path)
        {
            PSB psb = new PSB(path);
            return Decompile(psb);
        }

        /// <summary>
        /// Decompile Pure PSB as Json
        /// </summary>
        /// <param name="path"></param>
        /// <param name="resources">resources bytes</param>
        /// <returns></returns>

        public static string Decompile(string path, out List<byte[]> resources)
        {
            PSB psb = new PSB(path);
            resources = new List<byte[]>(psb.Resources.Select(r => r.Data));
            return Decompile(psb);
        }

        internal static string Decompile(PSB psb)
        {
            return JsonConvert.SerializeObject(psb.Objects, Formatting.Indented, new PsbTypeConverter());
        }

        /// <summary>
        /// Decompile to files
        /// </summary>
        /// <param name="inputPath"></param>
        public static void DecompileToFile(string inputPath)
        {
            var name = Path.GetFileNameWithoutExtension(inputPath);
            var dirPath = Path.Combine(Path.GetDirectoryName(inputPath), name);
            File.WriteAllText(inputPath + ".json", Decompile(inputPath, out List<byte[]> resources));
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }
            var resPaths = new List<string>(resources.Count);
            for (int i = 0; i < resources.Count; i++)
            {
                var relativePath = $"{name}/{i}.bin";
                resPaths.Add(relativePath);
                File.WriteAllBytes(Path.Combine(dirPath, $"{i}.bin"), resources[i]);
            }
            //MARK: We use `.resx.json` to distinguish from psbtools' `.res.json`
            File.WriteAllText(inputPath + ".resx.json", JsonConvert.SerializeObject(resPaths, Formatting.Indented));
        }
    }
}
