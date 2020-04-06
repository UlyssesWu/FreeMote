using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using FreeMote.Plugins;
using FreeMote.Psb;
using Newtonsoft.Json;

namespace FreeMote.PsBuild
{
    /// <summary>
    /// Texture Link Order
    /// </summary>
    public enum PsbLinkOrderBy
    {
        /// <summary>
        /// The image name should be FreeMote style: {part}-{name}.{ext}
        /// </summary>
        Convention = 0,

        /// <summary>
        /// The image name should be EMT Editor style: {name}_tex#{no:D3}.{ext}
        /// </summary>
        Name = 1,

        /// <summary>
        /// The order in list matters
        /// </summary>
        Order = 2,
    }

    /// <summary>
    /// Compile PSB File
    /// </summary>
    public static class PsbCompiler
    {
        private static readonly List<string> SupportedImageExt = new List<string> {".png", ".bmp", ".jpg", ".jpeg"};

        /// <summary>
        /// Compile to file
        /// </summary>
        /// <param name="inputPath">Json file path</param>
        /// <param name="outputPath">Output path</param>
        /// <param name="inputResPath">Special resource Json file path</param>
        /// <param name="version">PSB version</param>
        /// <param name="cryptKey">CryptKey, if you need to use it outside FreeMote</param>
        /// <param name="platform">PSB Platform</param>
        /// <param name="renameOutput">If true, the output file extension is renamed by type</param>
        /// <param name="keepShell">If true, the output can be compressed PSB shell type (if specified)</param>
        public static void CompileToFile(string inputPath, string outputPath, string inputResPath = null,
            ushort? version = null, uint? cryptKey = null, PsbSpec? platform = null, bool renameOutput = true,
            bool keepShell = true)
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
                baseDir = Path.GetDirectoryName(inputResPath);
                if (renameOutput) //start renaming
                {
                    if (resJson.Trim().StartsWith("{"))
                    {
                        PsbResourceJson resx = JsonConvert.DeserializeObject<PsbResourceJson>(resJson);
                        bool pure = cryptKey == null && resx.CryptKey == null;
                        string ext = pure ? ".pure" : ".impure";
                        ext += resx.PsbType.HasValue ? resx.PsbType.Value.DefaultExtension() : ".psb";

                        if (resx.Context != null && resx.Context.ContainsKey(Consts.Context_PsbShellType) && keepShell)
                        {
                            var shellType = resx.Context[Consts.Context_PsbShellType] as string;
                            if (!string.IsNullOrEmpty(shellType) && shellType.ToUpperInvariant() != "PSB")
                            {
                                ext += $".{shellType.ToLowerInvariant()}";
                            }
                        }

                        var newPath = Path.ChangeExtension(outputPath, ext);
                        if (!string.IsNullOrWhiteSpace(newPath))
                        {
                            outputPath = newPath;
                        }
                    }
                }
            }

            var result = Compile(File.ReadAllText(inputPath), resJson, baseDir, version, cryptKey, platform, keepShell);

