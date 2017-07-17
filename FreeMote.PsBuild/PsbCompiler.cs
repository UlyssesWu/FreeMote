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
    public static class PsbCompiler
    {
        /// <summary>
        /// Compile to file
        /// </summary>
        /// <param name="inputPath"></param>
        /// <param name="outputPath"></param>
        public static void CompileToFile(string inputPath, string outputPath, string inputResPath = null, ushort version = 3, uint? cryptKey = null, PsbSpec platform = PsbSpec.common)
        {
            if (string.IsNullOrEmpty(inputResPath) || !File.Exists(inputResPath))
            {
                inputResPath = Path.ChangeExtension(inputPath, ".resx.json");
                if (!File.Exists(inputResPath))
                {
                    inputResPath = Path.ChangeExtension(inputPath, ".res.json");
                }
            }

            if (string.IsNullOrEmpty(inputPath))
            {
                throw new FileNotFoundException("Can not find input json file.");
            }

            string resJson = null;
            string baseDir = Path.GetDirectoryName(inputPath);
            if (File.Exists(inputResPath))
            {
                resJson = File.ReadAllText(inputResPath);
                baseDir = Path.GetDirectoryName(inputPath);
            }

            var result = Compile(File.ReadAllText(inputPath), resJson, baseDir, version, cryptKey, platform);

            File.WriteAllBytes(outputPath, result);
        }

        public static byte[] Compile(string inputJson, string inputResJson, string baseDir = null, ushort version = 3, uint? cryptKey = null,
            PsbSpec spec = PsbSpec.common)
        {
            //Parse
            PSB psb = Parse(inputJson, version);
            psb.SwitchSpec(spec);
            //Link
            if (!string.IsNullOrWhiteSpace(inputResJson))
            {
                psb.Link(inputResJson, baseDir);
            }
            //Build
            psb.Merge();
            var bytes = psb.Build();
            //Convert
            return cryptKey != null ? PsbFile.EncodeToBytes(cryptKey.Value, bytes, EncodeMode.Encrypt, EncodePosition.Auto) : bytes;
        }

        internal static PSB Parse(string json, ushort version)
        {
            PSB psb = new PSB(version)
            {
                Objects = JsonConvert.DeserializeObject<PsbDictionary>(json, new PsbTypeConverter())
            };
            return psb;
        }

        internal static void Link(this PSB psb, string resJson, string baseDir = null)
        {
            List<string> resPaths = JsonConvert.DeserializeObject<List<string>>(resJson);
            var resList = psb.CollectResources();
            foreach (var resPath in resPaths)
            {
                if (!uint.TryParse(Path.GetFileNameWithoutExtension(resPath), out uint rid))
                {
                    throw new InvalidCastException($"Filename can not be parsed as Resource ID: {resPath}");
                }
                var resMd = resList.FirstOrDefault(r => r.Index == rid);
                if (resMd == null)
                {
                    Console.WriteLine($"[WARN]{resPath} is not used.");
                    continue;
                }
                var fullPath = Path.Combine(baseDir ?? "", resPath.Replace('/','\\'));
                byte[] data = null;
                switch (Path.GetExtension(resPath)?.ToLowerInvariant())
                {
                    case ".png":
                    case ".bmp":
                        data = psb.Platform.CompressType() == PsbCompressType.RL ? RL.CompressImageFile(fullPath) : RL.GetPixelBytesFromImageFile(fullPath);
                        break;
                    case ".rl":
                        data = psb.Platform.CompressType() == PsbCompressType.RL ? File.ReadAllBytes(fullPath) : RL.Uncompress(File.ReadAllBytes(fullPath));
                        break;
                    case ".raw":
                        data = psb.Platform.CompressType() == PsbCompressType.RL ? RL.Compress(File.ReadAllBytes(fullPath)) : File.ReadAllBytes(fullPath);
                        break;
                    default: //For `.bin`, you have to manage by yourself
                        data = File.ReadAllBytes(resPath);
                        break;
                }
                resMd.Resource.Data = data;
            }
        }
    }
}
