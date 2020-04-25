using System;
using System.Collections.Generic;
using System.Drawing;
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
                    ctx.Context[Consts.Context_PsbShellType] = type;
                    fs.Dispose();
                    stream = ms;
                }

                try
                {
                    psb = new PSB(stream, false);
                }
                catch (PsbBadFormatException e) when (e.Reason == PsbBadFormatReason.Header ||
                                                      e.Reason == PsbBadFormatReason.Array ||
                                                      e.Reason == PsbBadFormatReason.Body) //maybe encrypted
                {
                    stream.Position = 0;
                    uint? key = null;
                    if (ctx.Context.ContainsKey(Consts.Context_CryptKey))
                    {
                        key = ctx.Context[Consts.Context_CryptKey] as uint?;
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
                            using (var mms = new MemoryStream((int) stream.Length))
                            {
                                PsbFile.Encode(key.Value, EncodeMode.Decrypt, EncodePosition.Auto, stream, mms);
                                stream.Dispose();
                                psb = new PSB(mms);
                                ctx.Context[Consts.Context_CryptKey] = key;
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

        public static string Decompile(PSB psb)
        {
            return JsonConvert.SerializeObject(psb.Objects, Formatting.Indented,
                new PsbJsonConverter(Consts.JsonArrayCollapse, Consts.JsonUseDoubleOnly,
                    Consts.JsonUseHexNumber));
        }

        internal static void OutputResources(PSB psb, FreeMountContext context, string filePath, PsbExtractOption extractOption = PsbExtractOption.Original,
            PsbImageFormat extractFormat = PsbImageFormat.Png, bool useResx = true)
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            var dirPath = Path.Combine(Path.GetDirectoryName(filePath), name);

            var resources = psb.CollectResources();
            PsbResourceJson resx = new PsbResourceJson(psb, context.Context);

            if (File.Exists(dirPath))
            {
                name += "-resources";
                dirPath += "-resources";
            }

            if (!Directory.Exists(dirPath)) //ensure there is no file with same name!
            {
                if (psb.Resources.Count != 0 || resources.Count != 0)
                {
                    Directory.CreateDirectory(dirPath);
                }
            }

            Dictionary<string, string> resDictionary = new Dictionary<string, string>();

            if (extractOption == PsbExtractOption.Original)
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
                    var friendlyName = resource.GetFriendlyName(psb.Type);
                    string relativePath = friendlyName;
                    if (string.IsNullOrWhiteSpace(friendlyName))
                    {
                        relativePath = resource.Resource.Index?.ToString() ?? $"({i})";
                        friendlyName = i.ToString();
                    }

                    var currentExtractOption = extractOption;
                    if (resource.Width <= 0 || resource.Height <= 0) //impossible to extract, just keep raw
                    {
                        if (currentExtractOption == PsbExtractOption.Extract)
                        {
                            currentExtractOption = PsbExtractOption.Original;
                        }
                    }

                    switch (currentExtractOption)
                    {
                        case PsbExtractOption.Extract:
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

                            relativePath = CheckPath(relativePath, i);
                            if (resource.Compress == PsbCompressType.RL)
                            {
                                RL.DecompressToImageFile(resource.Data, Path.Combine(dirPath, relativePath),
                                    resource.Height, resource.Width, extractFormat, resource.PixelFormat);
                            }
                            else if (resource.Compress == PsbCompressType.Tlg ||
                                     resource.Compress == PsbCompressType.ByName)
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
                                    resource.Height, resource.Width, extractFormat, resource.PixelFormat, resource.PalData, resource.PalettePixelFormat);
                            }

                            break;
                        case PsbExtractOption.Original:
                            if (resources[i].Compress == PsbCompressType.RL)
                            {
                                relativePath += ".rl";
                                relativePath = CheckPath(relativePath, i);
                                File.WriteAllBytes(Path.Combine(dirPath, relativePath), resource.Data);
                            }
                            else if (resource.Compress == PsbCompressType.Tlg)
                            {
                                relativePath += ".tlg";
                                relativePath = CheckPath(relativePath, i);
                                File.WriteAllBytes(Path.Combine(dirPath, relativePath), resource.Data);
                            }
                            else
                            {
                                relativePath += ".raw";
                                relativePath = CheckPath(relativePath, i);
                                File.WriteAllBytes(Path.Combine(dirPath, relativePath), resource.Data);
                            }

                            break;
                        case PsbExtractOption.Decompress:
                            relativePath += ".raw";
                            relativePath = CheckPath(relativePath, i);
                            File.WriteAllBytes(Path.Combine(dirPath, relativePath),
                                resources[i].Compress == PsbCompressType.RL
                                    ? RL.Decompress(resource.Data)
                                    : resource.Data);
                            break;
                        case PsbExtractOption.Compress:
                            relativePath += ".rl";
                            relativePath = CheckPath(relativePath, i);
                            File.WriteAllBytes(Path.Combine(dirPath, relativePath),
                                resources[i].Compress != PsbCompressType.RL
                                    ? RL.Compress(resource.Data)
                                    : resource.Data);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(currentExtractOption), currentExtractOption, null);
                    }

                    try
                    {
                        resDictionary.Add(resource.Resource.Index == null? friendlyName : resource.Index.ToString(), $"{name}/{relativePath}");
                    }
                    catch (ArgumentException e)
                    {
                        throw new PsbBadFormatException(PsbBadFormatReason.Resources,
                            "Resource Export Error: Name conflict, or Index is not specified. Try Raw export mode.", e);
                    }
                }
            }

            //MARK: We use `.resx.json` to distinguish from psbtools' `.res.json`
            if (useResx)
            {
                resx.Resources = resDictionary;
                resx.Context = context.Context;
                File.WriteAllText(Path.ChangeExtension(filePath, ".resx.json"),
                    JsonConvert.SerializeObject(resx, Formatting.Indented));
            }
            else
            {
                File.WriteAllText(Path.ChangeExtension(filePath, ".res.json"),
                    JsonConvert.SerializeObject(resDictionary.Values.ToList(), Formatting.Indented));
            }

            string CheckPath(string rPath, int id)
            {
                var k = Path.GetFileNameWithoutExtension(rPath);
                if (resDictionary.ContainsKey(k))
                {
                    return $"{id}{Path.GetExtension(rPath)}";
                }

                return rPath;
            }
        }

        /// <summary>
        /// Decompile to files
        /// </summary>
        /// <param name="psb">PSB</param>
        /// <param name="outputPath">Output json file name, should end with .json</param>
        /// <param name="additionalContext">additional context used in decompilation</param>
        /// <param name="extractOption">whether to extract image to common format</param>
        /// <param name="extractFormat">if extract, what format do you want</param>
        /// <param name="useResx">if false, use array-based resource json (legacy)</param>
        /// <param name="key">PSB CryptKey</param>
        public static void DecompileToFile(PSB psb, string outputPath, Dictionary<string, object> additionalContext = null, PsbExtractOption extractOption = PsbExtractOption.Original,
            PsbImageFormat extractFormat = PsbImageFormat.Png, bool useResx = true, uint? key = null)
        {
            var context = FreeMount.CreateContext(additionalContext);
            if (key != null)
            {
                context.Context[Consts.Context_CryptKey] = key;
            }

            File.WriteAllText(outputPath, Decompile(psb)); //MARK: breaking change for json path

            OutputResources(psb, context, outputPath, extractOption, extractFormat, useResx);
        }

        /// <summary>
        /// Decompile to files
        /// </summary>
        /// <param name="inputPath">PSB file path</param>
        /// <param name="extractOption">whether to extract image to common format</param>
        /// <param name="extractFormat">if extract, what format do you want</param>
        /// <param name="useResx">if false, use array-based resource json (legacy)</param>
        /// <param name="key">PSB CryptKey</param>
        public static void DecompileToFile(string inputPath, PsbExtractOption extractOption = PsbExtractOption.Original,
            PsbImageFormat extractFormat = PsbImageFormat.Png, bool useResx = true, uint? key = null)
        {
            var context = FreeMount.CreateContext();
            if (key != null)
            {
                context.Context[Consts.Context_CryptKey] = key;
            }

            File.WriteAllText(Path.ChangeExtension(inputPath, ".json"),
                Decompile(inputPath, out var psb, context.Context)); //MARK: breaking change for json path

            OutputResources(psb, context, inputPath, extractOption, extractFormat, useResx);
        }

        /// <summary>
        /// Inlined PSB -> External Texture PSB. Inverse of <seealso cref="PsbCompiler.Link"/>
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="order">To make a regular external texture PSB you should set it to <see cref="PsbLinkOrderBy.Name"/>.</param>
        /// <param name="disposeResInPsb">Whether to remove resources in PSB. To make a real external texture PSB you should set it to true.</param>
        /// <returns>Ordered textures</returns>
        public static List<Bitmap> Unlink(this PSB psb, PsbLinkOrderBy order = PsbLinkOrderBy.Name, bool disposeResInPsb = true)
        {
            var resources = psb.CollectResources();
            List<Bitmap> texs = new List<Bitmap>();

            for (int i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                var tex = RL.ConvertToImage(resource.Data, resource.PalData, resource.Height, resource.Width, resource.PixelFormat, resource.PalettePixelFormat);

                switch (order)
                {
                    case PsbLinkOrderBy.Convention:
                        tex.Tag = resource.GetFriendlyName(psb.Type);
                        break;
                    default:
                        var tId = resource.TextureIndex;
                        if (tId == null)
                        {
                            throw new FormatException(
                                "Unable to unlink with texture names since they can't be recognized.");
                        }

                        tex.Tag = $"tex{tId.Value:D3}";
                        break;
                }
                
                texs.Add(tex);

                //Finally, dispose
                if (disposeResInPsb)
                {
                    resource.Data = null;
                }
            }

            return texs;
        }

        /// <summary>
        /// Convert a PSB to External Texture PSB.
        /// </summary>
        /// <param name="inputPath"></param>
        /// <param name="outputUnlinkedPsb">output unlinked PSB; otherwise only output textures</param>
        /// <param name="order"></param>
        /// <param name="format"></param>
        /// <returns>The unlinked PSB path</returns>
        public static string UnlinkToFile(string inputPath, bool outputUnlinkedPsb = true, PsbLinkOrderBy order = PsbLinkOrderBy.Name, PsbImageFormat format = PsbImageFormat.Png)
        {
            if (!File.Exists(inputPath))
            {
                return null;
            }

            var name = Path.GetFileNameWithoutExtension(inputPath);
            var dirPath = Path.Combine(Path.GetDirectoryName(inputPath), name);
            var psbSavePath = "";
            if (File.Exists(dirPath))
            {
                name += "-resources";
                dirPath += "-resources";
            }

            if (!Directory.Exists(dirPath)) //ensure there is no file with same name!
            {
                Directory.CreateDirectory(dirPath);
            }

            var psb = new PSB(inputPath);
            var texs = psb.Unlink();

            if (outputUnlinkedPsb)
            {
                psb.Merge();
                psbSavePath = Path.ChangeExtension(inputPath, ".unlinked.psb"); //unlink only works with motion.psb so no need for ext rename
                File.WriteAllBytes(psbSavePath, psb.Build());
            }

            var texExt = format == PsbImageFormat.Bmp ? ".bmp" :".png";
            var texFormat = format == PsbImageFormat.Bmp ? ImageFormat.Bmp : ImageFormat.Png;
           
            switch (order)
            {
                case PsbLinkOrderBy.Convention:
                    foreach (var tex in texs)
                    {
                        tex.Save(Path.Combine(dirPath, tex.Tag + texExt), texFormat);
                    }
                    break;
                case PsbLinkOrderBy.Name:
                    foreach (var tex in texs)
                    {
                        tex.Save(Path.Combine(dirPath, $"{name}_{tex.Tag}{texExt}"), texFormat);
                    }
                    break;
                case PsbLinkOrderBy.Order:
                    for (var i = 0; i < texs.Count; i++)
                    {
                        var tex = texs[i];
                        tex.Save(Path.Combine(dirPath, $"{i}{texExt}"), texFormat);
                    }
                    break;
            }

            return psbSavePath;
        }
    }
}