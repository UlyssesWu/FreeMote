using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreeMote.Plugins;
using FreeMote.Psb;
using FreeMote.PsBuild;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json.Linq;
using static FreeMote.Consts;
using static FreeMote.Psb.PsbExtension;

namespace FreeMote.Tools.PsBuild
{
    enum ProcessMethod
    {
        None = 0,
        EncodeMdf = 1,
        Compile = 2,
    }

    class Program
    {
        //private static PsbPixelFormat _pixelFormat = PsbPixelFormat.None;

        static void Main(string[] args)
        {
            Console.WriteLine("FreeMote PSB Compiler");
            Console.WriteLine("by Ulysses, wdwxy12345@gmail.com");

            FreeMount.Init();
            Console.WriteLine($"{FreeMount.PluginsCount} Plugins Loaded.");

            InMemoryLoading = true;
            Console.WriteLine();

            var app = new CommandLineApplication();
            app.OptionsComparison = StringComparison.OrdinalIgnoreCase;

            //help
            app.HelpOption();
            app.ExtendedHelpText = PrintHelp();

            //options
            var optVer = app.Option<ushort>("-v|--ver <VER>", "Set PSB version [2,4]. Default=3",
                CommandOptionType.SingleValue);
            var optKey = app.Option<uint>("-k|--key <KEY>", "Set PSB key (uint, dec)", CommandOptionType.SingleValue);
            var optSpec = app.Option<PsbSpec>("-p|--spec <SPEC>", "Set PSB platform (krkr/common/win/ems)",
                CommandOptionType.SingleValue);
            var optNoRename = app.Option("-no-rename",
                "Prevent output file renaming, may overwrite your original PSB files", CommandOptionType.NoValue);
            var optNoShell = app.Option("-no-shell", "Prevent shell packing (compression)", CommandOptionType.NoValue);
            var optDouble = app.Option("-double|--json-double", "(Json) Use double numbers only (no float)",
                CommandOptionType.NoValue, true);
            //var optOutputPath =
            //  app.Option<string>("-o|--output", "(TODO:)Set output directory or file name.", CommandOptionType.SingleValue);
            //TODO: If set dir, ok; if set filename, only works for the first

            //args
            var argPath =
                app.Argument("Files", "File paths", multipleValues: true);

            //command: link
            app.Command("link", linkCmd =>
            {
                //help
                linkCmd.Description = "Link textures into an external texture PSB";
                linkCmd.HelpOption();
                linkCmd.ExtendedHelpText = @"
Example:
  PsBuild link -o Order sample.psb tex000.png tex001.bmp 
";
                //options
                var optOrder = linkCmd.Option<PsbLinkOrderBy>("-o|--order <ORDER>",
                    "Set texture link order (Name/Order/Convention). Default=Name",
                    CommandOptionType.SingleValue);
                //args
                var argPsbPath = linkCmd.Argument("PSB", "PSB Path").IsRequired().Accepts(v => v.ExistingFile());
                var argTexPaths = linkCmd.Argument("Textures", "Texture Paths", true).IsRequired();

                linkCmd.OnExecute(() =>
                {
                    var order = optOrder.HasValue() ? optOrder.ParsedValue : PsbLinkOrderBy.Name;
                    var psbPath = argPsbPath.Value;
                    var texPaths = argTexPaths.Values;
                    Link(psbPath, texPaths, order);
                });
            });

            //command: port
            app.Command("port", portCmd =>
            {
                //help
                portCmd.Description = "Re-compile a PSB to another platform";
                portCmd.HelpOption();
                portCmd.ExtendedHelpText = @"
Example:
  PsBuild port -p win sample.psb 
";
                //options
                var optPortSpec = portCmd.Option<PsbSpec>("-p|--spec <SPEC>",
                    "Target PSB platform (krkr/common/win/ems)",
                    CommandOptionType.SingleValue).IsRequired();
                //args
                var argPsbPath = portCmd.Argument("PSB", "PSB Path", multipleValues: true).IsRequired();

                portCmd.OnExecute(() =>
                {
                    var portSpec = optPortSpec.ParsedValue;
                    var psbPaths = argPsbPath.Values;
                    foreach (var s in psbPaths)
                    {
                        if (File.Exists(s))
                        {
                            Port(s, portSpec);
                        }
                    }
                });
            });

            //info-psb
            app.Command("info-psb", archiveCmd =>
            {
                //help
                archiveCmd.Description = "Pack files to info.psb.m & body.bin (FreeMote.Plugins required).";
                archiveCmd.HelpOption();
                archiveCmd.ExtendedHelpText = @"
Example:
  PsBuild info-psb sample_info.psb.m.json (Key specified in resx.json)
  PsBuild info-psb -k 1234567890ab -l 131 sample_info.psb.m.json (Must keep every filename correct)
  Hint: Always keep file names correct. A file name in source folder must match a name kept in .m.json
  If there are both `a.psb.m` and `a.psb.m.json` in the source folder, `.json` will be used (unless using `--packed`).
  If you don't have enough RAM to keep the whole output, use `-1by1` and wait patiently.
";
                //options
                //var optMdfSeed = archiveCmd.Option("-s|--seed <SEED>",
                //    "Set complete seed (Key+FileName)",
                //    CommandOptionType.SingleValue);
                var optIntersect = archiveCmd.Option("-i|--intersect",
                    "Only pack files which existed in info.psb.m",
                    CommandOptionType.NoValue);
                var optPacked = archiveCmd.Option("-p|--packed",
                    "Prefer using PSB files rather than json files in source folder",
                    CommandOptionType.NoValue);
                //var optNoFolder = archiveCmd.Option("-nf|--no-folder",
                //    "Find files in source folder root at first, even if they should exist in other folders. Usually use with --intersect", CommandOptionType.NoValue);
                var optMdfKey = archiveCmd.Option("-k|--key <KEY>",
                    "Set key (get file name from input path)",
                    CommandOptionType.SingleValue);
                var optMdfKeyLen = archiveCmd.Option<int>("-l|--length <LEN>",
                    "Set key length. Default=131",
                    CommandOptionType.SingleValue);
                var optInfoOom = archiveCmd.Option("-1by1|--enumerate",
                    "Disable parallel processing (can be slow but save a lot memory)", CommandOptionType.NoValue);
                var optInfoRaw = archiveCmd.Option("-raw|--raw",
                    "Keep all sources raw (don't compile jsons or pack MDF shell)", CommandOptionType.NoValue);

                //args
                var argPsbPaths = archiveCmd.Argument("PSB", "Archive Info PSB .json paths", true);

                archiveCmd.OnExecute(() =>
                {
                    //bool noFolder = optNoFolder.HasValue();
                    bool noFolder = false; //not worth time to support it for now
                    bool intersect = optIntersect.HasValue();
                    bool preferPacked = optPacked.HasValue();
                    bool enableParallel = FastMode;
                    bool keepRaw = false;
                    if (optInfoOom.HasValue())
                    {
                        enableParallel = false;
                    }

                    if (optInfoRaw.HasValue())
                    {
                        keepRaw = true;
                    }

                    string key = optMdfKey.HasValue() ? optMdfKey.Value() : null;
                    //string seed = optMdfSeed.HasValue() ? optMdfSeed.Value() : null;

                    int keyLen = optMdfKeyLen.HasValue() ? optMdfKeyLen.ParsedValue : 131;

                    Stopwatch sw = Stopwatch.StartNew();
                    foreach (var s in argPsbPaths.Values)
                    {
                        PackArchive(s, key, intersect, preferPacked, noFolder, enableParallel, keyLen, keepRaw);
                    }
                    sw.Stop();
                    Console.WriteLine($"Process time: {sw.Elapsed:g}");
                });
            });

            //command: replace
            app.Command("replace", replaceCmd =>
            {
                //help
                replaceCmd.Description = "In-place Replace the images in PSB";
                replaceCmd.HelpOption();
                replaceCmd.ExtendedHelpText = @"
Example:
  PsBuild replace sample.psb sample.json
  Hint: Only works with textures not compressed (RGBA8, RGBA4444) pure PSBs.
";
                var argPsbPath = replaceCmd.Argument("PSB", "PSB path", false);
                var argJsonPath = replaceCmd.Argument("Json", "PSB Json path", false);

                replaceCmd.OnExecute(() =>
                {
                    if (!File.Exists(argPsbPath.Value) || !File.Exists(argJsonPath.Value))
                    {
                        Console.WriteLine("File not exists.");
                        return;
                    }

                    var output = PsbCompiler.InplaceReplaceToFile(argPsbPath.Value, argJsonPath.Value);
                    Console.WriteLine($"In-place Replace Output: {output}");
                });
            });

            app.OnExecute(() =>
            {
                if (optDouble.HasValue())
                {
                    JsonUseDoubleOnly = true;
                }

                ushort? ver = optVer.HasValue() ? optVer.ParsedValue : null;
                uint? key = optKey.HasValue() ? optKey.ParsedValue : null;
                PsbSpec? spec = optSpec.HasValue() ? optSpec.ParsedValue : null;
                var canRename = !optNoRename.HasValue();
                var canPack = !optNoShell.HasValue();

                foreach (var file in argPath.Values)
                {
                    Compile(file, ver, key, spec, canRename, canPack);
                }
            });

            if (args.Length == 0)
            {
                app.ShowHelp();
                return;
            }

            app.Execute(args);

            Console.WriteLine("Done.");
        }

