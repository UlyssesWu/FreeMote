using System;
using System.Collections.Concurrent;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreeMote.Plugins;
using FreeMote.Psb;
using Newtonsoft.Json;
using static FreeMote.Consts;

namespace FreeMote.PsBuild
{
    /// <summary>
    /// Compile PSB File
    /// </summary>
    public static class PsbCompiler
    {
        public static Encoding Encoding { get; set; } = Encoding.UTF8;

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
        /// <returns>The actual output path</returns>
        public static string CompileToFile(string inputPath, string outputPath, string inputResPath = null,
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

                        if (resx.Context != null && keepShell)
                        {
                            if (resx.Context.TryGetValue(Consts.Context_FileName, out var fn))
                            {
                                if (fn is string targetName)
                                {
                                    var dir = Path.GetDirectoryName(outputPath) ?? string.Empty;
                                    var targetPath = Path.Combine(dir, targetName);
                                    if (!File.Exists(targetPath) || string.IsNullOrEmpty(dir)) //allow overwrite if save in current path
                                    {
                                        outputPath = targetPath;
                                        goto COMPILE;
                                    }
                                }
                            }

                            if (resx.Context.TryGetValue(Consts.Context_PsbShellType, out var st))
                            {
                                var shellType = st as string;
                                if (!string.IsNullOrEmpty(shellType) && shellType.ToUpperInvariant() != "PSB")
                                {
                                    ext += $".{shellType.ToLowerInvariant()}";
                                }
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

            COMPILE: ;
            var result = Compile(File.ReadAllText(inputPath), resJson, baseDir, version, cryptKey, platform, keepShell);

            // ReSharper disable once AssignNullToNotNullAttribute
            File.WriteAllBytes(outputPath, result);

            return outputPath;
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

                    if (resx.HasExtraResources)
                    {
                        PsbResHelper.LinkExtraResources(psb, context, resx.ExtraResources, resx.ExtraFlattenArrays, baseDir);
                    }

                    if (resx.ExternalTextures)
                    {
#if DEBUG
                        Logger.Log("[INFO] External Texture mode ON, no resource will be compiled.");
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
                bytes = PsbFile.EncodeToBytes(cryptKey.Value, bytes, EncodeMode.Encrypt, EncodePosition.Auto);
            }

            if (context.HasShell && keepShell)
            {
                using var outStream = context.PackToShell(new MemoryStream(bytes));
                bytes = outStream.ToArray();
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
                        Logger.Log("[INFO] External Texture mode ON, no resource will be compiled.");
#endif
                    }
                    else
                    {
                        psb.Link(resx, baseDir);
                    }

                    if (resx.Platform != null && resx.Platform != PsbSpec.none)
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
                Encoding = Encoding
            };
            var converter = new PsbJsonConverter();
            var j = json.TrimStart();
            if (j.StartsWith("["))
            {
                psb.Root = JsonConvert.DeserializeObject<PsbList>(json, converter);
            }
            else if (j.StartsWith("\""))
            {
                psb.Root = JsonConvert.DeserializeObject<PsbString>(json, converter);
            }
            else
            {
                try
                {
                    psb.Root = JsonConvert.DeserializeObject<PsbDictionary>(json, converter);
                }
                catch (Exception e)
                {
                    throw new PsbBadFormatException(PsbBadFormatReason.Json,
                        $"Cannot parse json to PSB: {json.Substring(0, Math.Min(json.Length, 30))}", e);
                }
            }

            psb.InferType();
            psb.Collect(false, false); //don't merge res since it's empty now
            return psb;
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
            var context = FreeMount.CreateContext();

            if (psb.Type == PsbType.Motion)
            {
                PsbResHelper.LinkImages(psb, context, resPaths, baseDir, order, isExternal);
                return;
            }

            if (psb.TypeHandler != null)
            {
                psb.TypeHandler.Link(psb, context, resPaths, baseDir, order);
            }
            else
            {
                PsbResHelper.LinkImages(psb, context, resPaths, baseDir, order);
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

            var context = FreeMount.CreateContext(resx.Context);
            if (psb.TypeHandler != null)
            {
                psb.TypeHandler.Link(psb, context, resx.Resources, baseDir);
            }
            else
            {
                PsbResHelper.LinkImages(psb, context, resx.Resources, baseDir);
            }
        }


        /// <summary>
        /// Modify the original PSB and only replace resources (according to json)
        /// </summary>
        /// <param name="psbPath">PSB to be modified</param>
        /// <param name="jsonPath">PSB Json which only resources are changed</param>
        /// <returns></returns>
        public static MemoryStream InplaceReplace(string psbPath, string jsonPath)
        {
            var jsonPsb = LoadPsbFromJsonFile(jsonPath);
            using var psbFs = File.OpenRead(psbPath);

            var ctx = FreeMount.CreateContext();
            using var psbStream = ctx.OpenStreamFromPsbFile(psbPath, out _);
            var psb = new PSB(psbStream, true, Encoding);

            if (jsonPsb.Resources.Count != psb.Resources.Count)
            {
                throw new NotSupportedException("The 2 PSBs are different (Resource count).");
            }

            MemoryStream ms = new MemoryStream((int)psbStream.Length);
            psbStream.Seek(0, SeekOrigin.Begin);
            psbStream.CopyTo(ms);
            using BinaryWriter bw = new BinaryWriter(ms, Encoding, true);

            for (var i = 0; i < jsonPsb.Resources.Count; i++)
            {
                var resource = jsonPsb.Resources[i];
                var oriResource = psb.Resources[i];
                if (resource.Data.Length > oriResource.Data.Length)
                {
                    throw new NotSupportedException($"The 2 PSBs are different (Resource {i} length: {resource.Data.Length} vs {oriResource.Data.Length}).");
                }

                if (oriResource.Index == null)
                {
                    Logger.LogWarn($"[WARN] Resource {i} is not replaced.");
                    continue;
                }

                var offset = psb.ChunkOffsets[(int)oriResource.Index];
                var length = psb.ChunkLengths[(int)oriResource.Index];

                bw.BaseStream.Seek(psb.Header.OffsetChunkData + offset, SeekOrigin.Begin);
                bw.Write(resource.Data);
                if (length > resource.Data.Length)
                {
                    bw.Write(new byte[length - resource.Data.Length]);
                }
            }
            
            return ms;
        }

        /// <summary>
        /// <inheritdoc cref="InplaceReplace"/>
        /// </summary>
        public static string InplaceReplaceToFile(string psbPath, string jsonPath)
        {
            var ms = InplaceReplace(psbPath, jsonPath);
            var outputPath = Path.ChangeExtension(psbPath, "IR.psb");
            using var fs = File.Create(outputPath);
            ms.WriteTo(fs);
            fs.Close();
            ms.Close();
            return outputPath;
        }

        /// <summary>
        /// Pack Archive PSB
        /// </summary>
        /// <param name="jsonPath">json path</param>
        /// <param name="key">crypt key</param>
        /// <param name="intersect">Only pack files which existed in info.psb.m</param>
        /// <param name="preferPacked">Prefer using PSB files rather than json files in source folder</param>
        /// <param name="extraContext">extra context</param>
        /// <param name="enableParallel">parallel process</param>
        /// <param name="keyLen">key length</param>
        /// <param name="keepRaw">Do not try to compile json or pack MDF</param>
        public static void PackArchive(string jsonPath, string key, bool intersect, bool preferPacked, Dictionary<string, object> extraContext = null, bool enableParallel = true,
            int keyLen = 131, bool keepRaw = false)
        {
            if (!File.Exists(jsonPath)) return;
            PSB infoPsb = LoadPsbFromJsonFile(jsonPath);
            if (infoPsb.Type != PsbType.ArchiveInfo)
            {
                Logger.LogWarn($"[WARN] The json ({infoPsb.Type}) seems not to be an ArchiveInfo type.");
            }

            var archiveInfoType = infoPsb.GetArchiveInfoType();
            var jsonName = Path.GetFileName(jsonPath);
            var packageName = Path.GetFileNameWithoutExtension(jsonName);
            var coreName = PsbExtension.ArchiveInfo_GetPackageNameFromInfoPsb(packageName);

            var resx = PsbResourceJson.LoadByPsbJsonPath(jsonPath);
            var context = resx.Context;
            FreeMountContext.Merge(extraContext, ref context);

            if (!context.ContainsKey(Context_ArchiveSource) ||
                context[Context_ArchiveSource] == null)
            {
                Logger.LogWarn($"ArchiveSource is not specified in resx.json Context. Use default: {coreName}");
                context[Context_ArchiveSource] = coreName;
            }
            var bodyBinFileName = string.IsNullOrEmpty(coreName) ? packageName + "_body.bin" : coreName + "_body.bin";
            if (context.ContainsKey(Context_BodyBinName) && context[Context_BodyBinName] is string bbName && !string.IsNullOrWhiteSpace(bbName))
            {
                bodyBinFileName = bbName;
            }
            Logger.Log($"Body FileName: {bodyBinFileName}");

            var defaultShellType = "MDF";
            if (context.ContainsKey(Context_PsbShellType) && context[Context_PsbShellType] is string st)
            {
                defaultShellType = st;
            }

            if (keyLen > 0)
            {
                context[Context_MdfKeyLength] = keyLen;
            }

            string infoKey = null;
            if (context[Context_MdfKey] is string mdfKey)
            {
                infoKey = mdfKey;
            }

            List<string> sourceDirs = null;
            if (context[Context_ArchiveSource] is string path)
            {
                sourceDirs = new List<string> { path };
            }
            else if (context[Context_ArchiveSource] is IList paths)
            {
                sourceDirs = new List<string>(paths.Count);
                sourceDirs.AddRange(from object p in paths select p.ToString());
            }
            else
            {
                Logger.LogError("ArchiveSource incorrect: must be a path string or a list of string.");
                return;
            }

            var baseDir = Path.GetDirectoryName(jsonPath);
            var files = new Dictionary<string, (string Path, ArchiveProcessMethod Method)>();
            var suffix = PsbExtension.ArchiveInfo_GetSuffix(infoPsb);
            HashSet<string> filter = null;
            if (intersect) //only collect files appeared in json
            {
                filter = PsbExtension.ArchiveInfo_CollectFiles(infoPsb, suffix).Select(p => p.Replace('\\', '/')).ToHashSet();
            }

            if (filter != null && context[Context_ArchiveItemFileNames] is IList fileNames)
            {
                foreach (var fileName in fileNames)
                {
                    filter.Add(fileName.ToString());
                }
            }

            // local functions

            void CollectFilesFromList(string targetDir, HashSet<string> infoFiles)
            {
                if (!Directory.Exists(targetDir))
                {
                    return;
                }

                foreach (var infoFile in infoFiles)
                {
                    var f = Path.Combine(targetDir, infoFile);
                    var source = Path.Combine(targetDir, infoFile + ".json");
                    if (preferPacked)
                    {
                        if (File.Exists(f)) //no need to compile
                        {
                            files[infoFile] = (f, keepRaw ? ArchiveProcessMethod.None : ArchiveProcessMethod.EncodeMPack);
                        }
                        else if (File.Exists(source))
                        {
                            files[infoFile] = (f, ArchiveProcessMethod.Compile);
                        }
                    }
                    else
                    {
                        if (File.Exists(source))
                        {
                            files[infoFile] = (f, ArchiveProcessMethod.Compile);
                        }
                        else if (File.Exists(f)) //no need to compile
                        {
                            files[infoFile] = (f, keepRaw ? ArchiveProcessMethod.None : ArchiveProcessMethod.EncodeMPack);
                        }
                    }
                }
            }

            void CollectFiles(string targetDir)
            {
                if (!Directory.Exists(targetDir))
                {
                    return;
                }

                HashSet<string> skipDirs = new HashSet<string>();
                foreach (var file in Directory.EnumerateFiles(targetDir, "*.resx.json", SearchOption.AllDirectories)) //every resx.json disables a folder
                {
                    skipDirs.Add(file.Remove(file.Length - ".resx.json".Length));
                }

                foreach (var f in Directory.EnumerateFiles(targetDir, "*", SearchOption.AllDirectories))
                {
                    if (skipDirs.Contains(Path.GetDirectoryName(f))) //this dir is a source dir for json, just skip
                    {
                        continue;
                    }

                    if (f.EndsWith(".resx.json", true, CultureInfo.InvariantCulture))
                    {
                        continue;
                    }

                    var relativePath = PathNetCore.GetRelativePath(targetDir, f).Replace('\\', '/');

                    if (f.EndsWith(".json", true, CultureInfo.InvariantCulture)) //json source, need compile
                    {
                        var name = Path.ChangeExtension(relativePath, null); //Path.GetFileNameWithoutExtension(f);
                        if (preferPacked && files.ContainsKey(name) &&
                            files[name].Method != ArchiveProcessMethod.Compile) //it's always right no matter set or replace
                        {
                            //ignore
                        }
                        else
                        {
                            if (intersect && filter != null && !filter.Contains(name)) //this file is not appeared in json
                            {
                                //ignore
                            }
                            else
                            {
                                files[name] = (f, keepRaw ? ArchiveProcessMethod.None : ArchiveProcessMethod.Compile);
                            }
                        }
                    }
                    else
                    {
                        var name = relativePath;
                        if (!preferPacked && files.ContainsKey(name) &&
                            files[name].Method == ArchiveProcessMethod.Compile)
                        {
                            //ignore
                        }
                        else
                        {
                            if (intersect && filter != null && !filter.Contains(name))
                            {
                                //ignore
                            }
                            else
                            {
                                using var fs = File.OpenRead(f);
                                if (!MPack.IsSignatureMPack(fs, out _) && name.DefaultShellType() == "MDF")
                                {
                                    files[name] = (f, keepRaw ? ArchiveProcessMethod.None : ArchiveProcessMethod.EncodeMPack);
                                }
                                else
                                {
                                    files[name] = (f, ArchiveProcessMethod.None);
                                }
                            }
                        }
                    }
                }
            }

            void AddFileInfo(PsbDictionary fileInfoDic, string relativePathWithoutSuffix, long bodyPos, long size)
            {
                if (archiveInfoType == PsbArchiveInfoType.UmdRoot)
                {
                    fileInfoDic.Add(relativePathWithoutSuffix, new PsbList
                        {PsbNull.Null, new PsbNumber(size), new PsbNumber(bodyPos)}); //We still have no idea about the first parameter
                }
                else
                {
                    fileInfoDic.Add(relativePathWithoutSuffix, new PsbList
                        {new PsbNumber(bodyPos), new PsbNumber(size)});
                }
            }

            //Collect files
            Logger.Log("Collecting files ...");
            foreach (var sourceDir in sourceDirs)
            {
                var targetDir = Path.IsPathRooted(sourceDir) ? sourceDir : Path.Combine(baseDir, sourceDir);
                if (intersect)
                {
                    CollectFilesFromList(targetDir, filter);
                }
                else
                {
                    CollectFiles(targetDir);
                }
            }

            var compileCount = files.Values.Count(m => m.Method == ArchiveProcessMethod.Compile);
            Logger.Log($"Packing {files.Count} files (compile: {compileCount}) ...");

            //using var mmFile =
            //    MemoryMappedFile.CreateFromFile(bodyBinFileName, FileMode.Create, coreName, );
            using var bodyFs = File.OpenWrite(bodyBinFileName);
            var fileInfoDic = new PsbDictionary(files.Count);
            var fmContext = FreeMount.CreateContext(context);
            //byte[] bodyBin = null;
            if (enableParallel)
            {
                var contents = new ConcurrentBag<(string Name, Stream Content)>();
                Parallel.ForEach(files, (kv) =>
                {
                    var relativePathWithoutSuffix = PsbExtension.ArchiveInfo_GetFileNameRemoveSuffix(kv.Key, suffix);
                    var fileNameWithSuffix = Path.GetFileName(kv.Key);

                    if (kv.Value.Method == ArchiveProcessMethod.None)
                    {
                        contents.Add((relativePathWithoutSuffix, File.OpenRead(kv.Value.Path)));
                        return;
                    }

                    var mdfContext = new Dictionary<string, object>(context);
                    var itemContext = FreeMount.CreateContext(mdfContext);
                    if (!string.IsNullOrEmpty(key))
                    {
                        mdfContext[Context_MdfKey] = key + fileNameWithSuffix;
                    }
                    else if (context[Context_MdfMtKey] is string mtKey)
                    {
                        mdfContext[Context_MdfKey] = mtKey + fileNameWithSuffix;
                    }
                    else
                    {
                        mdfContext.Remove(Context_MdfKey);
                    }

                    mdfContext.Remove(Context_ArchiveSource);

                    if (kv.Value.Method == ArchiveProcessMethod.EncodeMPack)
                    {
                        using var mmFs = MemoryMappedFile.CreateFromFile(kv.Value.Path, FileMode.Open);
                        //using var fs = File.OpenRead(kv.Value.Path);
                        //https://docs.microsoft.com/en-us/dotnet/api/system.io.memorymappedfiles.memorymappedfile.createviewstream?view=net-6.0
                        //NOTE: To create a complete view of the memory-mapped file, specify 0 (zero) for the size parameter.
                        //If you do this, the size of the view might be larger than the size of the source file on disk.
                        //This is because views are provided in units of system pages, and the size of the view is rounded up to the next system page size.
                        var fileSize = new FileInfo(kv.Value.Path).Length;
                        var mmStream = mmFs.CreateViewStream(0, fileSize, MemoryMappedFileAccess.Read);
                        if (MPack.IsSignatureMPack(mmStream, out var currentShellType) && currentShellType == defaultShellType) //prevent multiple pack
                        {
                            contents.Add((relativePathWithoutSuffix, mmStream)); //disposed later
                        }
                        else
                        {
                            contents.Add((relativePathWithoutSuffix, itemContext.PackToShell(mmStream, defaultShellType))); //disposed later
                        }
                    }
                    else
                    {
                        var content = LoadPsbAndContextFromJsonFile(kv.Value.Path);
                        var stream = content.Psb.ToStream();
                        var shellType = kv.Key.DefaultShellType(); //MARK: use shellType in filename, or use suffix in info?
                        if (!string.IsNullOrEmpty(shellType))
                        {
                            var packedStream = itemContext.PackToShell(stream, shellType); //disposed later
                            stream.Dispose();
                            stream = packedStream;
                        }
                        contents.Add((relativePathWithoutSuffix, stream));
                    }
                });

                Logger.Log($"{contents.Count} files packed, now merging...");

                //using var ms = mmFile.CreateViewStream();
                foreach (var item in contents.OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    if (fileInfoDic.ContainsKey(item.Name))
                    {
                        Logger.LogWarn($"[WARN] {item.Name} was added before, skipping...");
                        item.Content.Dispose(); //Remember to dispose!
                        continue;
                    }

                    AddFileInfo(fileInfoDic, item.Name, bodyFs.Position, item.Content.Length);
                    
                    if (item.Content is MemoryStream ims)
                    {
                        ims.WriteTo(bodyFs);
                    }
                    else
                    {
                        item.Content.CopyTo(bodyFs);
                    }

                    item.Content.Dispose(); //Remember to dispose!

                    if (archiveInfoType == PsbArchiveInfoType.UmdRoot && item.Name.EndsWith(".png"))
                    {
                        //Padding?
                        int end = (int) (bodyFs.Position % 16);
                        if (end != 0)
                        {
                            var pad = 16 - end;
                            bodyFs.Write(new byte[pad], 0, pad);
                        }
                    }
                }

                //bodyBin = ms.ToArray();
            }
            else //non-parallel
            {
                //using var ms = mmFile.CreateViewStream();
                foreach (var kv in files.OrderBy(f => f.Key, StringComparer.Ordinal))
                { 
                    Logger.Log($"{(kv.Value.Method == ArchiveProcessMethod.Compile? "Compiling" : "Packing")} {kv.Key} ...");
                    var relativePathWithoutSuffix = PsbExtension.ArchiveInfo_GetFileNameRemoveSuffix(kv.Key, suffix);
                    if (fileInfoDic.ContainsKey(relativePathWithoutSuffix))
                    {
                        Logger.LogWarn($"[WARN] {relativePathWithoutSuffix} was added before, skipping...");
                        continue;
                    }
                    var fileNameWithSuffix = Path.GetFileName(kv.Key);
                    if (kv.Value.Method == ArchiveProcessMethod.None)
                    {
                        using var mmFs = MemoryMappedFile.CreateFromFile(kv.Value.Path, FileMode.Open);
                        //using var fs = File.OpenRead(kv.Value.Path);
                        var fileSize = new FileInfo(kv.Value.Path).Length;
                        var fs = mmFs.CreateViewStream(0, fileSize, MemoryMappedFileAccess.Read);
                        AddFileInfo(fileInfoDic, relativePathWithoutSuffix, bodyFs.Position, fs.Length);

                        fs.CopyTo(bodyFs); //CopyTo starts from current position, while WriteTo starts from 0. Use WriteTo if there is.

                        if (archiveInfoType == PsbArchiveInfoType.UmdRoot && relativePathWithoutSuffix.EndsWith(".png"))
                        {
                            //Padding?
                            int end = (int) (bodyFs.Position % 16);
                            if (end != 0)
                            {
                                var pad = 16 - end;
                                bodyFs.Write(new byte[pad], 0, pad);
                            }
                        }
                    }
                    else if (kv.Value.Method == ArchiveProcessMethod.EncodeMPack)
                    {
                        if (!string.IsNullOrEmpty(key))
                        {
                            fmContext.Context[Context_MdfKey] = key + fileNameWithSuffix;
                        }
                        else if (context[Context_MdfMtKey] is string mtKey)
                        {
                            fmContext.Context[Context_MdfKey] = mtKey + fileNameWithSuffix;
                        }
                        else
                        {
                            fmContext.Context.Remove(Context_MdfKey);
                        }

                        using var mmFs = MemoryMappedFile.CreateFromFile(kv.Value.Path, FileMode.Open);
                        var fileSize = new FileInfo(kv.Value.Path).Length;
                        using var outputMdf = fmContext.PackToShell(mmFs.CreateViewStream(0, fileSize, MemoryMappedFileAccess.Read), defaultShellType);
                        AddFileInfo(fileInfoDic, relativePathWithoutSuffix, bodyFs.Position, outputMdf.Length);
                        outputMdf.WriteTo(bodyFs);
                    }
                    else
                    {
                        var content = LoadPsbAndContextFromJsonFile(kv.Value.Path);
                        if (!string.IsNullOrEmpty(key))
                        {
                            fmContext.Context[Context_MdfKey] = key + fileNameWithSuffix;
                        }
                        else
                        {
                            fmContext.Context = content.Context;
                        }

                        var stream = content.Psb.ToStream();
                        var shellType = kv.Key.DefaultShellType(); //MARK: use shellType in filename, or use suffix in info?
                        if (!string.IsNullOrEmpty(shellType))
                        {
                            var packedStream = fmContext.PackToShell(stream, shellType); //disposed later
                            stream.Dispose();
                            stream = packedStream;
                        }
                        
                        AddFileInfo(fileInfoDic, relativePathWithoutSuffix, bodyFs.Position, stream.Length);
                        stream.WriteTo(bodyFs);
                        stream.Dispose();
                    }
                }

                bodyFs.Flush();
                //bodyBin = ms.ToArray();
            }

            //Write
            bodyFs.Dispose();

            infoPsb.Objects[archiveInfoType.GetRootKey()] = fileInfoDic;

            infoPsb.Merge();
            if (key != null)
            {
                fmContext.Context[Context_MdfKey] = key + packageName;
            }
            else if (!string.IsNullOrEmpty(infoKey))
            {
                fmContext.Context[Context_MdfKey] = infoKey;
            }
            else
            {
                fmContext.Context.Remove(Context_MdfKey);
            }

            using var infoMdf = fmContext.PackToShell(infoPsb.ToStream(), fmContext.HasShell ? fmContext.Shell.ToUpperInvariant() : "MDF");
            File.WriteAllBytes(packageName, infoMdf.ToArray());
            infoMdf.Dispose();

            //File.WriteAllBytes(bodyBinFileName, bodyBin);
        }
    }
}