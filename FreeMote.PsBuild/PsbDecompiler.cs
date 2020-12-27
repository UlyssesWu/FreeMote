using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FreeMote.Psb;
using FreeMote.Plugins;
using FreeMote.Psb.Textures;
using FreeMote.Psb.Types;
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
                        if (e.Reason == PsbBadFormatReason.Header || e.Reason == PsbBadFormatReason.Array) //now try Dullahan loading
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
            if (Consts.JsonArrayCollapse)
            {
                return ArrayCollapseJsonTextWriter.SerializeObject(psb.Objects,
                    new PsbJsonConverter(Consts.JsonUseDoubleOnly, Consts.JsonUseHexNumber));
            }
            return JsonConvert.SerializeObject(psb.Objects, Formatting.Indented,
                new PsbJsonConverter(Consts.JsonUseDoubleOnly, Consts.JsonUseHexNumber));
        }

        internal static void OutputResources(PSB psb, FreeMountContext context, string filePath, PsbExtractOption extractOption = PsbExtractOption.Original,
            PsbImageFormat extractFormat = PsbImageFormat.png, bool useResx = true)
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            var dirPath = Path.Combine(Path.GetDirectoryName(filePath), name);
            PsbResourceJson resx = new PsbResourceJson(psb, context.Context);
            
            if (File.Exists(dirPath))
            {
                name += "-resources";
                dirPath += "-resources";
            }

            var extraDir = Path.Combine(dirPath, Consts.ExtraResourceFolderName);
            
            if (!Directory.Exists(dirPath)) //ensure there is no file with same name!
            {
                if (psb.Resources.Count != 0)
                {
                    Directory.CreateDirectory(dirPath);
                }
            }

            if (psb.ExtraResources.Count > 0)
            {
                var extraDic = PsbResHelper.OutputExtraResources(psb, context, name, extraDir, out var flattenArrays, extractOption);
                resx.ExtraResources = extraDic;
                if (flattenArrays != null && flattenArrays.Count > 0)
                {
                    resx.ExtraFlattenArrays = flattenArrays;
                }
            }

            var resDictionary = psb.TypeHandler.OutputResources(psb, context, name, dirPath, extractOption);

            //MARK: We use `.resx.json` to distinguish from psbtools' `.res.json`
            if (useResx)
            {
                resx.Resources = resDictionary;
                resx.Context = context.Context;
                string json;
                if (Consts.JsonArrayCollapse)
                {
                    json = ArrayCollapseJsonTextWriter.SerializeObject(resx);
                }
                else
                {
                    json = JsonConvert.SerializeObject(resx, Formatting.Indented);
                }
                File.WriteAllText(Path.ChangeExtension(filePath, ".resx.json"), json);
            }
            else
            {
                if (psb.ExtraResources.Count > 0)
                {
                    throw new NotSupportedException("PSBv4 cannot use legacy res.json format.");
                }
                File.WriteAllText(Path.ChangeExtension(filePath, ".res.json"),
                    JsonConvert.SerializeObject(resDictionary.Values.ToList(), Formatting.Indented));
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
            PsbImageFormat extractFormat = PsbImageFormat.png, bool useResx = true, uint? key = null)
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
            PsbImageFormat extractFormat = PsbImageFormat.png, bool useResx = true, uint? key = null)
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
        /// Convert a PSB to External Texture PSB.
        /// </summary>
        /// <param name="inputPath"></param>
        /// <param name="outputUnlinkedPsb">output unlinked PSB; otherwise only output textures</param>
        /// <param name="order"></param>
        /// <param name="format"></param>
        /// <returns>The unlinked PSB path</returns>
        public static string UnlinkToFile(string inputPath, bool outputUnlinkedPsb = true, PsbLinkOrderBy order = PsbLinkOrderBy.Name, PsbImageFormat format = PsbImageFormat.png)
        {
            if (!File.Exists(inputPath))
            {
                return null;
            }

            var name = Path.GetFileNameWithoutExtension(inputPath);
            var dirPath = Path.Combine(Path.GetDirectoryName(inputPath), name);
            var psbSavePath = inputPath;
            if (File.Exists(dirPath))
            {
                name += "-resources";
                dirPath += "-resources";
            }

            if (!Directory.Exists(dirPath)) //ensure there is no file with same name!
            {
                Directory.CreateDirectory(dirPath);
            }

            var context = FreeMount.CreateContext();
            context.ImageFormat = format;
            var psb = new PSB(inputPath);
            if (psb.TypeHandler is BaseImageType imageType)
            {
                imageType.UnlinkToFile(psb, context, name, dirPath, outputUnlinkedPsb, order);
            }

            psb.TypeHandler.UnlinkToFile(psb, context, name, dirPath, outputUnlinkedPsb, order);

            if (outputUnlinkedPsb)
            {
                psb.Merge();
                psbSavePath = Path.ChangeExtension(inputPath, ".unlinked.psb"); //unlink only works with motion.psb so no need for ext rename
                File.WriteAllBytes(psbSavePath, psb.Build());
            }

            return psbSavePath;
        }

        /// <summary>
        /// Save (most user friendly) images
        /// </summary>
        /// <param name="inputPath"></param>
        /// <param name="format"></param>
        public static void ExtractImageFiles(string inputPath, PsbImageFormat format = PsbImageFormat.png)
        {
            if (!File.Exists(inputPath))
            {
                return;
            }

            var name = Path.GetFileNameWithoutExtension(inputPath);
            var dirPath = Path.Combine(Path.GetDirectoryName(inputPath), name);

            if (File.Exists(dirPath))
            {
                name += "-resources";
                dirPath += "-resources";
            }

            if (!Directory.Exists(dirPath)) //ensure there is no file with same name!
            {
                Directory.CreateDirectory(dirPath);
            }

            var texExt = format == PsbImageFormat.bmp ? ".bmp" : ".png";
            var texFormat = format.ToImageFormat();

            var psb = new PSB(inputPath);
            if (psb.Type == PsbType.Tachie)
            {
                var bitmaps = TextureCombiner.CombineTachie(psb);
                foreach (var kv in bitmaps)
                {
                    kv.Value.Save(Path.Combine(dirPath, $"{kv.Key}{texExt}"), texFormat);
                }
                return;
            }

            var texs = PsbResHelper.UnlinkImages(psb);
            
            foreach (var tex in texs)
            {
                tex.Save(Path.Combine(dirPath, tex.Tag + texExt), texFormat);
            }
        }
    }
}