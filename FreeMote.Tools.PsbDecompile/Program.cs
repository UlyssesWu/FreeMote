﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading.Tasks;
using FreeMote.Plugins;
using FreeMote.Psb;
using FreeMote.PsBuild;
using McMaster.Extensions.CommandLineUtils;
using static FreeMote.Consts;

namespace FreeMote.Tools.PsbDecompile
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("FreeMote PSB Decompiler");
            Console.WriteLine("by Ulysses, wdwxy12345@gmail.com");

            FreeMount.Init();
            Console.WriteLine($"{FreeMount.PluginsCount} Plugins Loaded.");

            InMemoryLoading = true;
            Console.WriteLine();

            var app = new CommandLineApplication();
            app.OptionsComparison = StringComparison.OrdinalIgnoreCase;

            //help
            app.HelpOption(); //do not inherit
            app.ExtendedHelpText = PrintHelp();

            //options
            var optKey = app.Option<uint>("-k|--key", "Set PSB key (uint, dec)", CommandOptionType.SingleValue);
            //var optFormat = app.Option<PsbImageFormat>("-e|--extract <FORMAT>",
            //    "Convert textures to png/bmp. Default=png", CommandOptionType.SingleValue, true);
            var optRaw = app.Option("-raw|--raw", "Output raw resources", CommandOptionType.NoValue, inherited: true);
            //メモリ足りない もうどうしよう : https://soundcloud.com/ulysses-wu/Heart-Chrome
            var optOom = app.Option("-oom|--memory-limit", "Disable In-Memory Loading", CommandOptionType.NoValue,
                inherited: true);
            var optNoParallel = app.Option("-1by1|--enumerate",
                "Disable parallel processing (can be very slow)", CommandOptionType.NoValue, inherited: true);
            var optHex = app.Option("-hex|--json-hex", "(Json) Use hex numbers", CommandOptionType.NoValue, true);
            var optArray = app.Option("-indent|--json-array-indent", "(Json) Indent arrays", CommandOptionType.NoValue,
                true);
            var optDisableFlattenArray = app.Option("-dfa|--disable-flatten-array",
                "Disable represent extra resource as flatten arrays", CommandOptionType.NoValue, inherited: true);
            var optType = app.Option<PsbType>("-t|--type <TYPE>", "Set PSB type manually",
                CommandOptionType.SingleValue, inherited: true);

            //args
            var argPath =
                app.Argument("Files", "File paths", multipleValues: true);

            //command: image
            app.Command("image", imageCmd =>
            {
                //help
                imageCmd.Description = "Extract (combined) textures from PSBs";
                imageCmd.HelpOption();
                imageCmd.ExtendedHelpText = @"
Example:
  PsbDecompile image sample.psb
  PsbDecompile image sample-resource-folder
";
                //args
                var argPsbPath = imageCmd.Argument("Path", "PSB paths or PSB directory paths").IsRequired();

                imageCmd.OnExecute(() =>
                {
                    var enableParallel = !optNoParallel.HasValue();
                    var psbPaths = argPsbPath.Values;

                    foreach (var psbPath in psbPaths)
                    {
                        if (File.Exists(psbPath))
                        {
                            try
                            {
                                PsbDecompiler.ExtractImageFiles(psbPath);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                        }
                        else if (Directory.Exists(psbPath))
                        {
                            var files = PsbExtension.GetFiles(psbPath, new[] {"*.psb", "*.pimg", "*.m", "*.bytes"});

                            if (enableParallel)
                            {
                                Parallel.ForEach(files, (s, state) =>
                                {
                                    try
                                    {
                                        PsbDecompiler.ExtractImageFiles(s);
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
                                        PsbDecompiler.ExtractImageFiles(s);
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
  Todo: Pack body.bin which larger than 2GB is not supported currently.
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
                var optMdfKeyLen = archiveCmd.Option<int>("-l|--length <LEN>",
                    "Set key length. Default=131",
                    CommandOptionType.SingleValue);

                //args
                var argPsbPaths = archiveCmd.Argument("PSB", "Archive Info PSB Paths", true);

                archiveCmd.OnExecute(() =>
                {
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

                    int keyLen = optMdfKeyLen.HasValue() ? optMdfKeyLen.ParsedValue : 0x83;
                    Dictionary<string, object> context = new Dictionary<string, object>();

                    if (keyLen >= 0)
                    {
                        context[Context_MdfKeyLength] = (uint) keyLen;
                    }

                    foreach (var s in argPsbPaths.Values)
                    {
                        ExtractArchive(s, key, context, outputRaw, extractAll, enableParallel);
                    }
                });
            });

            app.OnExecute(() =>
            {
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

                bool useRaw = optRaw.HasValue();
                //PsbImageFormat format = optFormat.HasValue() ? optFormat.ParsedValue : PsbImageFormat.png;
                uint? key = optKey.HasValue() ? optKey.ParsedValue : (uint?) null;

                PsbType type = PsbType.PSB;
                if (optType.HasValue())
                {
                    type = optType.ParsedValue;
                }
                foreach (var s in argPath.Values)
                {
                    if (File.Exists(s))
                    {
                        Decompile(s, useRaw, PsbImageFormat.png, key, type);
                    }
                    else if (Directory.Exists(s))
                    {
                        foreach (var file in PsbExtension.GetFiles(s,
                            new[] {"*.psb", "*.mmo", "*.pimg", "*.scn", "*.dpak", "*.psz", "*.psp", "*.bytes", "*.m"}))
                        {
                            Decompile(s, useRaw, PsbImageFormat.png, key, type);
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

        private static MemoryStream MdfConvert(Stream stream, string shellType, Dictionary<string, object> context = null)
        {
            var ctx = FreeMount.CreateContext(context);
            var ms = ctx.OpenFromShell(stream, ref shellType);
            if (ms != stream)
            {
                stream.Dispose();
            }

            return ms;
        }

        static void Decompile(string path, bool keepRaw = false, PsbImageFormat format = PsbImageFormat.png,
            uint? key = null, PsbType type = PsbType.PSB)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            Console.WriteLine($"Decompiling: {name}");

#if !DEBUG
            try
#endif
            {
                if (keepRaw)
                {
                    PsbDecompiler.DecompileToFile(path, key: key, type: type);
                }
                else
                {
                    PsbDecompiler.DecompileToFile(path, PsbExtractOption.Extract, format, key: key, type: type);
                }
            }
#if !DEBUG
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
#endif
        }

        static void ExtractArchive(string filePath, string key, Dictionary<string, object> context, bool outputRaw = true,
            bool extractAll = false, bool enableParallel = true)
        {
            if (File.Exists(filePath))
            {
                var fileName = Path.GetFileName(filePath);
                context[Context_MdfKey] = key + fileName;

                var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
                var name = PsbExtension.ArchiveInfoGetPackageName(fileName);
                if (name == null)
                {
                    Console.WriteLine($"File name incorrect: {fileName}");
                    name = fileName;
                }

                var body = Path.Combine(dir ?? "", name + "_body.bin");
                bool hasBody = true;
                if (!File.Exists(body))
                {
                    Console.WriteLine($"Can not find body: {body}");
                    hasBody = false;
                }

                try
                {
                    var baseShellType = Path.GetExtension(fileName).DefaultShellType();
                    PSB psb = null;
                    using (var fs = File.OpenRead(filePath))
                    {
                        psb = new PSB(MdfConvert(fs, baseShellType, context));
                    }

                    File.WriteAllText(Path.GetFullPath(filePath) + ".json", PsbDecompiler.Decompile(psb));
                    PsbResourceJson resx = new PsbResourceJson(psb, context);

                    var dic = psb.Objects["file_info"] as PsbDictionary;
                    var suffixList = ((PsbList) psb.Objects["expire_suffix_list"]);
                    var suffix = "";
                    if (suffixList.Count > 0)
                    {
                        suffix = suffixList[0] as PsbString ?? "";
                    }

                    var shellType = suffix.DefaultShellType();

                    if (!hasBody)
                    {
                        //Write resx.json
                        resx.Context[Context_ArchiveSource] = new List<string> {name};
                        File.WriteAllText(Path.GetFullPath(filePath) + ".resx.json", resx.SerializeToJson());
                        return;
                    }

                    Console.WriteLine($"Extracting info from {fileName} ...");

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

#if DEBUG
                    Stopwatch sw = Stopwatch.StartNew();
#endif

                    if (enableParallel) //parallel!
                    {
                        int count = 0;

                        using var mmFile =
                            MemoryMappedFile.CreateFromFile(body, FileMode.Open, name, 0, MemoryMappedFileAccess.Read);
                        Parallel.ForEach(dic, pair =>
                        {
                            count++;
                            //Console.WriteLine($"{(extractAll ? "Decompiling" : "Extracting")} {pair.Key} ...");
                            var range = ((PsbList) pair.Value);
                            var start = ((PsbNumber) range[0]).UIntValue;
                            var len = ((PsbNumber) range[1]).IntValue;

                            using var mmAccessor = mmFile.CreateViewAccessor(start, len, MemoryMappedFileAccess.Read);
                            var bodyBytes = new byte[len];
                            mmAccessor.ReadArray(0, bodyBytes, 0, len);

                            if (outputRaw)
                            {
                                File.WriteAllBytes(Path.Combine(extractDir, pair.Key + suffix),
                                    bodyBytes);
                                return;
                            }

                            using var ms = new MemoryStream(bodyBytes);
                            var bodyContext = new Dictionary<string, object>(context)
                            {
                                [Context_MdfKey] = key + pair.Key + suffix
                            };
                            bodyContext.Remove(Context_ArchiveSource);
                            var mms = MdfConvert(ms, shellType, bodyContext);
                            if (extractAll)
                            {
                                try
                                {
                                    PSB bodyPsb = new PSB(mms);
                                    PsbDecompiler.DecompileToFile(bodyPsb,
                                        Path.Combine(extractDir, pair.Key + suffix + ".json"),
                                        bodyContext, PsbExtractOption.Extract);
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"Decompile failed: {pair.Key}");
                                    File.WriteAllBytes(Path.Combine(extractDir, pair.Key + suffix),
                                        mms.ToArray());
                                }
                            }
                            else
                            {
                                File.WriteAllBytes(Path.Combine(extractDir, pair.Key + suffix),
                                    mms.ToArray());
                            }
                        });

                        Console.WriteLine($"{count} files {(extractAll ? "decompiled" : "extracted")}.");
                    }
                    else
                    {
                        //no parallel
                        //var maxLen = dic?.Values.Max(item => item.Children(1).GetInt()) ?? 0;
                        using var mmFile =
                            MemoryMappedFile.CreateFromFile(body, FileMode.Open, name, 0, MemoryMappedFileAccess.Read);

                        foreach (var pair in dic)
                        {
                            Console.WriteLine(
                                $"{(extractAll ? "Decompiling" : "Extracting")} {pair.Key} ...");
                            var range = ((PsbList) pair.Value);
                            var start = ((PsbNumber) range[0]).IntValue;
                            var len = ((PsbNumber) range[1]).IntValue;

                            using var mmAccessor = mmFile.CreateViewAccessor(start, len, MemoryMappedFileAccess.Read);
                            var bodyBytes = new byte[len];
                            mmAccessor.ReadArray(0, bodyBytes, 0, len);

                            if (outputRaw)
                            {
                                File.WriteAllBytes(Path.Combine(extractDir, pair.Key + suffix),
                                    bodyBytes.AsSpan().Slice(start, len).ToArray());
                                continue;
                            }

                            using (var ms = new MemoryStream(bodyBytes))
                            {
                                context[Context_MdfKey] = key + pair.Key + suffix;
                                var mms = MdfConvert(ms, shellType, context);
                                if (extractAll)
                                {
                                    try
                                    {
                                        PSB bodyPsb = new PSB(mms);
                                        PsbDecompiler.DecompileToFile(bodyPsb,
                                            Path.Combine(extractDir, pair.Key + suffix + ".json"), context,
                                            PsbExtractOption.Extract);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine($"Decompile failed: {pair.Key}");
                                        File.WriteAllBytes(Path.Combine(extractDir, pair.Key + suffix),
                                            mms.ToArray());
                                    }
                                }
                                else
                                {
                                    File.WriteAllBytes(Path.Combine(extractDir, pair.Key + suffix),
                                        mms.ToArray());
                                }
                            }
                        }
                    }

                    //Write resx.json
                    resx.Context[Context_ArchiveSource] = new List<string> {name};
                    resx.Context[Context_MdfMtKey] = key;
                    File.WriteAllText(Path.GetFullPath(filePath) + ".resx.json", resx.SerializeToJson());
#if DEBUG
                    sw.Stop();
                    Console.WriteLine($"Process time: {sw.Elapsed:g}");
#endif
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
#if DEBUG
                    throw e;
#endif
                }
            }
        }
    }
}