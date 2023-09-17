using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FreeMote.Psb;
using FreeMote.Plugins;
using FreeMote.Psb.Textures;
using FreeMote.Psb.Types;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static FreeMote.Consts;

// ReSharper disable AssignNullToNotNullAttribute

namespace FreeMote.PsBuild
{
    /// <summary>
    /// Decompile PSB(/MMO) File
    /// </summary>
    public static class PsbDecompiler
    {
        public static Encoding Encoding { get; set; } = Encoding.UTF8;

        /// <summary>
        /// Decompile Pure PSB as Json
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string Decompile(string path)
        {
            PSB psb = new PSB(path, Encoding);
            return Decompile(psb);
        }

        /// <summary>
        /// Decompile Pure PSB as Json
        /// </summary>
        /// <param name="path"></param>
        /// <param name="psb"></param>
        /// <param name="context"></param>
        /// <param name="psbType"></param>
        /// <returns></returns>
        public static string Decompile(string path, out PSB psb, Dictionary<string, object> context = null, PsbType psbType = PsbType.PSB)
        {
            using var fs = File.OpenRead(path);
            var ctx = FreeMount.CreateContext(context);
            string type = null;
            Stream stream = fs;
            using var ms = ctx.OpenFromShell(fs, ref type);
            if (ms != null)
            {
                ctx.Context[Consts.Context_PsbShellType] = type;
                fs.Dispose();
                stream = ms;
            }

            try
            {
                psb = new PSB(stream, false, Encoding);
            }
            catch (PsbBadFormatException e) when (e.Reason == PsbBadFormatReason.Header ||
                                                  e.Reason == PsbBadFormatReason.Array ||
                                                  e.Reason == PsbBadFormatReason.Body) //maybe encrypted
            {
                stream.Position = 0;
                uint? key = null;
                if (ctx.Context.TryGetValue(Consts.Context_CryptKey, out var cryptKey))
                {
                    key = cryptKey as uint?;
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
                        using var mms = new MemoryStream((int) stream.Length);
                        PsbFile.Encode(key.Value, EncodeMode.Decrypt, EncodePosition.Auto, stream, mms);
                        stream.Dispose();
                        psb = new PSB(mms, true, Encoding);
                        ctx.Context[Consts.Context_CryptKey] = key;
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

            if (psbType != PsbType.PSB)
            {
                psb.Type = psbType;
            }

            return Decompile(psb);
        }

        public static string Decompile(PSB psb)
        {
            if (Consts.JsonArrayCollapse)
            {
                return ArrayCollapseJsonTextWriter.SerializeObject(psb.Root,
                    new PsbJsonConverter(Consts.JsonUseDoubleOnly, Consts.JsonUseHexNumber));
            }

            return JsonConvert.SerializeObject(psb.Root, Formatting.Indented,
                new PsbJsonConverter(Consts.JsonUseDoubleOnly, Consts.JsonUseHexNumber));
        }

        public static void OutputResources(PSB psb, FreeMountContext context, string filePath,
            PsbExtractOption extractOption = PsbExtractOption.Original,
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

                File.WriteAllText(ChangeExtensionForOutputJson(filePath, ".resx.json"), json);
            }
            else
            {
                if (psb.ExtraResources.Count > 0)
                {
                    throw new NotSupportedException("PSBv4 cannot use legacy res.json format.");
                }

                File.WriteAllText(ChangeExtensionForOutputJson(filePath, ".res.json"),
                    JsonConvert.SerializeObject(resDictionary.Values.ToList(), Formatting.Indented));
            }
        }

        private static string ChangeExtensionForOutputJson(string inputPath, string extension = ".json")
        {
            if (!extension.StartsWith("."))
            {
                extension = "." + extension;
            }

            if (inputPath.EndsWith(".m")) //special handle for .m
            {
                return inputPath + extension;
            }

            return Path.ChangeExtension(inputPath, extension);
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
        public static void DecompileToFile(PSB psb, string outputPath, Dictionary<string, object> additionalContext = null,
            PsbExtractOption extractOption = PsbExtractOption.Original,
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
        /// <param name="type">Specify PSB type, if not set, infer type automatically</param>
        /// <param name="contextDic">Context, used to set some configurations</param>
        public static (string OutputPath, PSB Psb) DecompileToFile(string inputPath,
            PsbExtractOption extractOption = PsbExtractOption.Original,
            PsbImageFormat extractFormat = PsbImageFormat.png, bool useResx = true, uint? key = null, PsbType type = PsbType.PSB,
            Dictionary<string, object> contextDic = null)
        {
            var context = FreeMount.CreateContext(contextDic);
            if (key != null)
            {
                context.Context[Consts.Context_CryptKey] = key;
            }

            var outputPath = ChangeExtensionForOutputJson(inputPath, ".json");
            File.WriteAllText(outputPath, Decompile(inputPath, out var psb, context.Context));

            if (type != PsbType.PSB)
            {
                psb.Type = type;
            }

            OutputResources(psb, context, inputPath, extractOption, extractFormat, useResx);

            return (outputPath, psb);
        }

        /// <summary>
        /// Convert a PSB to External Texture PSB.
        /// </summary>
        /// <param name="inputPath"></param>
        /// <param name="outputUnlinkedPsb">output unlinked PSB; otherwise only output textures</param>
        /// <param name="order"></param>
        /// <param name="format"></param>
        /// <returns>The unlinked PSB path</returns>
        public static string UnlinkToFile(string inputPath, bool outputUnlinkedPsb = true, PsbLinkOrderBy order = PsbLinkOrderBy.Name,
            PsbImageFormat format = PsbImageFormat.png)
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
            var psb = new PSB(inputPath, Encoding);
            if (psb.TypeHandler is BaseImageType imageType)
            {
                imageType.UnlinkToFile(psb, context, name, dirPath, outputUnlinkedPsb, order);
            }

            psb.TypeHandler.UnlinkToFile(psb, context, name, dirPath, outputUnlinkedPsb, order);

            if (outputUnlinkedPsb)
            {
                psb.Merge();
                psbSavePath = Path.ChangeExtension(inputPath,
                    ".unlinked.psb"); //unlink only works with motion.psb so no need for ext rename
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

            var psb = new PSB(inputPath, Encoding);
            if (psb.Type == PsbType.Tachie)
            {
                var bitmaps = TextureCombiner.CombineTachie(psb, out var hasPalette);
                foreach (var kv in bitmaps)
                {
                    kv.Value.CombinedImage.Save(Path.Combine(dirPath, $"{kv.Key}{texExt}"), texFormat);
                }

                return;
            }
            else if (psb.Type == PsbType.Pimg)
            {
                OutputResources(psb, FreeMount.CreateContext(), inputPath, PsbExtractOption.Extract);
                return;
            }

            var texs = PsbResHelper.UnlinkImages(psb);

            foreach (var tex in texs)
            {
                tex.Save(Path.Combine(dirPath, tex.Tag + texExt), texFormat);
            }
        }

        /// <summary>
        /// Extract files from info.psb.m and body.bin
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="key"></param>
        /// <param name="context"></param>
        /// <param name="bodyPath"></param>
        /// <param name="outputRaw">no mdf unzip, no decompile</param>
        /// <param name="extractAll">mdf unzip + decompile</param>
        /// <param name="enableParallel"></param>
        public static void ExtractArchive(string filePath, string key, Dictionary<string, object> context, string bodyPath = null,
            bool outputRaw = true, bool extractAll = false, bool enableParallel = true)
        {
            if (filePath.ToLowerInvariant().EndsWith(".bin"))
            {
                Logger.LogWarn(
                    "[WARN] It seems that you are trying to extract from a body.bin file. You should extract body.bin by extracting info.psb.m file with `info-psb` command instead.");
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), $"Context cannot be null, since {Context_MdfKeyLength} has to be set.");
            }

            if (File.Exists(filePath))
            {
                var fileName = Path.GetFileName(filePath);
                var archiveMdfKey = key + fileName;
                context[Context_FileName] = fileName;
                context[Context_MdfKey] = archiveMdfKey;

                var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
                var name = PsbExtension.ArchiveInfoGetPackageName(fileName);
                if (name == null)
                {
                    Logger.LogWarn($"File name incorrect: {fileName}");
                    name = fileName;
                }

                bool hasBody = false;
                string body = null;
                if (!string.IsNullOrEmpty(bodyPath))
                {
                    if (!File.Exists(bodyPath))
                    {
                        Logger.LogWarn($"Can not find body from specified path: {bodyPath}");
                    }
                    else
                    {
                        body = bodyPath;
                        hasBody = true;
                    }
                }
                else
                {
                    body = Path.Combine(dir ?? "", name + "_body.bin");

                    if (!File.Exists(body))
                    {
                        Logger.LogWarn($"Can not find body (use `-b` to set body.bin path manually): {body} ");
                    }
                    else
                    {
                        hasBody = true;
                    }
                }

                try
                {
                    PSB psb = null;

                    using (var fs = File.OpenRead(filePath))
                    {
                        var shellType = PsbFile.GetSignatureShellType(fs);
                        if (shellType != "PSB")
                        {
                            try
                            {
                                using var unpacked = PsbExtension.MdfConvert(fs, shellType, context);
                                psb = new PSB(unpacked);
                            }
                            catch (InvalidDataException)
                            {
                                string realName = fileName;
                                Regex RealNameRegex = new Regex(@"[A-Za-z0-9_-]+\.[A-Za-z0-9]+\.m");
                                if (RealNameRegex.IsMatch(fileName))
                                {
                                    realName = RealNameRegex.Match(fileName).Value;
                                }

                                if (realName != fileName)
                                {
                                    Logger.Log($"Trying file name: {realName}");
                                    archiveMdfKey = key + realName;
                                    context[Context_FileName] = realName;
                                    context[Context_MdfKey] = archiveMdfKey;
                                    fs.Seek(0, SeekOrigin.Begin);
                                    using var unpacked = PsbExtension.MdfConvert(fs, shellType, context);
                                    psb = new PSB(unpacked);
                                }
                                else
                                {
                                    throw;
                                }
                            }
                        }
                        else
                        {
                            psb = new PSB(fs);
                        }
                    }

                    File.WriteAllText(Path.GetFullPath(filePath) + ".json", Decompile(psb));
                    PsbResourceJson resx = new PsbResourceJson(psb, context);
                    if (!hasBody)
                    {
                        //Write resx.json
                        //resx.Context[Context_ArchiveSource] = new List<string> {name};
                        //File.WriteAllText(Path.GetFullPath(filePath) + ".resx.json", resx.SerializeToJson());
                        context[Context_ArchiveSource] = new List<string> {name};
                        OutputResources(psb, FreeMount.CreateContext(context), Path.GetFullPath(filePath), PsbExtractOption.Extract);
                        return;
                    }

                    PsbArchiveInfoType archiveInfoType = psb.GetArchiveInfoType();
                    if (archiveInfoType == PsbArchiveInfoType.None)
                    {
                        return;
                    }

                    //Maybe PSB is not identified as ArchiveInfo, but since we have tested it with GetArchiveInfoType,
                    //we just set it here.
                    resx.PsbType = PsbType.ArchiveInfo;
                    var dic = psb.Objects[archiveInfoType.GetRootKey()] as PsbDictionary;
                    var suffixList = (PsbList) psb.Objects["expire_suffix_list"];
                    var suffix = "";
                    if (suffixList.Count > 0)
                    {
                        suffix = suffixList[0] as PsbString ?? "";
                    }

                    Logger.Log($"Extracting info from {fileName} ...");

                    var extractDir = Path.Combine(dir, name);
                    if (File.Exists(extractDir)) //conflict with File, not Directory
                    {
                        name += "-resources";
                        extractDir += "-resources";
                    }

                    if (!Directory.Exists(extractDir))
                    {
                        Directory.CreateDirectory(extractDir);
                    }

                    List<string> specialItemFileNames = new List<string>();
                    if (enableParallel) //parallel!
                    {
                        var archiveItemFileNames = new ConcurrentDictionary<string, string>();
                        var fileLength = new FileInfo(body).Length;
                        using var mmFile =
                            MemoryMappedFile.CreateFromFile(body, FileMode.Open, name, 0, MemoryMappedFileAccess.Read);
                        Parallel.ForEach(dic, pair =>
                        {
                            //Console.WriteLine($"{(extractAll ? "Decompiling" : "Extracting")} {pair.Key} ...");
                            var range = (PsbList) pair.Value;
                            var (start, len) = PsbExtension.ArchiveInfoGetItemPositionFromRangeList(range, archiveInfoType);

                            if (start + len > fileLength)
                            {
                                Logger.LogError(
                                    $"{pair.Key} (start:{start}, len:{len}) is beyond the body.bin's range. Check your body.bin file. Skipping...");
                                return;
                            }

                            using var mmAccessor = mmFile.CreateViewAccessor(start, len, MemoryMappedFileAccess.Read);
                            var bodyBytes = new byte[len];
                            mmAccessor.ReadArray(0, bodyBytes, 0, len);

                            var rawPath = Path.Combine(extractDir, pair.Key);
                            EnsureDirectory(rawPath);
                            if (outputRaw)
                            {
                                File.WriteAllBytes(rawPath, bodyBytes);
                                return;
                            }

                            MPack.IsSignatureMPack(bodyBytes, out var shellType);
                            //var shellType = MdfFile.IsSignatureMdf(bodyBytes) ? "MDF" : "";
                            var possibleFileNames = PsbExtension.ArchiveInfoGetAllPossibleFileNames(pair.Key, suffix);
                            var relativePath = pair.Key;
                            var finalContext = new Dictionary<string, object>(context);
                            finalContext.Remove(Context_ArchiveSource);

                            var ms = MsManager.GetStream(bodyBytes);
                            MemoryStream mms = null;

                            if (!string.IsNullOrEmpty(shellType) && possibleFileNames.Count > 0)
                            {
                                foreach (var possibleFileName in possibleFileNames)
                                {
                                    var bodyContext = new Dictionary<string, object>(finalContext)
                                    {
                                        [Context_MdfKey] = key + possibleFileName,
                                        [Context_FileName] = possibleFileName
                                    };

                                    try
                                    {
                                        mms = PsbExtension.MdfConvert(ms, shellType, bodyContext);
                                    }
                                    catch (InvalidDataException e)
                                    {
                                        ms.Dispose();
                                        ms = MsManager.GetStream(bodyBytes);
                                        mms = null;
                                    }

                                    if (mms != null)
                                    {
                                        relativePath = possibleFileName.Contains("/") ? possibleFileName :
                                            pair.Key.Contains("/") ? Path.Combine(Path.GetDirectoryName(pair.Key), possibleFileName) :
                                            possibleFileName;
                                        finalContext = bodyContext;
                                        if (possibleFileName != possibleFileNames[0])
                                        {
                                            archiveItemFileNames[pair.Key] = possibleFileName;
                                        }

                                        break;
                                    }
                                }
                            }

                            var finalPath = Path.Combine(extractDir, relativePath);
                            if (mms == null)
                            {
                                mms = ms;
                            }
                            else
                            {
                                ms?.Dispose();
                            }

                            if (extractAll && PsbFile.IsSignaturePsb(mms))
                            {
                                try
                                {
                                    PSB bodyPsb = new PSB(mms);
                                    DecompileToFile(bodyPsb,
                                        Path.Combine(extractDir, relativePath + ".json"), //important, must keep suffix for rebuild
                                        finalContext, PsbExtractOption.Extract);
                                }
                                catch (Exception e)
                                {
#if DEBUG
                                    Debug.WriteLine(e);
#endif
                                    Logger.LogError($"Decompile failed: {pair.Key}");
                                    WriteAllBytes(finalPath, mms);
                                    //File.WriteAllBytes(Path.Combine(extractDir, pair.Key + suffix), mms.ToArray());
                                }
                            }
                            else
                            {
                                WriteAllBytes(finalPath, mms);
                                //File.WriteAllBytes(Path.Combine(extractDir, pair.Key + suffix), mms.ToArray());
                            }

                            try
                            {
                                mms?.Dispose();
                            }
                            catch (Exception e)
                            {
                                // ignored
#if DEBUG
                                Debug.WriteLine(e);
#endif
                            }
                        });

                        specialItemFileNames.AddRange(archiveItemFileNames.Values);
                        Logger.Log($"{dic.Count} files extracted.");
                    }
                    else
                    {
                        //no parallel
                        //var maxLen = dic?.Values.Max(item => item.Children(1).GetInt()) ?? 0;
                        var archiveItemFileNames = new Dictionary<string, string>();
                        using var mmFile =
                            MemoryMappedFile.CreateFromFile(body, FileMode.Open, name, 0, MemoryMappedFileAccess.Read);

                        foreach (var pair in dic)
                        {
                            Logger.Log($"{(extractAll ? "Extracting" : "Unpacking")} {pair.Key} ...");
                            var range = ((PsbList) pair.Value);
                            var (start, len) = PsbExtension.ArchiveInfoGetItemPositionFromRangeList(range, archiveInfoType);

                            using var mmAccessor = mmFile.CreateViewAccessor(start, len, MemoryMappedFileAccess.Read);
                            var bodyBytes = new byte[len];
                            mmAccessor.ReadArray(0, bodyBytes, 0, len);

                            var rawPath = Path.Combine(extractDir, pair.Key);
                            EnsureDirectory(rawPath);
                            if (outputRaw)
                            {
                                File.WriteAllBytes(rawPath, bodyBytes);
                                continue;
                            }

                            MPack.IsSignatureMPack(bodyBytes, out var shellType);
                            var possibleFileNames = PsbExtension.ArchiveInfoGetAllPossibleFileNames(pair.Key, suffix);
                            var relativePath = pair.Key;
                            var finalContext = new Dictionary<string, object>(context);
                            finalContext.Remove(Context_ArchiveSource);

                            var ms = MsManager.GetStream(bodyBytes);
                            MemoryStream mms = null;

                            if (!string.IsNullOrEmpty(shellType) && possibleFileNames.Count > 0)
                            {
                                foreach (var possibleFileName in possibleFileNames)
                                {
                                    var bodyContext = new Dictionary<string, object>(finalContext)
                                    {
                                        [Context_MdfKey] = key + possibleFileName,
                                        [Context_FileName] = possibleFileName
                                    };

                                    try
                                    {
                                        mms = PsbExtension.MdfConvert(ms, shellType, bodyContext);
                                    }
                                    catch (InvalidDataException)
                                    {
                                        ms = MsManager.GetStream(bodyBytes);
                                        mms = null;
                                    }

                                    if (mms != null)
                                    {
                                        relativePath = possibleFileName.Contains("/") ? possibleFileName :
                                            pair.Key.Contains("/") ? Path.Combine(Path.GetDirectoryName(pair.Key), possibleFileName) :
                                            possibleFileName; finalContext = bodyContext;
                                        if (possibleFileName != possibleFileNames[0])
                                        {
                                            Logger.Log($"  detected key name: {pair.Key} -> {possibleFileName}");
                                            archiveItemFileNames[pair.Key] = possibleFileName;
                                        }

                                        break;
                                    }
                                }
                            }

                            var finalPath = Path.Combine(extractDir, relativePath);
                            mms ??= ms;

                            if (extractAll && PsbFile.IsSignaturePsb(mms))
                            {
                                try
                                {
                                    PSB bodyPsb = new PSB(mms);
                                    DecompileToFile(bodyPsb,
                                        Path.Combine(extractDir, relativePath + ".json"), //important, must keep suffix for rebuild
                                        finalContext, PsbExtractOption.Extract);
                                }
                                catch (Exception e)
                                {
#if DEBUG
                                    Debug.WriteLine(e);
#endif
                                    Logger.LogError($"Decompile failed: {pair.Key}");
                                    WriteAllBytes(finalPath, mms);
                                    //File.WriteAllBytes(Path.Combine(extractDir, pair.Key + suffix), mms.ToArray());
                                }
                            }
                            else
                            {
                                WriteAllBytes(finalPath, mms);
                                //File.WriteAllBytes(Path.Combine(extractDir, pair.Key + suffix), mms.ToArray());
                            }
                        }

                        specialItemFileNames.AddRange(archiveItemFileNames.Values);
                    }

                    //Write resx.json
                    resx.Context[Context_ArchiveSource] = new List<string> {name};
                    resx.Context[Context_MdfMtKey] = key;
                    resx.Context[Context_MdfKey] = archiveMdfKey;
                    resx.Context[Context_ArchiveItemFileNames] = specialItemFileNames;
                    resx.Context[Context_FileName] = fileName;
                    File.WriteAllText(Path.GetFullPath(filePath) + ".resx.json", resx.SerializeToJson());
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
#if DEBUG
                    throw e;
#endif
                }
            }
        }

        static void WriteAllBytes(string path, MemoryStream ms)
        {
            EnsureDirectory(path);
            using var fs = new FileStream(path, FileMode.Create);
            ms.WriteTo(fs);
        }

        static void EnsureDirectory(string path)
        {
            var baseDir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(baseDir) && !Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }
        }
    }
}