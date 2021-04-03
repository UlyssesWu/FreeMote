using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
using static FreeMote.Consts;

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
                archiveCmd.Description = "Pack files to info.psb.m & body.bin (FreeMote.Plugins required)";
                archiveCmd.HelpOption();
                archiveCmd.ExtendedHelpText = @"
Example:
  PsBuild info-psb sample_info.psb.m.json (Key specified in resx.json)
  PsBuild info-psb -k 1234567890ab -l 131 sample_info.psb.m.json (Must keep every filename correct)
  Hint: Always keep file names correct. A file name in source folder must match a name kept in .m.json
  If there are both `.m` and `.m.json` in the source folder, `.json` will be used (unless using `-p`).
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
                var optMdfKey = archiveCmd.Option("-k|--key <KEY>",
                    "Set key (get file name from input path)",
                    CommandOptionType.SingleValue);
                var optMdfKeyLen = archiveCmd.Option<int>("-l|--length <LEN>",
                    "Set key length. Default=131",
                    CommandOptionType.SingleValue);
                var optInfoOom = archiveCmd.Option("-1by1|--enumerate",
                    "Disable parallel processing (can be slow but save a lot memory)", CommandOptionType.NoValue);

                //args
                var argPsbPaths = archiveCmd.Argument("PSB", "Archive Info PSB .json paths", true);

                archiveCmd.OnExecute(() =>
                {
                    bool intersect = optIntersect.HasValue();
                    bool preferPacked = optPacked.HasValue();
                    bool enableParallel = FastMode;
                    if (optInfoOom.HasValue())
                    {
                        enableParallel = false;
                    }

                    string key = optMdfKey.HasValue() ? optMdfKey.Value() : null;
                    //string seed = optMdfSeed.HasValue() ? optMdfSeed.Value() : null;

                    int keyLen = optMdfKeyLen.HasValue() ? optMdfKeyLen.ParsedValue : 131;

                    foreach (var s in argPsbPaths.Values)
                    {
                        PackArchive(s, key, intersect, preferPacked, enableParallel, keyLen);
                    }
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

                ushort? ver = optVer.HasValue() ? optVer.ParsedValue : (ushort?) null;
                uint? key = optKey.HasValue() ? optKey.ParsedValue : (uint?) null;
                PsbSpec? spec = optSpec.HasValue() ? optSpec.ParsedValue : (PsbSpec?) null;
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

        private static string ArchiveInfoPsbGetFileName(string fileName, string suffix)
        {
            if (string.IsNullOrEmpty(suffix))
            {
                return fileName;
            }

            if (fileName.EndsWith(suffix, true, CultureInfo.InvariantCulture))
            {
                return fileName.Remove(fileName.Length - suffix.Length, suffix.Length);
            }

            return fileName;
        }

        private static List<string> ArchiveInfoPsbCollectFiles(PSB psb, string suffix)
        {
            if (psb.Objects.ContainsKey("file_info") && psb.Objects["file_info"] is PsbDictionary fileInfo)
            {
                return fileInfo.Keys.Select(name => name + suffix).ToList();
            }

            return null;
        }

        private static string ArchiveInfoPsbGetSuffix(PSB psb)
        {
            var suffix = "";
            if (psb.Objects.ContainsKey("expire_suffix_list") &&
                psb.Objects["expire_suffix_list"] is PsbList col && col[0] is PsbString s)
            {
                suffix = s;
            }

            return suffix;
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
        /// <param name="enableParallel">parallel process</param>
        /// <param name="keyLen">key length</param>
        public static void PackArchive(string jsonPath, string key, bool intersect, bool preferPacked, bool enableParallel = true,
            int keyLen = 131)
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
                sourceDirs = new List<string> {path};
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
            var suffix = ArchiveInfoPsbGetSuffix(infoPsb);
            List<string> filter = null;
            if (intersect)
            {
                filter = ArchiveInfoPsbCollectFiles(infoPsb, suffix);
            }

            void CollectFiles(string targetDir)
            {
                if (!Directory.Exists(targetDir))
                {
                    return;
                }

                foreach (var f in Directory.EnumerateFiles(targetDir))
                {
                    if (f.EndsWith(".resx.json", true, CultureInfo.InvariantCulture))
                    {
                        continue;
                    }
                    else if (f.EndsWith(".json", true, CultureInfo.InvariantCulture))
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        if (preferPacked && files.ContainsKey(name) &&
                            files[name].Method != ProcessMethod.Compile)
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
                                files[name] = (f, ProcessMethod.Compile);
                            }
                        }
                    }
                    else
                    {
                        var name = Path.GetFileName(f);
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
                                    files[name] = (f, ProcessMethod.EncodeMdf);
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
            foreach (var sourceDir in sourceDirs)
            {
                CollectFiles(Path.IsPathRooted(sourceDir) ? sourceDir : Path.Combine(baseDir, sourceDir));
            }

            var fileName = Path.GetFileName(jsonPath);
            var packageName = Path.GetFileNameWithoutExtension(fileName);

            var coreName = PsbExtension.ArchiveInfoGetPackageName(packageName);
            fileName = string.IsNullOrEmpty(coreName) ? packageName + "_body.bin" : coreName + "_body.bin";

            var fileInfoDic = new PsbDictionary(files.Count);
            var fmContext = FreeMount.CreateContext(resx.Context);
            byte[] bodyBin = null;
            if (enableParallel)
            {
                var contents = new ConcurrentBag<(string Name, Stream Content)>();
                Parallel.ForEach(files, (kv) =>
                {
                    var fileNameWithoutSuffix = ArchiveInfoPsbGetFileName(kv.Key, suffix);
                    if (kv.Value.Method == ProcessMethod.None)
                    {
                        contents.Add((fileNameWithoutSuffix, File.OpenRead(kv.Value.Path)));
                        return;
                    }

                    var mdfContext = new Dictionary<string, object>(resx.Context);
                    var context = FreeMount.CreateContext(mdfContext);
                    if (!string.IsNullOrEmpty(key))
                    {
                        mdfContext[Context_MdfKey] = key + fileNameWithoutSuffix + suffix;
                    }
                    else if (resx.Context[Context_MdfMtKey] is string mtKey)
                    {
                        mdfContext[Context_MdfKey] =
                            mtKey + fileNameWithoutSuffix + suffix;
                    }
                    else
                    {
                        mdfContext.Remove(Context_MdfKey);
                    }

                    mdfContext.Remove(Context_ArchiveSource);

                    if (kv.Value.Method == ProcessMethod.EncodeMdf)
                    {
                        contents.Add((fileNameWithoutSuffix, context.PackToShell(
                            File.OpenRead(kv.Value.Path), "MDF")));
                    }
                    else
                    {
                        var content = PsbCompiler.LoadPsbAndContextFromJsonFile(kv.Value.Path);

                        var outputMdf = context.PackToShell(content.Psb.ToStream(), "MDF");
                        contents.Add((fileNameWithoutSuffix, outputMdf));
                    }
                });

                Console.WriteLine($"{contents.Count} files collected.");
                using (var ms = new MemoryStream())
                {
                    foreach (var item in contents.OrderBy(item => item.Name, StringComparer.Ordinal))
                    {
                        fileInfoDic.Add(item.Name,
                            new PsbList
                                {new PsbNumber((int) ms.Position), new PsbNumber(item.Content.Length)});
                        if (item.Content is MemoryStream ims)
                        {
                            ims.WriteTo(ms);
                        }
                        else
                        {
                            item.Content.CopyTo(ms);
                        }

                        item.Content.Dispose();
                    }

                    bodyBin = ms.ToArray();
                }
            }
            else //non-parallel
            {
                //TODO: support pack 2GB+ archive. Does anyone even need this?
                using var ms = new MemoryStream();
                foreach (var kv in files.OrderBy(f => f.Key, StringComparer.Ordinal))
                {
                    var fileNameWithoutSuffix = ArchiveInfoPsbGetFileName(kv.Key, suffix);
                    if (kv.Value.Method == ProcessMethod.None)
                    {
                        using (var fs = File.OpenRead(kv.Value.Path))
                        {
                            fs.CopyTo(
                                ms); //CopyTo starts from current position, while WriteTo starts from 0. Use WriteTo if there is.
                            fileInfoDic.Add(fileNameWithoutSuffix, new PsbList
                                {new PsbNumber((int) ms.Position), new PsbNumber(fs.Length)});
                        }
                    }
                    else if (kv.Value.Method == ProcessMethod.EncodeMdf)
                    {
                        if (!string.IsNullOrEmpty(key))
                        {
                            fmContext.Context[Context_MdfKey] = key + fileNameWithoutSuffix + suffix;
                        }
                        else if (resx.Context[Context_MdfMtKey] is string mtKey)
                        {
                            fmContext.Context[Context_MdfKey] =
                                mtKey + fileNameWithoutSuffix + suffix;
                        }
                        else
                        {
                            fmContext.Context.Remove(Context_MdfKey);
                        }

                        var outputMdf = fmContext.PackToShell(File.OpenRead(kv.Value.Path), "MDF");
                        outputMdf.WriteTo(ms);
                        fileInfoDic.Add(fileNameWithoutSuffix, new PsbList
                            {new PsbNumber((int) ms.Position), new PsbNumber(outputMdf.Length)});
                        outputMdf.Dispose();
                    }
                    else
                    {
                        var content = PsbCompiler.LoadPsbAndContextFromJsonFile(kv.Value.Path);
                        if (!string.IsNullOrEmpty(key))
                        {
                            fmContext.Context[Context_MdfKey] = key + fileNameWithoutSuffix + suffix;
                        }
                        else
                        {
                            fmContext.Context = content.Context;
                        }

                        var outputMdf = fmContext.PackToShell(content.Psb.ToStream(), "MDF");
                        outputMdf.WriteTo(ms);
                        fileInfoDic.Add(fileNameWithoutSuffix, new PsbList
                            {new PsbNumber((int) ms.Position), new PsbNumber(outputMdf.Length)});
                        outputMdf.Dispose();
                    }
                }

                bodyBin = ms.ToArray();
            }

            //Write
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

            var infoMdf = fmContext.PackToShell(infoPsb.ToStream(), "MDF");
            File.WriteAllBytes(packageName, infoMdf.ToArray());
            infoMdf.Dispose();

            File.WriteAllBytes(fileName, bodyBin);
        }
    }
}