        private static void Port(string s, PsbSpec portSpec)
        {
            var name = Path.GetFileNameWithoutExtension(s);
            var ext = Path.GetExtension(s);
            Console.WriteLine($"Converting {name} to {portSpec} platform...");
            PSB psb = new PSB(s);
            if (psb.Platform == portSpec)
            {
                Console.WriteLine("Already at the same platform, Skip.");
            }
            else
            {
                psb.SwitchSpec(portSpec);
                psb.Merge();
                File.WriteAllBytes(Path.ChangeExtension(s, $".{portSpec}{psb.Type.DefaultExtension()}"), psb.Build());
                Console.WriteLine($"Convert {name} done.");
            }
        }

        private static void Link(string psbPath, List<string> texPaths, PsbLinkOrderBy order)
        {
            if (!File.Exists(psbPath))
            {
                return;
            }

            var name = Path.GetFileNameWithoutExtension(psbPath);
            var ext = Path.GetExtension(psbPath);

            try
            {
                List<string> texs = new List<string>();
                foreach (var texPath in texPaths)
                {
                    if (File.Exists(texPath))
                    {
                        texs.Add(texPath);
                    }
                    else if (Directory.Exists(texPath))
                    {
                        texs.AddRange(Directory.EnumerateFiles(texPath));
                    }
                }

                PSB psb = new PSB(psbPath);
                psb.Link(texs, order: order, isExternal: true);
                psb.Merge();
                File.WriteAllBytes(Path.ChangeExtension(psbPath, "linked" + ext), psb.Build());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Console.WriteLine($"Link {name} done.");
        }

        private static void Compile(string s, ushort? version, uint? key, PsbSpec? spec, bool canRename,
            bool canPackShell)
        {
            if (!File.Exists(s))
            {
                //此処にいて何処にもいない　キミの面影はいつも朧 : https://soundcloud.com/yuhyuhyuhxibbd2/parallel-utau
                return;
            }

            var name = Path.GetFileNameWithoutExtension(s);
            var ext = Path.GetExtension(s);

            Console.WriteLine($"Compiling {name} ...");
            try
            {
                //var filename = name + (_key == null ? _noRename ? ".psb" : "-pure.psb" : "-impure.psb");
                var filename = name + ".psb"; //rename later //TODO: support set output path
                PsbCompiler.CompileToFile(s, filename, null, version, key, spec, canRename,
                    canPackShell);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Compile {name} failed.\r\n{e}");
            }

            Console.WriteLine($"Compile {name} done.");
        }

        private static string PrintHelp()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine().AppendLine("Plugins:");
            sb.AppendLine(FreeMount.PrintPluginInfos(2));
            sb.AppendLine(@"Examples: 
  PsBuild -v 4 -k 123456789 -p krkr sample.psb.json");
            return sb.ToString();
        }

        /// <summary>
        /// Pack Archive PSB
        /// </summary>
        /// <param name="jsonPath">json path</param>
        /// <param name="key">crypt key</param>
        /// <param name="intersect">Only pack files which existed in info.psb.m</param>
        /// <param name="preferPacked">Prefer using PSB files rather than json files in source folder</param>
        /// <param name="noFolder">Always try searching files at source root path at first</param>
        /// <param name="enableParallel">parallel process</param>
        /// <param name="keyLen">key length</param>
        /// <param name="keepRaw">Do not try to compile json or pack MDF</param>
        public static void PackArchive(string jsonPath, string key, bool intersect, bool preferPacked, bool noFolder = false, bool enableParallel = true,
            int keyLen = 131, bool keepRaw = false)
        {
            if (!File.Exists(jsonPath)) return;
            PSB infoPsb = PsbCompiler.LoadPsbFromJsonFile(jsonPath);
            if (infoPsb.Type != PsbType.ArchiveInfo)
            {
                Console.WriteLine("Json is not an ArchiveInfo PSB.");
                return;
            }

            var resx = PsbResourceJson.LoadByPsbJsonPath(jsonPath);
            if (!resx.Context.ContainsKey(Context_ArchiveSource) ||
                resx.Context[Context_ArchiveSource] == null)
            {
                Console.WriteLine("ArchiveSource must be specified in resx.json Context.");
                return;
            }

            if (keyLen > 0)
            {
                resx.Context[Context_MdfKeyLength] = keyLen;
            }

            string infoKey = null;
            if (resx.Context[Context_MdfKey] is string mdfKey)
            {
                infoKey = mdfKey;
            }

            List<string> sourceDirs = null;
            if (resx.Context[Context_ArchiveSource] is string path)
            {
                sourceDirs = new List<string> { path };
            }
            else if (resx.Context[Context_ArchiveSource] is IList paths)
            {
                sourceDirs = new List<string>(paths.Count);
                sourceDirs.AddRange(from object p in paths select p.ToString());
            }
            else
            {
                Console.WriteLine("ArchiveSource incorrect.");
                return;
            }

            var baseDir = Path.GetDirectoryName(jsonPath);
            var files = new Dictionary<string, (string Path, ProcessMethod Method)>();
            var suffix = ArchiveInfoGetSuffix(infoPsb);
            HashSet<string> filter = null;
            if (intersect) //only collect files appeared in json
            {
                filter = ArchiveInfoCollectFiles(infoPsb, suffix).Select(p => p.Replace('\\', '/')).ToHashSet();
            }

            if (filter != null && resx.Context[Context_ArchiveItemFileNames] is IList fileNames)
            {
                foreach (var fileName in fileNames)
                {
                    filter.Add(fileName.ToString());
                }
            }

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
                            files[infoFile] = (f, keepRaw ? ProcessMethod.None : ProcessMethod.Compile);
                        }
                        else if (File.Exists(source))
                        {
                            files[infoFile] = (f, ProcessMethod.Compile);
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
                            files[name].Method != ProcessMethod.Compile) //it's always right no matter set or replace
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
                                files[name] = (f, keepRaw ? ProcessMethod.None : ProcessMethod.Compile);
                            }
                        }
                    }
                    else
                    {
                        var name = relativePath;
                        if (!preferPacked && files.ContainsKey(name) &&
                            files[name].Method == ProcessMethod.Compile)
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
                                if (!MdfFile.IsSignatureMdf(fs) && name.DefaultShellType() == "MDF")
                                {
                                    files[name] = (f, keepRaw ? ProcessMethod.None : ProcessMethod.EncodeMdf);
                                }
                                else
                                {
                                    files[name] = (f, ProcessMethod.None);
                                }
                            }
                        }
                    }
                }
            }

            //Collect files
            Console.WriteLine("Collecting files ...");
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

            Console.WriteLine($"Packing {files.Count} files ...");
            var bodyBinFileName = Path.GetFileName(jsonPath);
            var packageName = Path.GetFileNameWithoutExtension(bodyBinFileName);

            var coreName = ArchiveInfoGetPackageName(packageName);
            bodyBinFileName = string.IsNullOrEmpty(coreName) ? packageName + "_body.bin" : coreName + "_body.bin";

            //using var mmFile =
            //    MemoryMappedFile.CreateFromFile(bodyBinFileName, FileMode.Create, coreName, );
            using var bodyFs = File.OpenWrite(bodyBinFileName);
            var fileInfoDic = new PsbDictionary(files.Count);
            var fmContext = FreeMount.CreateContext(resx.Context);
            //byte[] bodyBin = null;
            if (enableParallel)
            {
                var contents = new ConcurrentBag<(string Name, Stream Content)>();
                Parallel.ForEach(files, (kv) =>
                {
                    var relativePathWithoutSuffix = ArchiveInfoGetFileNameRemoveSuffix(kv.Key, suffix);
                    var fileNameWithSuffix = Path.GetFileName(kv.Key);

                    if (kv.Value.Method == ProcessMethod.None)
                    {
                        contents.Add((relativePathWithoutSuffix, File.OpenRead(kv.Value.Path)));
                        return;
                    }

                    var mdfContext = new Dictionary<string, object>(resx.Context);
                    var context = FreeMount.CreateContext(mdfContext);
                    if (!string.IsNullOrEmpty(key))
                    {
                        mdfContext[Context_MdfKey] = key + fileNameWithSuffix;
                    }
                    else if (resx.Context[Context_MdfMtKey] is string mtKey)
                    {
                        mdfContext[Context_MdfKey] = mtKey + fileNameWithSuffix;
                    }
                    else
                    {
                        mdfContext.Remove(Context_MdfKey);
                    }

                    mdfContext.Remove(Context_ArchiveSource);

                    if (kv.Value.Method == ProcessMethod.EncodeMdf)
                    {
                        using var mmFs = MemoryMappedFile.CreateFromFile(kv.Value.Path, FileMode.Open);

                        //using var fs = File.OpenRead(kv.Value.Path);
                        contents.Add((relativePathWithoutSuffix, context.PackToShell(mmFs.CreateViewStream(), "MDF"))); //disposed later
                    }
                    else
                    {
                        var content = PsbCompiler.LoadPsbAndContextFromJsonFile(kv.Value.Path);
                        var stream = content.Psb.ToStream();
                        var shellType = kv.Key.DefaultShellType(); //MARK: use shellType in filename, or use suffix in info?
                        if (!string.IsNullOrEmpty(shellType))
                        {
                            var packedStream = context.PackToShell(stream, shellType); //disposed later
                            stream.Dispose();
                            stream = packedStream;
                        }
                        contents.Add((relativePathWithoutSuffix, stream));
                    }
                });

                Console.WriteLine($"{contents.Count} files packed, now merging...");

                //using var ms = mmFile.CreateViewStream();
                foreach (var item in contents.OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    if (fileInfoDic.ContainsKey(item.Name))
                    {
                        Console.WriteLine($"[WARN] {item.Name} is added before, skipping...");
                        item.Content.Dispose(); //Remember to dispose!
                        continue;
                    }
                    fileInfoDic.Add(item.Name,
                        new PsbList
                            {new PsbNumber(bodyFs.Position), new PsbNumber(item.Content.Length)});
                    if (item.Content is MemoryStream ims)
                    {
                        ims.WriteTo(bodyFs);
                    }
                    else
                    {
                        item.Content.CopyTo(bodyFs);
                    }

                    item.Content.Dispose(); //Remember to dispose!
                }

                //bodyBin = ms.ToArray();
            }
            else //non-parallel
            {
                //using var ms = mmFile.CreateViewStream();
                foreach (var kv in files.OrderBy(f => f.Key, StringComparer.Ordinal))
                {
                    Console.WriteLine($"Packing {kv.Key} ...");
                    var relativePathWithoutSuffix = ArchiveInfoGetFileNameRemoveSuffix(kv.Key, suffix);
                    if (fileInfoDic.ContainsKey(relativePathWithoutSuffix))
                    {
                        Console.WriteLine($"[WARN] {relativePathWithoutSuffix} is added before, skipping...");
                        continue;
                    }
                    var fileNameWithSuffix = Path.GetFileName(kv.Key);
                    if (kv.Value.Method == ProcessMethod.None)
                    {
                        using var mmFs = MemoryMappedFile.CreateFromFile(kv.Value.Path, FileMode.Open);
                        //using var fs = File.OpenRead(kv.Value.Path);
                        var fs = mmFs.CreateViewStream();
                        fileInfoDic.Add(relativePathWithoutSuffix, new PsbList
                            {new PsbNumber(bodyFs.Position), new PsbNumber(fs.Length)});
                        fs.CopyTo(bodyFs); //CopyTo starts from current position, while WriteTo starts from 0. Use WriteTo if there is.
                    }
                    else if (kv.Value.Method == ProcessMethod.EncodeMdf)
                    {
                        if (!string.IsNullOrEmpty(key))
                        {
                            fmContext.Context[Context_MdfKey] = key + fileNameWithSuffix;
                        }
                        else if (resx.Context[Context_MdfMtKey] is string mtKey)
                        {
                            fmContext.Context[Context_MdfKey] = mtKey + fileNameWithSuffix;
                        }
                        else
                        {
                            fmContext.Context.Remove(Context_MdfKey);
                        }

                        using var mmFs = MemoryMappedFile.CreateFromFile(kv.Value.Path, FileMode.Open);
                        using var outputMdf = fmContext.PackToShell(mmFs.CreateViewStream(), "MDF");
                        fileInfoDic.Add(relativePathWithoutSuffix, new PsbList
                            {new PsbNumber(bodyFs.Position), new PsbNumber(outputMdf.Length)});
                        outputMdf.WriteTo(bodyFs);
                    }
                    else
                    {
                        var content = PsbCompiler.LoadPsbAndContextFromJsonFile(kv.Value.Path);
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

                        fileInfoDic.Add(relativePathWithoutSuffix, new PsbList
                            {new PsbNumber(bodyFs.Position), new PsbNumber(stream.Length)});
                        stream.WriteTo(bodyFs);
                        stream.Dispose();
                    }
                }

                bodyFs.Flush();
                //bodyBin = ms.ToArray();
            }

            //Write
            bodyFs.Dispose();

            infoPsb.Objects["file_info"] = fileInfoDic;

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

            using var infoMdf = fmContext.PackToShell(infoPsb.ToStream(), "MDF");
            File.WriteAllBytes(packageName, infoMdf.ToArray());
            infoMdf.Dispose();

            //File.WriteAllBytes(bodyBinFileName, bodyBin);
        }
    }
}