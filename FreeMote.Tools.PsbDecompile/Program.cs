using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FreeMote.Plugins;
using FreeMote.Psb;
using FreeMote.PsBuild;
using McMaster.Extensions.CommandLineUtils;
using static FreeMote.Consts;

// .pmf: https://wiki.multimedia.cx/index.php/PSMF 

namespace FreeMote.Tools.PsbDecompile
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("FreeMote PSB Decompiler");
            Console.WriteLine("by Ulysses, wdwxy12345@gmail.com");
            Logger.InitConsole();
            if (args.Length > 0 && args[0] == FreeMount.ARG_DISABLE_PLUGINS)
            {
                Console.WriteLine("Plugins disabled.");
            }
            else
            {
                FreeMount.Init();
                Console.WriteLine($"{FreeMount.PluginsCount} Plugins Loaded.");
            }

            InMemoryLoading = true;
            Console.WriteLine();

            var app = new CommandLineApplication();
            app.OptionsComparison = StringComparison.OrdinalIgnoreCase;

            //help
            app.HelpOption(); //do not inherit
            app.ExtendedHelpText = PrintHelp();

            //options
            var optMdfSeed = app.Option<string>("-s|--seed <SEED>", "Set MT19937 MDF seed (Key+FileName)", CommandOptionType.SingleValue,
                inherited: false);
            var optMdfKeyLen = app.Option<int>("-l|--length <LEN>", "Set MT19937 MDF key length. Default=131",
                CommandOptionType.SingleValue, inherited: false);
            var optKey = app.Option<uint>("-k|--key", "Set PSB key (uint, dec)", CommandOptionType.SingleValue);
            //var optFormat = app.Option<PsbImageFormat>("-e|--extract <FORMAT>",
            //    "Convert textures to png/bmp. Default=png", CommandOptionType.SingleValue, true);
            var optRaw = app.Option("-raw|--raw", "Output raw resources", CommandOptionType.NoValue, inherited: true);
            //メモリ足りない もうどうしよう : https://soundcloud.com/ulysses-wu/Heart-Chrome
            var optOom = app.Option("-oom|--memory-limit", "Disable In-Memory Loading", CommandOptionType.NoValue, inherited: true);
            var optNoParallel = app.Option("-1by1|--enumerate",
                "Disable parallel processing (can be very slow)", CommandOptionType.NoValue, inherited: true);
            var optHex = app.Option("-hex|--json-hex", "(Json) Use hex numbers", CommandOptionType.NoValue, true);
            var optArray = app.Option("-indent|--json-array-indent", "(Json) Indent arrays", CommandOptionType.NoValue, true);
            var optDisableFlattenArray = app.Option("-dfa|--disable-flatten-array",
                "Disable represent extra resource as flatten arrays", CommandOptionType.NoValue, inherited: true);
            var optEncoding = app.Option<string>("-e|--encoding <ENCODING>", "Set encoding (e.g. SHIFT-JIS). Default=UTF-8",
                CommandOptionType.SingleValue, inherited: true);
            var optType = app.Option<PsbType>("-t|--type <TYPE>", "Set PSB type manually", CommandOptionType.SingleValue, inherited: true);
            var optDisableCombinedImage = app.Option("-dci|--disable-combined-image",
                "Output chunk images (pieces) for image (Tachie) PSB (try this if you have problem on image type PSB)", CommandOptionType.NoValue);
            var optOutputPath = app.Option<string>("-o|--output <PATH>", "Set output folder path.",
                CommandOptionType.SingleValue, inherited: false);

            //args
            var argPath =
                app.Argument("Files", "File paths", multipleValues: true);

            //command: image
            app.Command("image", imageCmd =>
            {
                //help
                imageCmd.Description = "Extract (combined) images from image (Tachie) PSB (with \"imageList\")";
                imageCmd.HelpOption();
                imageCmd.ExtendedHelpText = @"
Example:
  PsbDecompile image tachie.psb
  PsbDecompile image sample-resource-folder
";
                //options
                var optOutputFolder = imageCmd.Option<string>("-o|--output <PATH>",
                    "Set output folder path. Default=Input file directory",
                    CommandOptionType.SingleValue);
                //args
                var argPsbPath = imageCmd.Argument("Path", "PSB paths or PSB directory paths").IsRequired();

                imageCmd.OnExecute(() =>
                {
                    var enableParallel = !optNoParallel.HasValue();
                    var psbPaths = argPsbPath.Values;
                    string outputFolder = null;
                    if (optOutputFolder.HasValue())
                    {
                        outputFolder = ResolveOutputFolder(optOutputFolder.Value());
                    }

                    foreach (var psbPath in psbPaths)
                    {
                        if (File.Exists(psbPath))
                        {
                            try
                            {
                                PsbDecompiler.ExtractImageFiles(psbPath, outputFolder: outputFolder);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                        }
                        else if (Directory.Exists(psbPath))
                        {
                            var files = FreeMoteExtension.GetFiles(psbPath, new[] {"*.psb", "*.pimg", "*.m", "*.bytes"});

                            if (enableParallel)
                            {
                                Parallel.ForEach(files, (s, state) =>
                                {
                                    try
                                    {
                                        PsbDecompiler.ExtractImageFiles(s, outputFolder: outputFolder);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(e);
                                    }
                                });
                            }
                            else
                            {
                                foreach (var s in files)
                                {
                                    try
                                    {
                                        PsbDecompiler.ExtractImageFiles(s, outputFolder: outputFolder);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(e);
                                    }
                                }
                            }
                        }
                    }
                });
            });

            //command: unlink
            app.Command("unlink", linkCmd =>
            {
                //help
                linkCmd.Description = "Unlink textures from PSBs";
                linkCmd.HelpOption();
                linkCmd.ExtendedHelpText = @"
Example:
  PsbDecompile unlink sample.psb
";
                //options
                var optOrder = linkCmd.Option<PsbLinkOrderBy>("-o|--order <ORDER>",
                    "Set texture unlink order (ByName/ByOrder/Convention). Default=ByName",
                    CommandOptionType.SingleValue);
                //args
                var argPsbPath = linkCmd.Argument("PSB", "PSB Path").IsRequired();
                //var argTexPath = linkCmd.Argument("Textures", "Texture Paths").IsRequired();

                linkCmd.OnExecute(() =>
                {
                    if (optEncoding.HasValue())
                    {
                        try
                        {
                            Encoding encoding = Encoding.GetEncoding(optEncoding.ParsedValue);
                            PsbDecompiler.Encoding = encoding;
                        }
                        catch (ArgumentException e)
                        {
                            Console.WriteLine($"[WARN] Encoding {optEncoding.Value()} is not valid.");
                        }
                    }

                    //PsbImageFormat format = optFormat.HasValue() ? optFormat.ParsedValue : PsbImageFormat.png;
                    var order = optOrder.HasValue() ? optOrder.ParsedValue : PsbLinkOrderBy.Name;
                    var psbPaths = argPsbPath.Values;
                    foreach (var psbPath in psbPaths)
                    {
                        if (File.Exists(psbPath))
                        {
                            try
                            {
                                PsbDecompiler.UnlinkToFile(psbPath, order: order);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Input path not found: {psbPath}");
                        }
                    }
                });
            });

            //info-psb
            app.Command("info-psb", archiveCmd =>
            {
                //help
                archiveCmd.Description = "Extract files from info.psb.m & body.bin (FreeMote.Plugins required)";
                archiveCmd.HelpOption();
                archiveCmd.ExtendedHelpText = @"
Example:
  PsbDecompile info-psb -k 1234567890ab sample_info.psb.m
  PsbDecompile info-psb -k 1234567890ab -l 131 -a sample_info.psb.m
  Hint: The body.bin should exist in the same folder and keep both file names correct.
";
                //options
                //var optMdfSeed = archiveCmd.Option("-s|--seed <SEED>",
                //    "Set complete seed (Key+FileName)",
                //    CommandOptionType.SingleValue);
                var optExtractAll = archiveCmd.Option("-a|--all",
                    "Decompile all contents in body.bin if possible (can be slow)",
                    CommandOptionType.NoValue);
                var optMdfKey = archiveCmd.Option("-k|--key <KEY>",
                    "Set key (Infer file name from path)",
                    CommandOptionType.SingleValue);
                var optMdfKeyLen2 = archiveCmd.Option<int>("-l|--length <LEN>",
                    "Set key length. Default=131",
                    CommandOptionType.SingleValue);
                var optBody = archiveCmd.Option<string>("-b|--body <PATH>",
                    "Set body.bin path. Default={xxx}_body.bin",
                    CommandOptionType.SingleValue);
                var optOutputFolder = archiveCmd.Option<string>("-o|--output <PATH>",
                    "Set output folder path. Default=Input file directory",
                    CommandOptionType.SingleValue);
                //var optNoFolder = archiveCmd.Option("-nf|--no-folder",
                //    "extract all files into source folder root, ignore the folder structure described in info.psb. May overwrite files; Won't be able to repack.",
                //    CommandOptionType.NoValue);

                //args
                var argPsbPaths = archiveCmd.Argument("PSB", "Archive Info PSB Paths", true);

                archiveCmd.OnExecute(() =>
                {
                    if (optEncoding.HasValue())
                    {
                        try
                        {
                            Encoding encoding = Encoding.GetEncoding(optEncoding.ParsedValue);
                            PsbDecompiler.Encoding = encoding;
                        }
                        catch (ArgumentException e)
                        {
                            Console.WriteLine($"[WARN] Encoding {optEncoding.Value()} is not valid.");
                        }
                    }

                    if (optOom.HasValue())
                    {
                        InMemoryLoading = false;
                    }

                    if (optArray.HasValue())
                    {
                        JsonArrayCollapse = false;
                    }

                    if (optHex.HasValue())
                    {
                        JsonUseHexNumber = true;
                    }

                    if (optDisableFlattenArray.HasValue())
                    {
                        FlattenArrayByDefault = false;
                    }

                    string bodyPath = null;
                    if (optBody.HasValue())
                    {
                        bodyPath = optBody.Value();
                    }

                    //bool noFolder = optNoFolder.HasValue();
                    bool extractAll = optExtractAll.HasValue();
                    var outputRaw = optRaw.HasValue();
                    bool enableParallel = FastMode;
                    if (optNoParallel.HasValue())
                    {
                        enableParallel = false;
                    }

                    string key = optMdfKey.HasValue() ? optMdfKey.Value() : null;
                    //string seed = optMdfSeed.HasValue() ? optMdfSeed.Value() : null;
                    if (string.IsNullOrEmpty(key))
                    {
                        throw new ArgumentNullException(nameof(key), "No key or seed specified.");
                    }

                    int keyLen = optMdfKeyLen2.HasValue() ? optMdfKeyLen2.ParsedValue : 0x83;
                    Dictionary<string, object> context = new Dictionary<string, object>();

                    if (keyLen >= 0)
                    {
                        context[Context_MdfKeyLength] = (uint) keyLen;
                    }

                    string outputFolder = null;
                    if (optOutputFolder.HasValue())
                    {
                        outputFolder = ResolveOutputFolder(optOutputFolder.Value());
                    }

                    Stopwatch sw = Stopwatch.StartNew();
                    foreach (var s in argPsbPaths.Values)
                    {
                        PsbDecompiler.ExtractArchive(s, key, context, bodyPath, outputRaw, extractAll, enableParallel, outputFolder);
                    }

                    sw.Stop();
                    Console.WriteLine($"Process time: {sw.Elapsed:g}");
                });
            });

            app.OnExecute(() =>
            {
                if (optEncoding.HasValue())
                {
                    try
                    {
                        Encoding encoding = Encoding.GetEncoding(optEncoding.ParsedValue);
                        PsbDecompiler.Encoding = encoding;
                    }
                    catch (ArgumentException e)
                    {
                        Console.WriteLine($"[WARN] Encoding {optEncoding.Value()} is not valid.");
                    }
                }

                if (optOom.HasValue())
                {
                    InMemoryLoading = false;
                }

                if (optArray.HasValue())
                {
                    JsonArrayCollapse = false;
                }

                if (optHex.HasValue())
                {
                    JsonUseHexNumber = true;
                }

                if (optDisableFlattenArray.HasValue())
                {
                    FlattenArrayByDefault = false;
                }

                Dictionary<string, object> context = new();

                if (optMdfSeed.HasValue())
                {
                    context[Context_MdfKey] = optMdfSeed.ParsedValue;
                }

                if (optMdfKeyLen.HasValue())
                {
                    context[Context_MdfKeyLength] = optMdfKeyLen.ParsedValue;
                }

                if (optDisableCombinedImage.HasValue())
                {
                    context[Context_DisableCombinedImage] = true;
                }

                bool useRaw = optRaw.HasValue();
                uint? key = optKey.HasValue() ? optKey.ParsedValue : (uint?) null;

                PsbType type = PsbType.PSB;
                if (optType.HasValue())
                {
                    type = optType.ParsedValue;
                }

                string outputFolder = null;
                if (optOutputPath.HasValue())
                {
                    outputFolder = ResolveOutputFolder(optOutputPath.Value());
                }

                foreach (var s in argPath.Values)
                {
                    if (File.Exists(s))
                    {
                        Decompile(s, useRaw, PsbImageFormat.png, key, type, context, outputFolder);
                    }
                    else if (Directory.Exists(s))
                    {
                        foreach (var file in FreeMoteExtension.GetFiles(s,
                                     new[] {"*.psb", "*.mmo", "*.pimg", "*.scn", "*.dpak", "*.psz", "*.psp", "*.bytes", "*.m"}))
                        {
                            Decompile(file, useRaw, PsbImageFormat.png, key, type, context, outputFolder);
                        }
                    }
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

        private static string PrintHelp()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine().AppendLine("Plugins:");
            sb.AppendLine(FreeMount.PrintPluginInfos(2));
            sb.AppendLine(@"Examples: 
  PsbDecompile -k 123456789 sample.psb");
            return sb.ToString();
        }

        private static bool IsOutputOptionAllowed()
        {
            var licensePath = Path.Combine(AppContext.BaseDirectory, "FreeMote.LICENSE.txt");
            return File.Exists(licensePath);
        }

        private static string ResolveOutputFolder(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return null;
            }

            if (!IsOutputOptionAllowed())
            {               
                return null;
            }

            if (File.Exists(outputPath))
            {
                Logger.LogWarn($"[WARN] Output path is a file: {outputPath}. Use a folder path instead.");
                return null;
            }

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            return Path.GetFullPath(outputPath);
        }

        private static string GetOutputJsonPath(string inputPath, string outputFolder)
        {
            var fileName = Path.GetFileName(inputPath);
            string outputFileName;
            if (fileName.EndsWith(".m", StringComparison.OrdinalIgnoreCase))
            {
                outputFileName = fileName + ".json";
            }
            else
            {
                outputFileName = Path.ChangeExtension(fileName, ".json");
            }

            return Path.Combine(outputFolder, outputFileName);
        }

        static void Decompile(string path, bool keepRaw = false, PsbImageFormat format = PsbImageFormat.png,
            uint? key = null, PsbType type = PsbType.PSB, Dictionary<string, object> context = null, string outputFolder = null)
        {
            if (path.ToLowerInvariant().EndsWith("_body.bin"))
            {
                var dir = Path.GetDirectoryName(path);
                var packageName = PsbExtension.ArchiveInfo_GetPackageNameFromBodyBin(Path.GetFileName(path)) + "_info.psb.m";
                var infoPath = Path.Combine(dir ?? "", packageName);
                if (File.Exists(infoPath))
                {
                    Logger.LogWarn($"[WARN] It seems that you are trying to decompile a body.bin which is NOT supported. You should extract body.bin by `info-psb {packageName}` command instead.");
                }
            }

            var name = Path.GetFileNameWithoutExtension(path);
            Console.WriteLine($"Decompiling: {name}");

#if !DEBUG
            try
#endif
            {
                PSB psb;
                if (string.IsNullOrEmpty(outputFolder))
                {
                    var result = keepRaw
                        ? PsbDecompiler.DecompileToFile(path, key: key, type: type)
                        : PsbDecompiler.DecompileToFile(path, PsbExtractOption.Extract, format, key: key, type: type,
                            contextDic: context);
                    psb = result.Psb;
                }
                else
                {
                    var outputPath = GetOutputJsonPath(path, outputFolder);
                    var decompileContext = context;
                    if (key != null)
                    {
                        decompileContext ??= new Dictionary<string, object>();
                        decompileContext[Context_CryptKey] = key;
                    }

                    PsbDecompiler.Decompile(path, out psb, decompileContext, type);
                    if (type != PsbType.PSB)
                    {
                        psb.Type = type;
                    }

                    var extractOption = keepRaw ? PsbExtractOption.Original : PsbExtractOption.Extract;
                    PsbDecompiler.DecompileToFile(psb, outputPath, decompileContext, extractOption, format, true, key);
                }
                if (psb.Type == PsbType.ArchiveInfo)
                {
                    Logger.LogWarn(
                        $"[INFO] {name} is an Archive Info PSB. Use `info-psb` command on this PSB to extract content from body.bin .");
                }
            }
#if !DEBUG
            catch (PsbBadFormatException psbBadFormatException)
            {
                if (psbBadFormatException.Reason == PsbBadFormatReason.Body)
                {
                    Console.WriteLine("[ERROR] Input file is not a PSB; Or maybe PSB is encrypted, use `-k` option with a valid key to decrypt it.");
                }

                Console.WriteLine(psbBadFormatException);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
#endif
        }
    }
}