            // ReSharper disable once AssignNullToNotNullAttribute
            File.WriteAllBytes(outputPath, result);
        }

        /// <summary>
        /// Compile Json to PSB
        /// </summary>
        /// <param name="inputJson">Json text</param>
        /// <param name="inputResJson">Resource Json text</param>
        /// <param name="baseDir">If resource Json uses relative paths (usually it does), specify the base dir</param>
        /// <param name="version">PSB version</param>
        /// <param name="cryptKey">CryptKey, use null for pure PSB</param>
        /// <param name="spec">PSB Platform</param>
        /// <param name="keepShell">If true, try to compress PSB to shell type (MDF/LZ4 etc.) specified in resx.json; otherwise just output PSB</param>
        /// <returns></returns>
        public static byte[] Compile(string inputJson, string inputResJson, string baseDir = null,
            ushort? version = null, uint? cryptKey = null,
            PsbSpec? spec = null, bool keepShell = true)
        {
            var context = FreeMount.CreateContext();
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

                    context = FreeMount.CreateContext(resx.Context);

                    if (resx.ExternalTextures)
                    {
#if DEBUG
                        Console.WriteLine("[INFO] External Texture mode ON, no resource will be compiled.");
#endif
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
            if (cryptKey != null)
            {
                return PsbFile.EncodeToBytes(cryptKey.Value, bytes, EncodeMode.Encrypt, EncodePosition.Auto);
            }

            if (context.HasShell && keepShell)
            {
                return context.PackToShell(new MemoryStream(bytes)).ToArray();
            }

            return bytes;
        }

        /// <summary>
        /// Load PSB and Context From Json file, use <see cref="LoadPsbFromJsonFile"/> if you don't need context
        /// </summary>
        /// <param name="inputPath">Json file path</param>
        /// <param name="inputResPath">Resource Json file</param>
        /// <param name="version">PSB version</param>
        /// <returns></returns>
        public static (PSB Psb, Dictionary<string, object> Context) LoadPsbAndContextFromJsonFile(string inputPath,
            string inputResPath = null,
            ushort? version = null)
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
            Dictionary<string, object> context = null;
            if (!string.IsNullOrWhiteSpace(inputResJson))
            {
                if (inputResJson.Trim().StartsWith("{")) //resx.json
                {
                    PsbResourceJson resx = JsonConvert.DeserializeObject<PsbResourceJson>(inputResJson);
                    context = resx.Context;
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
#if DEBUG
                        Console.WriteLine("[INFO] External Texture mode ON, no resource will be compiled.");
#endif
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
            return (psb, context);
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
            return LoadPsbAndContextFromJsonFile(inputPath, inputResPath, version).Psb;
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

        internal static byte[] LoadImageBytes(string path, ResourceMetadata metadata, FreeMountContext context,
            out byte[] palette)
        {
            palette = null;
            bool usePalette = metadata.PixelFormat.UsePalette();
            byte[] data;
            Bitmap image = null;
            var ext = Path.GetExtension(path)?.ToLowerInvariant();

            if (metadata.Compress == PsbCompressType.ByName && ext != null && metadata.Name != null &&
                metadata.Name.EndsWith(ext, true, null))
            {
                return File.ReadAllBytes(path);
            }

            switch (ext)
            {
                //tlg
                case ".tlg" when metadata.Compress == PsbCompressType.Tlg:
                    return File.ReadAllBytes(path);
                case ".tlg":
                    image = context.ResourceToBitmap(".tlg", File.ReadAllBytes(path));
                    break;
                //rl
                case ".rl" when metadata.Compress == PsbCompressType.RL:
                    return File.ReadAllBytes(path);
                case ".rl" when metadata.Compress == PsbCompressType.None:
                    return RL.Decompress(File.ReadAllBytes(path));
                case ".rl":
                    image = RL.DecompressToImage(File.ReadAllBytes(path), metadata.Height, metadata.Width,
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
                        if (metadata.PixelFormat.UsePalette())
                        {
                            image = BitmapHelper.LoadBitmap(File.ReadAllBytes(path));
                            palette = image.Palette.GetPaletteBytes(metadata.PalettePixelFormat);
                        }
                        else
                        {
                            image = new Bitmap(path);
                        }
                    }
                    else if (context.SupportImageExt(ext))
                    {
                        image = context.ResourceToBitmap(ext, File.ReadAllBytes(path));
                    }
                    else
                    {
                        //MARK: No longer try to read files we don't know
                        //return File.ReadAllBytes(path);
                        return null;
                    }

                    break;
            }

            switch (metadata.Compress)
            {
                case PsbCompressType.RL:
                    data = RL.CompressImage(image, metadata.PixelFormat);
                    break;
                case PsbCompressType.Tlg:
                    data = context.BitmapToResource(".tlg", image);
                    if (data == null)
                    {
                        var tlgPath = Path.ChangeExtension(path, ".tlg");
                        if (File.Exists(tlgPath))
                        {
                            Console.WriteLine($"[WARN] Can not encode TLG, using {tlgPath}");
                            data = File.ReadAllBytes(tlgPath);
                        }
                        else
                        {
                            Console.WriteLine($"[WARN] Can not convert image to TLG: {path}");
                            data = File.ReadAllBytes(path);
                        }
                    }

                    break;
                case PsbCompressType.ByName:
                    var imgExt = Path.GetExtension(metadata.Name);
                    if (context.SupportImageExt(imgExt))
                    {
                        data = context.BitmapToResource(imgExt, image);
                    }
                    else
                    {
                        Console.WriteLine($"[WARN] Unsupported image: {path}");
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
        /// Link Textures
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="resPaths">resource paths</param>
        /// <param name="baseDir"></param>
        /// <param name="order">how to arrange images</param>
        /// <param name="isExternal">Whether this is an external texture PSB</param>
        public static void Link(this PSB psb, IList<string> resPaths, string baseDir = null,
            PsbLinkOrderBy order = PsbLinkOrderBy.Convention, bool isExternal = false)
        {
            if (isExternal)
            {
                psb.MotionResourceInstrument();
            }

            var resList = psb.CollectResources(duplicatePalette: Consts.GeneratePalette);
            var context = FreeMount.CreateContext();
            if (order == PsbLinkOrderBy.Order)
            {
                for (int i = 0; i < resList.Count; i++)
                {
                    var resMd = resList[i];
                    var fullPath = Path.Combine(baseDir ?? "", resPaths[i]);
                    byte[] data = LoadImageBytes(fullPath, resMd, context, out var palette);
                    resMd.Data = data;
                    resMd.PalData = palette;
                }

                return;
            }

            if (order == PsbLinkOrderBy.Name)
            {
                if (psb.Platform == PsbSpec.krkr)
                {
                    throw new InvalidOperationException(
                        $"Can not link by file name for krkr PSB. Please consider using {PsbLinkOrderBy.Convention}");
                }

                resList.Sort((md1, md2) => (int) (md1.TextureIndex ?? 0) - (int) (md2.TextureIndex ?? 0));
            }

            for (var i = 0; i < resPaths.Count; i++)
            {
                var resPath = resPaths[i];
                var resName = Path.GetFileNameWithoutExtension(resPath);
                //var resMd = uint.TryParse(resName, out uint rid)
                //    ? resList.FirstOrDefault(r => r.Index == rid)
                //    : resList.FirstOrDefault(r =>
                //        resName == $"{r.Part}{PsbResCollector.ResourceNameDelimiter}{r.Name}");

                //Scan for Resource
                ResourceMetadata resMd = null;
                if (order == PsbLinkOrderBy.Name)
                {
                    if (resName == null)
                    {
                        continue;
                    }

                    if (resList.Count == 1 && resPaths.Count == 1)
                    {
                        //If there is only one resource and one texture, we won't care about file name.
                        resMd = resList[0];
                    }
                    else
                    {
                        var texIdx = ResourceMetadata.GetTextureIndex(resName);

                        if (texIdx == null)
                        {
                            Console.WriteLine($"[WARN]{resPath} is not used since the file name cannot be recognized.");
                            continue;
                        }

                        if (resList.Count <= texIdx.Value)
                        {
                            Console.WriteLine($"[WARN]{resPath} is not used since the tex No. is too large.");
                            continue;
                        }

                        resMd = resList[(int) texIdx.Value];
                    }
                }
                else //if (order == PsbLinkOrderBy.Convention)
                {
                    resMd = resList.FirstOrDefault(r =>
                        resName == $"{r.Part}{PsbResCollector.ResourceNameDelimiter}{r.Name}");
                    if (resMd == null && uint.TryParse(resName, out uint rid))
                    {
                        //This Link has no support for raw palette
                        resMd = resList.FirstOrDefault(r => r.Index == rid);
                    }

                    if (resMd == null && psb.Type == PsbType.Pimg)
                    {
                        resMd = resList.FirstOrDefault(r => resName == Path.GetFileNameWithoutExtension(r.Name));
                    }
                }


                if (resMd == null)
                {
                    Console.WriteLine($"[WARN]{resPath} is not used.");
                    continue;
                }

                var fullPath = Path.Combine(baseDir ?? "", resPath.Replace('/', '\\'));
                byte[] data = LoadImageBytes(fullPath, resMd, context, out var palette);
                resMd.Data = data;
                if (palette != null)
                {
                    resMd.PalData = palette;
                }
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
            if (resx.Resources == null)
            {
                return;
            }

            FreeMountContext context = FreeMount.CreateContext(resx.Context);
            var resList = psb.CollectResources(duplicatePalette: Consts.GeneratePalette);

            foreach (var resxResource in resx.Resources)
            {
                //Scan for Resource
                var resMd = resList.FirstOrDefault(r =>
                    resxResource.Key == r.GetFriendlyName(psb.Type));
                if (resMd == null && psb.Type == PsbType.Pimg)
                {
                    resMd = resList.FirstOrDefault(r => resxResource.Key == Path.GetFileNameWithoutExtension(r.Name));
                }

                if (resMd == null && uint.TryParse(resxResource.Key, out uint rid))
                {
                    resMd = resList.FirstOrDefault(r => r.Index == rid);
                    if (resMd == null)
                    {
                        //support raw palette
                        var palResMds = resList.FindAll(r => r.Palette?.Index == rid);
                        if (palResMds.Count > 0)
                        {
                            var palFullPath = Path.IsPathRooted(resxResource.Value)
                                ? resxResource.Value
                                : Path.Combine(baseDir ?? "", resxResource.Value.Replace('/', '\\'));
                            var palRawData = File.ReadAllBytes(palFullPath);
                            foreach (var palResMd in palResMds)
                            {
                                palResMd.PalData = palRawData;
                            }
                            continue;
                        }
                    }
                }

                if (resMd == null)
                {
                    Console.WriteLine($"[WARN]{resxResource.Key} is not used.");
                    continue;
                }

                var fullPath = Path.IsPathRooted(resxResource.Value)
                    ? resxResource.Value
                    : Path.Combine(baseDir ?? "", resxResource.Value.Replace('/', '\\'));
                byte[] data = LoadImageBytes(fullPath, resMd, context, out var palette);
                resMd.Data = data;
                if (palette != null)
                {
                    resMd.PalData = palette;
                }
            }
        }
    }
}