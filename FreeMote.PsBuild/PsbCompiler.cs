using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using FreeMote.Psb;
using Newtonsoft.Json;

namespace FreeMote.PsBuild
{

    /// <summary>
    /// Compile PSB File
    /// </summary>
    public static class PsbCompiler
    {
        private static readonly List<string> SupportedImageExt = new List<string> { ".png", ".bmp", ".jpg", ".jpeg" };

        /// <summary>
        /// Compile to file
        /// </summary>
        /// <param name="inputPath">Json file path</param>
        /// <param name="outputPath">Output path</param>
        /// <param name="inputResPath">Special resource Json file path</param>
        /// <param name="version">PSB version</param>
        /// <param name="cryptKey">CryptKey, if you need to use it outside FreeMote</param>
        /// <param name="platform">PSB Platform</param>
        public static void CompileToFile(string inputPath, string outputPath, string inputResPath = null, ushort? version = null, uint? cryptKey = null, PsbSpec? platform = null)
        {
            if (string.IsNullOrEmpty(inputPath))
            {
                throw new FileNotFoundException("Can not find input json file.");
            }

            if (string.IsNullOrEmpty(inputResPath) || !File.Exists(inputResPath))
            {
                inputResPath = Path.ChangeExtension(inputPath, ".resx.json");
                if (!File.Exists(inputResPath))
                {
                    inputResPath = Path.ChangeExtension(inputPath, ".res.json");
                }
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

        /// <summary>
        /// Compile Json to PSB
        /// </summary>
        /// <param name="inputJson">Json text</param>
        /// <param name="inputResJson">Resource Json text</param>
        /// <param name="baseDir">If resource Json uses relative paths (usually it does), specify the base dir</param>
        /// <param name="version">PSB version</param>
        /// <param name="cryptKey">CryptKey, if you need to use it outside FreeMote</param>
        /// <param name="spec">PSB Platform</param>
        /// <returns></returns>
        public static byte[] Compile(string inputJson, string inputResJson, string baseDir = null, ushort? version = null, uint? cryptKey = null,
            PsbSpec? spec = null)
        {
            //Parse
            PSB psb = Parse(inputJson, version ?? 3);
            //Link
            if (!string.IsNullOrWhiteSpace(inputResJson))
            {
                if (inputResJson.Trim().StartsWith("{")) //resx.json
                {
                    PsbResourceJson resx = JsonConvert.DeserializeObject<PsbResourceJson>(inputResJson);
                    if (resx.PsbType != null)
                    {
                        psb.Type = resx.PsbType.Value;
                    }
                    if (resx.PsbVersion != null && version == null)
                    {
                        psb.Header.Version = resx.PsbVersion.Value;
                    }
                    if (resx.Platform != null && spec == null)
                    {
                        spec = resx.Platform;
                    }
                    if (resx.CryptKey != null & cryptKey == null)
                    {
                        cryptKey = resx.CryptKey;
                    }

                    if (resx.ExternalTextures)
                    {
                        Console.WriteLine("[INFO] External Texture mode ON, no resource will be compiled.");
                    }
                    else
                    {
                        psb.Link(resx, baseDir);
                    }
                }
                else
                {
                    List<string> resources = JsonConvert.DeserializeObject<List<string>>(inputResJson);
                    psb.Link(resources, baseDir);
                }
            }
            //Build
            psb.Merge();
            if (spec != null && spec != psb.Platform)
            {
                psb.SwitchSpec(spec.Value, spec.Value.DefaultPixelFormat());
                psb.Merge();
            }
            var bytes = psb.Build();
            //Convert
            return cryptKey != null ? PsbFile.EncodeToBytes(cryptKey.Value, bytes, EncodeMode.Encrypt, EncodePosition.Auto) : bytes;
        }

        /// <summary>
        /// Load PSB From Json file
        /// </summary>
        /// <param name="inputPath">Json file path</param>
        /// <param name="inputResPath">Resource Json file</param>
        /// <param name="version">PSB version</param>
        /// <returns></returns>
        public static PSB LoadPsbFromJsonFile(string inputPath, string inputResPath = null, ushort? version = null)
        {
            if (string.IsNullOrEmpty(inputPath))
            {
                throw new FileNotFoundException("Can not find input json file.");
            }

            if (string.IsNullOrEmpty(inputResPath) || !File.Exists(inputResPath))
            {
                inputResPath = Path.ChangeExtension(inputPath, ".resx.json");
                if (!File.Exists(inputResPath))
                {
                    inputResPath = Path.ChangeExtension(inputPath, ".res.json");
                }
            }

            string inputResJson = null;
            string baseDir = Path.GetDirectoryName(inputPath);
            if (File.Exists(inputResPath))
            {
                inputResJson = File.ReadAllText(inputResPath);
                baseDir = Path.GetDirectoryName(inputPath);
            }

            //Parse
            PSB psb = Parse(File.ReadAllText(inputPath), version ?? 3);
            //Link
            if (!string.IsNullOrWhiteSpace(inputResJson))
            {
                if (inputResJson.Trim().StartsWith("{")) //resx.json
                {
                    PsbResourceJson resx = JsonConvert.DeserializeObject<PsbResourceJson>(inputResJson);
                    if (resx.PsbType != null)
                    {
                        psb.Type = resx.PsbType.Value;
                    }
                    if (resx.PsbVersion != null && version == null)
                    {
                        psb.Header.Version = resx.PsbVersion.Value;
                    }

                    if (resx.ExternalTextures)
                    {
                        Console.WriteLine("[INFO] External Texture mode ON, no resource will be compiled.");
                    }
                    else
                    {
                        psb.Link(resx, baseDir);
                    }

                    if (resx.Platform != null)
                    {
                        psb.SwitchSpec(resx.Platform.Value, resx.Platform.Value.DefaultPixelFormat());
                    }
                }
                else
                {
                    List<string> resources = JsonConvert.DeserializeObject<List<string>>(inputResJson);
                    psb.Link(resources, baseDir);
                }
            }
            if (version != null)
            {
                psb.Header.Version = version.Value;
            }
            psb.Merge();
            return psb;
        }

        internal static PSB Parse(string json, ushort version)
        {
            PSB psb = new PSB(version)
            {
                Objects = JsonConvert.DeserializeObject<PsbDictionary>(json, new PsbJsonConverter())
            };
            psb.Type = psb.InferType();
            return psb;
        }

        internal static byte[] LoadImageBytes(string path, ResourceMetadata metadata, bool tlg6 = false)
        {
            byte[] data;
            Bitmap image = null;
            var ext = Path.GetExtension(path)?.ToLowerInvariant();

            if (metadata.Compress == PsbCompressType.ByName && ext != null && metadata.Name != null && metadata.Name.EndsWith(ext, true, null))
            {
                return File.ReadAllBytes(path);
            }

            switch (ext)
            {
                //tlg
                case ".tlg" when metadata.Compress == PsbCompressType.Tlg:
                    return File.ReadAllBytes(path);
                case ".tlg":
                    image = TlgConverter.LoadTlg(File.ReadAllBytes(path), out _);
                    break;
                //rl
                case ".rl" when metadata.Compress == PsbCompressType.RL:
                    return File.ReadAllBytes(path);
                case ".rl" when metadata.Compress == PsbCompressType.None:
                    return RL.Uncompress(File.ReadAllBytes(path));
                case ".rl":
                    image = RL.UncompressToImage(File.ReadAllBytes(path), metadata.Height, metadata.Width,
                        metadata.PixelFormat);
                    break;
                //raw
                case ".raw" when metadata.Compress == PsbCompressType.None:
                    return File.ReadAllBytes(path);
                case ".raw" when metadata.Compress == PsbCompressType.RL:
                    return RL.Compress(File.ReadAllBytes(path));
                case ".raw":
                    image = RL.ConvertToImage(File.ReadAllBytes(path), metadata.Height, metadata.Width,
                        metadata.PixelFormat);
                    break;
                //bin
                case ".bin":
                    return File.ReadAllBytes(path);
                //image
                default:
                    if (SupportedImageExt.Contains(ext))
                    {
                        image = new Bitmap(path);
                    }
                    else
                    {
                        return File.ReadAllBytes(path);
                    }
                    break;
            }

            switch (metadata.Compress)
            {
                case PsbCompressType.RL:
                    data = RL.CompressImage(image, metadata.PixelFormat);
                    break;
                case PsbCompressType.Tlg:
                    if (TlgConverter.CanSaveTlg)
                    {
                        data = image.SaveTlg(tlg6);
                    }
                    else
                    {
                        Console.WriteLine($"[WARN] Can not convert image to TLG: {path}");
                        data = File.ReadAllBytes(path);
                    }
                    break;
                case PsbCompressType.None:
                default:
                    data = RL.GetPixelBytesFromImage(image, metadata.PixelFormat);
                    break;
            }

            return data;
        }

        /// <summary>
        /// Link
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="resPaths">resource paths</param>
        /// <param name="baseDir"></param>
        internal static void Link(this PSB psb, List<string> resPaths, string baseDir = null)
        {
            var resList = psb.CollectResources();
            foreach (var resPath in resPaths)
            {
                var resName = Path.GetFileNameWithoutExtension(resPath);
                //var resMd = uint.TryParse(resName, out uint rid)
                //    ? resList.FirstOrDefault(r => r.Index == rid)
                //    : resList.FirstOrDefault(r =>
                //        resName == $"{r.Part}{PsbResCollector.ResourceNameDelimiter}{r.Name}");

                //Scan for Resource
                var resMd = resList.FirstOrDefault(r =>
                    resName == $"{r.Part}{PsbResCollector.ResourceNameDelimiter}{r.Name}");
                if (resMd == null && uint.TryParse(resName, out uint rid))
                {
                    resMd = resList.FirstOrDefault(r => r.Index == rid);
                }
                if (resMd == null && psb.Type == PsbType.Images)
                {
                    resMd = resList.FirstOrDefault(r => resName == Path.GetFileNameWithoutExtension(r.Name));
                }
                if (resMd == null)
                {
                    Console.WriteLine($"[WARN]{resPath} is not used.");
                    continue;
                }
                var fullPath = Path.Combine(baseDir ?? "", resPath.Replace('/', '\\'));
                byte[] data = LoadImageBytes(fullPath, resMd);
                resMd.Resource.Data = data;
            }
        }

        /// <summary>
        /// Link
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="resx">advanced resource json(resx.jon)</param>
        /// <param name="baseDir"></param>
        internal static void Link(this PSB psb, PsbResourceJson resx, string baseDir)
        {
            var resList = psb.CollectResources();
            foreach (var resxResource in resx.Resources)
            {
                //Scan for Resource
                var resMd = resList.FirstOrDefault(r =>
                    resxResource.Key == $"{r.Part}{PsbResCollector.ResourceNameDelimiter}{r.Name}");
                if (resMd == null && psb.Type == PsbType.Images)
                {
                    resMd = resList.FirstOrDefault(r => resxResource.Key == Path.GetFileNameWithoutExtension(r.Name));
                }
                if (resMd == null && uint.TryParse(resxResource.Key, out uint rid))
                {
                    resMd = resList.FirstOrDefault(r => r.Index == rid);
                }

                if (resMd == null)
                {
                    Console.WriteLine($"[WARN]{resxResource.Key} is not used.");
                    continue;
                }

                var fullPath = Path.IsPathRooted(resxResource.Value)
                    ? resxResource.Value
                    : Path.Combine(baseDir ?? "", resxResource.Value.Replace('/', '\\'));
                byte[] data = LoadImageBytes(fullPath, resMd, resx.TlgVersion >= 6);
                resMd.Resource.Data = data;
            }
        }
    }
}
