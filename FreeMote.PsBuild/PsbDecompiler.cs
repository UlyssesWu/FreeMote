using System;
using System.Collections.Generic;
using System.IO;
using FreeMote.Psb;
using Newtonsoft.Json;

// ReSharper disable AssignNullToNotNullAttribute

namespace FreeMote.PsBuild
{
    public enum PsbImageOption
    {
        /// <summary>
        /// Keep original
        /// </summary>
        Original,
        /// <summary>
        /// Uncompress if needed
        /// </summary>
        Raw,
        /// <summary>
        /// Compress if needed
        /// </summary>
        Compress,
        /// <summary>
        /// Try to convert to common format
        /// </summary>
        Extract,

    }
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

        public static string Decompile(string path, out List<ResourceMetadata> resources)
        {
            PSB psb = new PSB(path);
            resources = psb.CollectResources();
            return Decompile(psb);
        }

        internal static string Decompile(PSB psb)
        {
            return JsonConvert.SerializeObject(psb.Objects, Formatting.Indented, new PsbTypeConverter());
        }

        /// <summary>
        /// Decompile to files
        /// </summary>
        /// <param name="inputPath">PSB file path</param>
        /// <param name="imageOption">whether to extract image to common format</param>
        /// <param name="extractFormat">if extract, what format do you want</param>
        public static void DecompileToFile(string inputPath, PsbImageOption imageOption = PsbImageOption.Original, PsbImageFormat extractFormat = PsbImageFormat.Bmp)
        {
            var name = Path.GetFileNameWithoutExtension(inputPath);
            var dirPath = Path.Combine(Path.GetDirectoryName(inputPath), name);
            File.WriteAllText(inputPath + ".json", Decompile(inputPath, out List<ResourceMetadata> resources));
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }
            var resPaths = new List<string>(resources.Count);
            for (int i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                var relativePath = $"{name}/{i}";
                switch (imageOption)
                {
                    case PsbImageOption.Extract:
                        var pixelFormat = resource.Spec.PixelFormat();
                        switch (extractFormat)
                        {
                            case PsbImageFormat.Png:
                                relativePath += ".png";
                                if (resource.Compress == PsbCompressType.RL)
                                {
                                    RlCompress.UncompressToImageFile(resource.Data, Path.Combine(dirPath, $"{i}.png"), resource.Height, resource.Width, PsbImageFormat.Png, pixelFormat);
                                }
                                else
                                {
                                    RlCompress.ConvertToImageFile(resource.Data, Path.Combine(dirPath, $"{i}.png"), resource.Height, resource.Width, extractFormat, pixelFormat);
                                }
                                break;
                            default:
                                relativePath += ".bmp";
                                if (resource.Compress == PsbCompressType.RL)
                                {
                                    RlCompress.UncompressToImageFile(resource.Data, Path.Combine(dirPath, $"{i}.bmp"), resource.Height, resource.Width, PsbImageFormat.Bmp, pixelFormat);
                                }
                                else
                                {
                                    RlCompress.ConvertToImageFile(resource.Data, Path.Combine(dirPath, $"{i}.bmp"), resource.Height, resource.Width, extractFormat, pixelFormat);
                                }
                                break;
                        }
                        break;
                    case PsbImageOption.Original:
                        if (resources[i].Compress == PsbCompressType.RL)
                        {
                            relativePath += ".rl";
                            File.WriteAllBytes(Path.Combine(dirPath, $"{i}.rl"), resource.Data);
                        }
                        else
                        {
                            relativePath += ".bin";
                            File.WriteAllBytes(Path.Combine(dirPath, $"{i}.bin"), resource.Data);
                        }
                        break;
                    case PsbImageOption.Raw:
                        File.WriteAllBytes(Path.Combine(dirPath, $"{i}.bin"),
                            resources[i].Compress == PsbCompressType.RL
                                ? RlCompress.Uncompress(resource.Data)
                                : resource.Data);
                        relativePath += ".bin";
                        break;
                    case PsbImageOption.Compress:
                        File.WriteAllBytes(Path.Combine(dirPath, $"{i}.rl"),
                            resources[i].Compress != PsbCompressType.RL
                                ? RlCompress.Compress(resource.Data)
                                : resource.Data);
                        relativePath += ".rl";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(imageOption), imageOption, null);
                }
                resPaths.Add(relativePath);
            }
            //MARK: We use `.resx.json` to distinguish from psbtools' `.res.json`
            File.WriteAllText(inputPath + ".resx.json", JsonConvert.SerializeObject(resPaths, Formatting.Indented));
        }
    }
}
