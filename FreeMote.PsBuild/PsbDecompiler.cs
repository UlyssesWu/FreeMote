using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using FreeMote.Psb;
using Newtonsoft.Json;

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
        /// <param name="psb"></param>
        /// <returns></returns>
        public static string Decompile(string path, out PSB psb)
        {
            psb = new PSB(path);
            return Decompile(psb);
        }

        internal static string Decompile(PSB psb)
        {
            return JsonConvert.SerializeObject(psb.Objects, Formatting.Indented, new PsbJsonConverter());
        }

        /// <summary>
        /// Decompile to files
        /// </summary>
        /// <param name="inputPath">PSB file path</param>
        /// <param name="imageOption">whether to extract image to common format</param>
        /// <param name="extractFormat">if extract, what format do you want</param>
        /// <param name="useResx">if false, use array-based resource json (legacy)</param>
        public static void DecompileToFile(string inputPath, PsbImageOption imageOption = PsbImageOption.Original, PsbImageFormat extractFormat = PsbImageFormat.Png, bool useResx = true)
        {
            var name = Path.GetFileNameWithoutExtension(inputPath);
            var dirPath = Path.Combine(Path.GetDirectoryName(inputPath), name);
            File.WriteAllText(inputPath + ".json", Decompile(inputPath, out var psb));
            var resources = psb.CollectResources();
            PsbResourceJson resx = new PsbResourceJson
            {
                PsbVersion = psb.Header.Version,
                PsbType = psb.Type,
                Platform = psb.Platform,
                ExternalTextures = psb.Type == PsbType.Motion && psb.Resources.Count <= 0
            };
            if (!Directory.Exists(dirPath)) //ensure no file with same name!
            {
                Directory.CreateDirectory(dirPath);
            }
            Dictionary<string, string> resDictionary = new Dictionary<string, string>();

            if (imageOption == PsbImageOption.Original)
            {
                for (int i = 0; i < psb.Resources.Count; i++)
                {
                    var relativePath = psb.Resources[i].Index == null ? $"#{i}.bin" : $"{psb.Resources[i].Index}.bin";

                    File.WriteAllBytes(
                        Path.Combine(dirPath, relativePath),
                        psb.Resources[i].Data);
                    resDictionary.Add(Path.GetFileNameWithoutExtension(relativePath), $"{name}/{relativePath}");
                }
            }
            else
            {
                for (int i = 0; i < resources.Count; i++)
                {
                    var resource = resources[i];
                    //Generate Friendly Name
                    string relativePath;
                    if (psb.Type == PsbType.Pimg && !string.IsNullOrWhiteSpace(resource.Name))
                    {
                        relativePath = Path.GetFileNameWithoutExtension(resource.Name);
                    }
                    else if (string.IsNullOrWhiteSpace(resource.Name) || string.IsNullOrWhiteSpace(resource.Part))
                    {
                        relativePath = resource.Index.ToString();
                    }
                    else
                    {
                        relativePath = $"{resource.Part}{PsbResCollector.ResourceNameDelimiter}{resource.Name}";
                    }

                    switch (imageOption)
                    {
                        case PsbImageOption.Extract:
                            //var pixelFormat = resource.Spec.DefaultPixelFormat(); //MARK: PixelFormat should refer `type`
                            switch (extractFormat)
                            {
                                case PsbImageFormat.Png:
                                    relativePath += ".png";
                                    if (resource.Compress == PsbCompressType.RL)
                                    {
                                        RL.UncompressToImageFile(resource.Data, Path.Combine(dirPath, relativePath),
                                            resource.Height, resource.Width, PsbImageFormat.Png, resource.PixelFormat);
                                    }
                                    else if (resource.Compress == PsbCompressType.Tlg
                                             || resource.Compress == PsbCompressType.ByName && resource.Name.EndsWith(".tlg", true, null))
                                    {
                                        TlgImageConverter converter = new TlgImageConverter();
                                        using (var ms = new MemoryStream(resource.Data))
                                        {
                                            BinaryReader br = new BinaryReader(ms);
                                            converter.Read(br).Save(Path.Combine(dirPath, relativePath), ImageFormat.Png);
                                        }
                                        //WARN: tlg is kept and recorded in resource json for compile 
                                        relativePath = Path.ChangeExtension(relativePath, ".tlg");
                                        File.WriteAllBytes(Path.Combine(dirPath, relativePath), resource.Data);
                                    }
                                    else if (resource.Compress == PsbCompressType.ByName)
                                    {
                                        relativePath = Path.ChangeExtension(relativePath, Path.GetExtension(resource.Name));
                                        File.WriteAllBytes(Path.Combine(dirPath, relativePath), resource.Data);
                                    }
                                    else
                                    {
                                        RL.ConvertToImageFile(resource.Data, Path.Combine(dirPath, relativePath),
                                            resource.Height, resource.Width, extractFormat, resource.PixelFormat);
                                    }
                                    break;
                                default:
                                    relativePath += ".bmp";
                                    if (resource.Compress == PsbCompressType.RL)
                                    {
                                        RL.UncompressToImageFile(resource.Data, Path.Combine(dirPath, relativePath),
                                            resource.Height, resource.Width, PsbImageFormat.Bmp, resource.PixelFormat);
                                    }
                                    else if (resource.Compress == PsbCompressType.Tlg
                                             || resource.Compress == PsbCompressType.ByName && resource.Name.EndsWith(".tlg", true, null))
                                    {
                                        TlgImageConverter converter = new TlgImageConverter();
                                        using (var ms = new MemoryStream(resource.Data))
                                        {
                                            BinaryReader br = new BinaryReader(ms);
                                            converter.Read(br).Save(Path.Combine(dirPath, relativePath), ImageFormat.Bmp);
                                        }
                                        //WARN: tlg is kept and recorded in resource json for compile 
                                        relativePath = Path.ChangeExtension(relativePath, ".tlg");
                                        File.WriteAllBytes(Path.Combine(dirPath, relativePath), resource.Data);
                                    }
                                    else if (resource.Compress == PsbCompressType.ByName)
                                    {
                                        relativePath = Path.ChangeExtension(relativePath, Path.GetExtension(resource.Name));
                                        File.WriteAllBytes(Path.Combine(dirPath, relativePath), resource.Data);
                                    }
                                    else
                                    {
                                        RL.ConvertToImageFile(resource.Data, Path.Combine(dirPath, relativePath),
                                            resource.Height, resource.Width, extractFormat, resource.PixelFormat);
                                    }
                                    break;
                            }
                            break;
                        case PsbImageOption.Original:
                            if (resources[i].Compress == PsbCompressType.RL)
                            {
                                relativePath += ".rl";
                                File.WriteAllBytes(Path.Combine(dirPath, relativePath), resource.Data);
                            }
                            else if (resource.Compress == PsbCompressType.Tlg)
                            {
                                relativePath += ".tlg";
                                File.WriteAllBytes(Path.Combine(dirPath, relativePath), resource.Data);
                            }
                            else
                            {
                                relativePath += ".raw";
                                File.WriteAllBytes(Path.Combine(dirPath, relativePath), resource.Data);
                            }
                            break;
                        case PsbImageOption.Uncompress:
                            File.WriteAllBytes(Path.Combine(dirPath, relativePath),
                                resources[i].Compress == PsbCompressType.RL
                                    ? RL.Uncompress(resource.Data)
                                    : resource.Data);
                            relativePath += ".raw";
                            break;
                        case PsbImageOption.Compress:
                            File.WriteAllBytes(Path.Combine(dirPath, relativePath),
                                resources[i].Compress != PsbCompressType.RL
                                    ? RL.Compress(resource.Data)
                                    : resource.Data);
                            relativePath += ".rl";
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(imageOption), imageOption, null);
                    }

                    try
                    {
                        resDictionary.Add(Path.GetFileNameWithoutExtension(relativePath), $"{name}/{relativePath}");
                    }
                    catch (ArgumentException e)
                    {
                        throw new BadImageFormatException("There are resources with same names! Try Raw export mode.", e);
                    }
                }
            }

            //MARK: We use `.resx.json` to distinguish from psbtools' `.res.json`
            if (useResx)
            {
                resx.Resources = resDictionary;
                File.WriteAllText(inputPath + ".resx.json", JsonConvert.SerializeObject(resx, Formatting.Indented));
            }
            else
            {
                File.WriteAllText(inputPath + ".res.json", JsonConvert.SerializeObject(resDictionary.Values.ToList(), Formatting.Indented));
            }
        }
    }
}
