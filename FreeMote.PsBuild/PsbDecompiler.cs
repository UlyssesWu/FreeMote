using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using FreeMote.Psb;
using FreeMote.Plugins;
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
        /// <param name="context"></param>
        /// <returns></returns>
        public static string Decompile(string path, out PSB psb, Dictionary<string, object> context = null)
        {
            using (var fs = File.OpenRead(path))
            {
                var ctx = FreeMount.CreateContext(context);
                string type = null;
                var ms = ctx.OpenFromShell(fs, ref type);
                if (ms != null)
                {
                    ctx.Context[FreeMount.PsbShellType] = type;
                    psb = new PSB(ms);
                }
                else
                {
                    psb = new PSB(fs);
                }
                return Decompile(psb);
            }
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
            var context = FreeMount.CreateContext();

            var name = Path.GetFileNameWithoutExtension(inputPath);
            var dirPath = Path.Combine(Path.GetDirectoryName(inputPath), name);
            File.WriteAllText(inputPath + ".json", Decompile(inputPath, out var psb, context.Context));
            var resources = psb.CollectResources();
            PsbResourceJson resx = new PsbResourceJson
            {
                PsbVersion = psb.Header.Version,
                PsbType = psb.Type,
                Platform = psb.Platform,
                ExternalTextures = psb.Type == PsbType.Motion && psb.Resources.Count <= 0,
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
                            ImageFormat pixelFormat;
                            switch (extractFormat)
                            {
                                case PsbImageFormat.Png:
                                    relativePath += ".png";
                                    pixelFormat = ImageFormat.Png;
                                    break;
                                default:
                                    relativePath += ".bmp";
                                    pixelFormat = ImageFormat.Bmp;
                                    break;
                            }

                            if (resource.Compress == PsbCompressType.RL)
                            {
                                RL.UncompressToImageFile(resource.Data, Path.Combine(dirPath, relativePath),
                                    resource.Height, resource.Width, extractFormat, resource.PixelFormat);
                            }
                            else if (resource.Compress == PsbCompressType.Tlg || resource.Compress == PsbCompressType.ByName)
                            {
                                var bmp = context.ResourceToBitmap(resource.Compress == PsbCompressType.Tlg
                                    ? ".tlg"
                                    : Path.GetExtension(resource.Name), resource.Data);
                                if (bmp == null)
                                {
                                    if (resource.Compress == PsbCompressType.Tlg) //Fallback to managed TLG decoder
                                    {
                                        using (var ms = new MemoryStream(resource.Data))
                                        using (var br = new BinaryReader(ms))
                                        {
                                            bmp = new TlgImageConverter().Read(br);
                                            bmp.Save(Path.Combine(dirPath, relativePath), pixelFormat);
                                            bmp.Dispose();
                                        }
                                    }
                                    relativePath = Path.ChangeExtension(relativePath, Path.GetExtension(resource.Name));
                                    File.WriteAllBytes(Path.Combine(dirPath, relativePath), resource.Data);
                                }
                                else
                                {
                                    bmp.Save(Path.Combine(dirPath, relativePath), pixelFormat);
                                    bmp.Dispose();
                                }
                            }
                            //else if (resource.Compress == PsbCompressType.ByName)
                            //{
                            //    relativePath = Path.ChangeExtension(relativePath, Path.GetExtension(resource.Name));
                            //    File.WriteAllBytes(Path.Combine(dirPath, relativePath), resource.Data);
                            //}
                            else
                            {
                                RL.ConvertToImageFile(resource.Data, Path.Combine(dirPath, relativePath),
                                    resource.Height, resource.Width, extractFormat, resource.PixelFormat);
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
                        throw new PsbBadFormatException(PsbBadFormatReason.Resources, "There are resources with same names! Try Raw export mode.", e);
                    }
                }
            }

            //MARK: We use `.resx.json` to distinguish from psbtools' `.res.json`
            if (useResx)
            {
                resx.Resources = resDictionary;
                resx.Context = context.Context;
                File.WriteAllText(inputPath + ".resx.json", JsonConvert.SerializeObject(resx, Formatting.Indented));
            }
            else
            {
                File.WriteAllText(inputPath + ".res.json", JsonConvert.SerializeObject(resDictionary.Values.ToList(), Formatting.Indented));
            }
        }
    }
}
