using FreeMote.Plugins;
using FreeMote.Psb;
using FreeMote.Psb.Textures;
using FreeMote.Psb.Types;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
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
            psb.Merge();
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

            psb.Merge();
            return Decompile(psb);
        }

        public static string Decompile(PSB psb)
        {
            if (JsonArrayCollapse)
            {
                return ArrayCollapseJsonTextWriter.SerializeObject(psb.Root,
                    new PsbJsonConverter(JsonUseDoubleOnly, JsonUseHexNumber, PsbObjectOrderByKey));
            }

            return JsonConvert.SerializeObject(psb.Root, Formatting.Indented,
                new PsbJsonConverter(JsonUseDoubleOnly, JsonUseHexNumber, PsbObjectOrderByKey));
        }

        public static void OutputResources(PSB psb, FreeMountContext context, string filePath,
            PsbExtractOption extractOption = PsbExtractOption.Original,
            PsbImageFormat extractFormat = PsbImageFormat.png, bool useResx = true)
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            var dirPath = Path.Combine(Path.GetDirectoryName(filePath), name);
            PsbResourceJson resx = new PsbResourceJson(psb, context.Context);
            if (context.TryGet(Consts.Context_UseWebP, out bool useWebP))
            {
                resx.UseWebP = useWebP;
            }

            if (Encoding != null && !Equals(Encoding, Encoding.UTF8))
            {
                resx.Encoding = Encoding.CodePage;
            }

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
                resx.Context = context.Context
                    .Where(kv => kv.Key != Consts.Context_UseWebP && kv.Key != Consts.Context_ForceWebP)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
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
        /// <param name="outputFolder"></param>
        /// <returns>The unlinked PSB path</returns>
        public static string UnlinkToFile(string inputPath, bool outputUnlinkedPsb = true, PsbLinkOrderBy order = PsbLinkOrderBy.Name,
            PsbImageFormat format = PsbImageFormat.png, string outputFolder = null)
        {
            if (!File.Exists(inputPath))
            {
                return null;
            }

            var name = Path.GetFileNameWithoutExtension(inputPath);
            var outputDir = Path.GetDirectoryName(inputPath);
            if (!string.IsNullOrEmpty(outputFolder))
            {
                outputDir = Path.GetFullPath(outputFolder);
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }
            }

            var dirPath = Path.Combine(outputDir ?? "", name);
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
            //if (psb.TypeHandler is BaseImageType imageType)
            //{
            //    imageType.UnlinkToFile(psb, context, name, dirPath, outputUnlinkedPsb, order);
            //}

            psb.TypeHandler.UnlinkToFile(psb, context, name, dirPath, outputUnlinkedPsb, order);

            if (outputUnlinkedPsb)
            {
                psb.Merge();
                var outputName = Path.GetFileName(Path.ChangeExtension(inputPath,
                    ".unlinked.psb")); //unlink only works with motion.psb so no need for ext rename
                psbSavePath = Path.Combine(outputDir ?? "", outputName);
                File.WriteAllBytes(psbSavePath, psb.Build());
            }

            return psbSavePath;
        }

        /// <summary>
        /// Save (most user friendly) images
        /// </summary>
        /// <param name="inputPath"></param>
        /// <param name="format"></param>
        /// <param name="outputFolder"></param>
        public static void ExtractImageFiles(string inputPath, PsbImageFormat format = PsbImageFormat.png, string outputFolder = null)
        {
            if (!File.Exists(inputPath))
            {
                return;
            }

            var name = Path.GetFileNameWithoutExtension(inputPath);
            var outputDir = Path.GetDirectoryName(inputPath);
            if (!string.IsNullOrEmpty(outputFolder))
            {
                outputDir = Path.GetFullPath(outputFolder);
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }
            }

            var outputBasePath = Path.Combine(outputDir ?? "", Path.GetFileName(inputPath));
            var dirPath = Path.Combine(outputDir ?? "", name);

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
                OutputResources(psb, FreeMount.CreateContext(), outputBasePath, PsbExtractOption.Extract);
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
        /// <param name="outputFolder"></param>
        public static void ExtractArchive(string filePath, string key, Dictionary<string, object> context, string bodyPath = null,
            bool outputRaw = true, bool extractAll = false, bool enableParallel = true, string outputFolder = null)
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

            if (!File.Exists(filePath))
            {
                Logger.LogError($"Cannot find input file: {filePath}");
                return;
            }

            var fileName = Path.GetFileName(filePath);
            var archiveMdfKey = key + fileName;
            context[Context_FileName] = fileName;
            context[Context_MdfKey] = archiveMdfKey;

            var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
            var outputDir = dir;
            if (!string.IsNullOrEmpty(outputFolder))
            {
                outputDir = Path.GetFullPath(outputFolder);
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }
            }

            var outputBasePath = Path.Combine(outputDir ?? "", fileName);
            var name = PsbExtension.ArchiveInfo_GetPackageNameFromInfoPsb(fileName);
            if (string.IsNullOrEmpty(name))
            {
                Logger.LogWarn($"File name incorrect: {fileName}");
                name = fileName;
            }

            bool hasBody = false;
            string body = null;
            string bodyBinName = null;
            if (!string.IsNullOrEmpty(bodyPath))
            {
                if (!File.Exists(bodyPath))
                {
                    if (!Path.IsPathRooted(bodyPath) && Path.IsPathRooted(filePath))
                    {
                        var bodyFullPath = Path.Combine(Path.GetDirectoryName(filePath), bodyPath);
                        if (File.Exists(bodyFullPath))
                        {
                            body = bodyFullPath;
                            hasBody = true;
                            bodyBinName = Path.GetFileName(bodyPath);
                            Logger.Log($"Body FilePath: {bodyFullPath}");
                        }
                    }
                }
                else
                {
                    body = bodyPath;
                    hasBody = true;
                    bodyBinName = Path.GetFileName(bodyPath);
                    Logger.Log($"Body FilePath: {bodyPath}");
                }

                if (!hasBody)
                {
                    Logger.LogWarn($"Can not find body from specified path: {bodyPath}");
                }
            }
            else
            {
                string[] possibleBodyNames =
                [
                    $"{name}_body.bin",
                    $"{name}body.bin",
                    $"{name}.bin"
                ];

                for (var i = 0; i < possibleBodyNames.Length; i++)
                {
                    var possibleBodyName = possibleBodyNames[i];
                    body = Path.Combine(dir ?? "", possibleBodyName);
                    if (File.Exists(body))
                    {
                        hasBody = true;
                        bodyBinName = possibleBodyName;
                        break;
                    }
                }

                if (hasBody)
                {
                    Logger.Log($"Assume Body FilePath: {body}");
                }
                else
                {
                    Logger.LogWarn($"Can not find body (use `-b` to set body.bin path manually): {body} ");
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

                File.WriteAllText(outputBasePath + ".json", Decompile(psb));
                PsbResourceJson resx = new PsbResourceJson(psb, context);
                if (!hasBody)
                {
                    //Write resx.json
                    //resx.Context[Context_ArchiveSource] = new List<string> {name};
                    //File.WriteAllText(Path.GetFullPath(filePath) + ".resx.json", resx.SerializeToJson());
                    context[Context_ArchiveSource] = new List<string> { name };
                    OutputResources(psb, FreeMount.CreateContext(context), outputBasePath, PsbExtractOption.Extract);
                    return; //comment this if you need to debug without body
                }

                var archiveInfoType = psb.GetArchiveInfoType();
                if (archiveInfoType == PsbArchiveInfoType.None)
                {
                    Logger.LogWarn("Unknown ArchiveInfo type. Body won't be extracted.");
                    return;
                }

                //Maybe PSB is not identified as ArchiveInfo, but since we have tested it with GetArchiveInfoType,
                //we just set it here.
                resx.PsbType = PsbType.ArchiveInfo;
                var rootKey = archiveInfoType.GetRootKey();
                var dic = psb.Objects[rootKey] as PsbDictionary;
                if (dic == null)
                {
                    Logger.LogWarn($"Archive root object ({rootKey}) is null!");
                    dic = new PsbDictionary();
                }

                var suffixList = (PsbList) psb.Objects["expire_suffix_list"];
                Logger.Log($"Extracting {dic.Count} files from {fileName} ...");

                var extractDir = Path.Combine(outputDir, name);
                if (File.Exists(extractDir)) //conflict with File, not Directory
                {
                    name += "-resources";
                    extractDir += "-resources";
                }

                if (!Directory.Exists(extractDir))
                {
                    Directory.CreateDirectory(extractDir);
                }

                var archiveItemFileNames = new ConcurrentDictionary<string, string>();
                var failures = new ConcurrentDictionary<string, object>();
                var failureDir = outputBasePath + ".failed";
                var errorPath = outputBasePath + ".errors.json";
                var extractedCount = 0;
                var fileLength = new FileInfo(body).Length;
                var suffixes = suffixList.OfType<PsbString>().Select(s => s.Value).DefaultIfEmpty("").ToArray();
                using var mmFile = MemoryMappedFile.CreateFromFile(body, FileMode.Open, name, 0, MemoryMappedFileAccess.Read);

                void RecordFailure(string entry, long start, int len, string shellType,
                    List<string> candidates, string reason, byte[] rawBytes = null)
                {
                    string savedPath = null;
                    if (rawBytes != null)
                    {
                        savedPath = Path.Combine(failureDir, entry + ".bin");
                        EnsureDirectory(savedPath);
                        using var rawFile = File.Create(savedPath);
                        rawFile.Write(rawBytes, 0, len);
                    }
                    failures[entry] = new
                    {
                        Entry = entry, Offset = start, Length = len, ShellType = shellType,
                        TriedFileNames = candidates, Error = reason, RawFile = savedPath
                    };
                    Logger.LogError($"Skipping {entry}: {reason}");
                }

                void ExtractItem(KeyValuePair<string, IPsbValue> pair)
                {
                    var (start, len) = PsbExtension.ArchiveInfo_GetItemPositionFromRangeList((PsbList) pair.Value, archiveInfoType);
                    if (start < 0 || len <= 0 || start > fileLength || len > fileLength - start)
                    {
                        RecordFailure(pair.Key, start, len, null, null, "Entry is outside the body.bin range.");
                        return;
                    }
                    var bodyBytes = outputRaw ? new byte[len] : ArrayPool<byte>.Shared.Rent(len);
                    byte[] decompressed = null;
                    MemoryStream decoded = null;
                    try
                    {
                        using var accessor = mmFile.CreateViewAccessor(start, len, MemoryMappedFileAccess.Read);
                        accessor.ReadArray(0, bodyBytes, 0, len);
                        using var bodyStream = new MemoryStream(bodyBytes, 0, len, false);
                        if (outputRaw)
                        {
                            WriteAllBytes(Path.Combine(extractDir, pair.Key), bodyStream);
                            System.Threading.Interlocked.Increment(ref extractedCount);
                            return;
                        }

                        MPack.IsSignatureMPack(bodyStream, out var shellType);
                        var possibleFileNames = suffixes.SelectMany(s =>
                            PsbExtension.ArchiveInfo_GetAllPossibleFileNames(pair.Key, s)).Distinct().ToList();
                        var relativePath = pair.Key;
                        var finalContext = CreateArchiveItemContext(context, Path.GetFileName(pair.Key));
                        if (!string.IsNullOrEmpty(shellType))
                        {
                            string lastError = "No filename candidates.";
                            foreach (var possibleFileName in possibleFileNames)
                            {
                                var bodyContext = new Dictionary<string, object>(finalContext)
                                {
                                    [Context_MdfKey] = key + possibleFileName,
                                    [Context_FileName] = possibleFileName,
                                    [Context_PsbShellType] = shellType
                                };
                                bodyStream.Position = 0;
                                try
                                {
                                    if (shellType == MdfShell.ShellName && Consts.FastMode)
                                    {
                                        var expectedLength = bodyStream.MdfGetOriginalLength();
                                        if (expectedLength < 0)
                                        {
                                            throw new InvalidDataException("Invalid MDF decompressed length.");
                                        }
                                        decompressed ??= ArrayPool<byte>.Shared.Rent(expectedLength);
                                        MdfShell.ToPsb(bodyStream, decompressed, bodyContext);
                                        decoded = new MemoryStream(decompressed, 0, expectedLength, false);
                                    }
                                    else
                                    {
                                        decoded = PsbExtension.MdfConvert(bodyStream, shellType, bodyContext);
                                    }
                                    if (decoded == null)
                                    {
                                        lastError = "Shell decoder returned no data.";
                                        continue;
                                    }
                                }
                                catch (InvalidDataException e)
                                {
                                    lastError = e.Message;
                                    decoded?.Dispose();
                                    decoded = null;
                                    continue;
                                }

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
                            if (decoded == null)
                            {
                                RecordFailure(pair.Key, start, len, shellType, possibleFileNames,
                                    "All filename candidates failed validation. " + lastError, bodyBytes);
                                return;
                            }
                        }

                        var content = decoded ?? bodyStream;
                        var finalPath = Path.Combine(extractDir, relativePath);
                        if (extractAll && PsbFile.IsSignaturePsb(content))
                        {
                            try
                            {
                                var bodyPsb = new PSB(content);
                                DecompileToFile(bodyPsb, finalPath + ".json", finalContext, PsbExtractOption.Extract);
                            }
                            catch (Exception e)
                            {
                                WriteAllBytes(finalPath, content);
                                RecordFailure(pair.Key, start, len, shellType, possibleFileNames,
                                    "PSB decompilation failed: " + e.Message);
                                return;
                            }
                        }
                        else
                        {
                            WriteAllBytes(finalPath, content);
                        }
                        System.Threading.Interlocked.Increment(ref extractedCount);
                    }
                    finally
                    {
                        decoded?.Dispose();
                        if (!outputRaw)
                        {
                            ArrayPool<byte>.Shared.Return(bodyBytes);
                        }
                        if (decompressed != null)
                        {
                            ArrayPool<byte>.Shared.Return(decompressed);
                        }
                    }
                }

                if (enableParallel)
                {
                    Parallel.ForEach(dic.OrderByDescending(kv =>
                        PsbExtension.ArchiveInfo_GetLengthFromRangeList((PsbList) kv.Value, archiveInfoType)),
                        new ParallelOptions { MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount / 2, 2) }, ExtractItem);
                }
                else
                {
                    foreach (var pair in dic)
                    {
                        ExtractItem(pair);
                    }
                }
                var specialItemFileNames = archiveItemFileNames.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value).ToList();
                Logger.Log($"{extractedCount}/{dic.Count} files extracted; {failures.Count} failed.");
                if (!failures.IsEmpty)
                {
                    File.WriteAllText(errorPath, JsonConvert.SerializeObject(
                        failures.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value), Formatting.Indented));
                    Logger.LogError($"Archive extraction incomplete. Failure details: {errorPath}. Check the key, key length, filenames and matching body.bin.");
                }
                //Write resx.json
                resx.Context[Context_ArchiveSource] = new List<string> { name };
                resx.Context[Context_MdfMtKey] = key;
                resx.Context[Context_MdfKey] = archiveMdfKey;
                resx.Context[Context_ArchiveItemFileNames] = specialItemFileNames;
                resx.Context[Context_FileName] = fileName;
                if (!string.IsNullOrWhiteSpace(bodyBinName))
                {
                    resx.Context[Context_BodyBinName] = bodyBinName;
                }

                File.WriteAllText(outputBasePath + ".resx.json", resx.SerializeToJson());
                context[Context_ArchiveExtractionErrorCount] = failures.Count;
            }
            catch (Exception e)
            {
                context[Context_ArchiveExtractionErrorCount] = 1;
                Logger.LogError(e);
#if DEBUG
                throw e;
#endif
            }
        }

        private static Dictionary<string, object> CreateArchiveItemContext(Dictionary<string, object> context, string fileName)
        {
            var result = new Dictionary<string, object>(context);
            // Shell and seed describe the child input, not its enclosing archive index.
            foreach (var field in new[] {Context_ArchiveSource, Context_ArchiveItemFileNames,
                Context_BodyBinName, Context_MdfMtKey, Context_MdfKey, Context_FileName,
                Context_PsbZlibFastCompress, Context_PsbShellCompression, Context_ArchiveExtractionErrorCount})
            {
                result.Remove(field);
            }
            result[Context_PsbShellType] = "";
            result[Context_FileName] = fileName;
            return result;
        }

        static void WriteAllBytes(string path, MemoryStream ms)
        {
            if (Directory.Exists(path))
            {
                Logger.LogError($"[ERROR] There is a folder with same name when trying to Write file.\r\n Please remove the folder if you do want to overwrite: {path}");
                return;
            }
            EnsureDirectory(path);
            using var fs = new FileStream(path, FileMode.Create);
            ms.WriteTo(fs);
        }

        //static async Task WriteAllBytesAsync(string path, MemoryStream ms)
        //{
        //    if (Directory.Exists(path))
        //    {
        //        Logger.LogError($"[ERROR] There is a folder with same name when trying to Write file.\r\n Please remove the folder if you do want to overwrite: {path}");
        //        return;
        //    }
        //    EnsureDirectory(path);
        //    using var fs = new FileStream(path, FileMode.Create);
        //    ms.Seek(0, SeekOrigin.Begin);
        //    await ms.CopyToAsync(fs);
        //}

        static void EnsureDirectory(string path)
        {
            var baseDir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(baseDir) && !Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }
        }

        /// <summary>
        /// Unpack MT19937 MDF
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="outputPath"></param>
        /// <param name="key"></param>
        /// <param name="keyLength"></param>
        /// <param name="fileKey"></param>
        /// <param name="suffix"></param>
        /// <exception cref="FormatException"></exception>
        internal static void MtUnpack(string filePath, string outputPath, string key, int keyLength, string fileKey = "", string suffix = "")
        {
            if (!File.Exists(filePath))
            {
                Logger.LogError($"Cannot find input file: {filePath}");
                return;
            }

            var bodyBytes = File.ReadAllBytes(filePath);
            MPack.IsSignatureMPack(bodyBytes, out var shellType);
            var fileName = Path.GetFileName(filePath);
            fileKey ??= "";
            var name = string.IsNullOrEmpty(fileKey) ? fileName : fileKey;
            var possibleFileNames = PsbExtension.ArchiveInfo_GetAllPossibleFileNames(name, suffix);
            //var relativePath = fileKey;
            var finalContext = new Dictionary<string, object>()
            {
                {Context_MdfKeyLength, keyLength}
            };
            finalContext.Remove(Context_ArchiveSource);

            using var ms = new MemoryStream(bodyBytes);

            if (!string.IsNullOrEmpty(shellType) && possibleFileNames.Count > 0)
            {
                foreach (var possibleFileName in possibleFileNames)
                {
                    var bodyContext = new Dictionary<string, object>(finalContext)
                    {
                        [Context_MdfKey] = key + possibleFileName,
                        [Context_FileName] = possibleFileName
                    };

                    MemoryStream mms;
                    try
                    {
                        ms.Position = 0;
                        mms = PsbExtension.MdfConvert(ms, shellType, bodyContext);
                    }
                    catch (InvalidDataException)
                    {
                        continue;
                    }

                    if (mms != null)
                    {
                        //relativePath = possibleFileName.Contains("/") ? possibleFileName :
                        //    fileKey.Contains("/") ? Path.Combine(Path.GetDirectoryName(fileKey), possibleFileName) :
                        //    possibleFileName;
                        //finalContext = bodyContext;
                        if (possibleFileName != possibleFileNames[0])
                        {
                            Logger.Log($"  detected key name: {fileKey} -> {possibleFileName}");
                        }

                        //write to file
                        using var outFs = File.Create(string.IsNullOrEmpty(outputPath)
                            ? Path.ChangeExtension(filePath, ".unpack.psb")
                            : outputPath);
                        mms.WriteTo(outFs);
                        mms.Dispose();
                        return;
                    }
                }
                throw new InvalidDataException($"All filename candidates failed validation for {fileName}: {string.Join(", ", possibleFileNames)}");
            }
            else
            {
                throw new FormatException("File is not a shell PSB.");
            }
        }
    }
}
