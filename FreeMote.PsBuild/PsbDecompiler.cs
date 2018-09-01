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
                Stream stream = fs;
                var ms = ctx.OpenFromShell(fs, ref type);
                if (ms != null)
                {
                    ctx.Context[FreeMount.PsbShellType] = type;
                    fs.Dispose();
                    stream = ms;
                }
                try
                {
                    psb = new PSB(stream, false);
                }
                catch (PsbBadFormatException e) when (e.Reason == PsbBadFormatReason.Header || e.Reason == PsbBadFormatReason.Array || e.Reason == PsbBadFormatReason.Body) //maybe encrypted
                {
                    stream.Position = 0;
                    uint? key = null;
                    if (ctx.Context.ContainsKey(FreeMount.CryptKey))
                    {
                        key = ctx.Context[FreeMount.CryptKey] as uint?;
                    }
                    else
                    {
                        key = ctx.GetKey(stream);
                    }

                    stream.Position = 0;
                    if (key != null) //try use key
                    {
                        try
                        {
                            using (var mms = new MemoryStream((int)stream.Length))
                            {
                                PsbFile.Encode(key.Value, EncodeMode.Decrypt, EncodePosition.Auto, stream, mms);
                                stream.Dispose();
                                psb = new PSB(mms);
                                ctx.Context[FreeMount.CryptKey] = key;
                            }
                        }
                        catch
                        {
                            throw e;
                        }
                    }
                    else //key = null
                    {
                        if (e.Reason == PsbBadFormatReason.Header) //now try Dullahan loading
                        {
                            psb = PSB.DullahanLoad(stream);
                        }
                        else
                        {
                            throw;
                        }
                    }

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
        /// <param name="key">PSB CryptKey</param>
        public static void DecompileToFile(string inputPath, PsbImageOption imageOption = PsbImageOption.Original, PsbImageFormat extractFormat = PsbImageFormat.Png, bool useResx = true, uint? key = null)
        {
            var context = FreeMount.CreateContext();
            if (key != null)
            {
                context.Context[FreeMount.CryptKey] = key;
            }
            var name = Path.GetFileNameWithoutExtension(inputPath);
            var dirPath = Path.Combine(Path.GetDirectoryName(inputPath), name);
            File.WriteAllText(Path.ChangeExtension(inputPath, ".json"), Decompile(inputPath, out var psb, context.Context)); //MARK: breaking change for json path
            var resources = psb.CollectResources();
            PsbResourceJson resx = new PsbResourceJson
            {
                PsbVersion = psb.Header.Version,
                PsbType = psb.Type,
                Platform = psb.Platform,
                CryptKey = context.Context.ContainsKey(FreeMount.CryptKey) ? (uint?)context.Context[FreeMount.CryptKey] : null,
                ExternalTextures = psb.Type == PsbType.Motion && psb.Resources.Count <= 0,
            };

            if (File.Exists(dirPath))
            {
                dirPath += "-resources";
            }
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
                    string relativePath = resource.GetFriendlyName(psb.Type);

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
                File.WriteAllText(Path.ChangeExtension(inputPath, ".resx.json"), JsonConvert.SerializeObject(resx, Formatting.Indented));
            }
            else
            {
                File.WriteAllText(Path.ChangeExtension(inputPath, ".res.json"), JsonConvert.SerializeObject(resDictionary.Values.ToList(), Formatting.Indented));
            }
        }
    }
}
