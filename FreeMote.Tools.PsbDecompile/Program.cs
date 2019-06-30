using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FreeMote.Plugins;
using FreeMote.Psb;
using FreeMote.PsBuild;
using McMaster.Extensions.CommandLineUtils;

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

            PsbConstants.InMemoryLoading = true;
            Console.WriteLine();

            var app = new CommandLineApplication();
            app.OptionsComparison = StringComparison.OrdinalIgnoreCase;

            //help
            app.HelpOption(); //do not inherit
            app.ExtendedHelpText = PrintHelp();
            
            //options
            var optKey = app.Option<uint>("-k|--key", "Set PSB key (uint, dec)", CommandOptionType.SingleValue);
            var optFormat = app.Option<PsbImageFormat>("-e|--extract <FORMAT>",
                "Convert textures to Png/Bmp. Default=Png", CommandOptionType.SingleValue, true);
            var optRaw = app.Option("-raw|--raw", "Keep raw textures", CommandOptionType.NoValue);
            //メモリ足りない もうどうしよう : https://soundcloud.com/ulysses-wu/Heart-Chrome
            var optOom = app.Option("-oom|--memory-limit", "Disable In-Memory Loading", CommandOptionType.NoValue);

            var optHex = app.Option("-hex|--json-hex", "(Json) Use hex numbers", CommandOptionType.NoValue, true);
            var optArray = app.Option("-indent|--json-array-indent", "(Json) Indent arrays", CommandOptionType.NoValue, true);


            //args
            var argPath =
                app.Argument("Files", "File paths", multipleValues: true);
            
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
                    PsbImageFormat format = optFormat.HasValue() ? optFormat.ParsedValue : PsbImageFormat.Png;
                    //var order = optOrder.HasValue() ? optOrder.ParsedValue : PsbLinkOrderBy.Name;
                    var psbPaths = argPsbPath.Values;
                    foreach (var psbPath in psbPaths)
                    {
                        if (File.Exists(psbPath))
                        {
                            try
                            {
                                PsbDecompiler.UnlinkToFile(psbPath, format: format);
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
  PsbDecompile info-psb -k 1234567890ab -l 131 sample_info.psb.m
  PsbDecompile info-psb -s 1234567890absample_info.psb.m -l 131 sample_info.psb
  Hint: The body.bin should exist in the same folder and keep both file names correct.
";
                //options
                //var optMdfSeed = archiveCmd.Option("-s|--seed <SEED>",
                //    "Set complete seed (Key+FileName)",
                //    CommandOptionType.SingleValue);
                var optExtractAll = archiveCmd.Option("-a|--all",
                    "Also decompile all contents if possible",
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
                    bool extractAll = optExtractAll.HasValue();
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
                        context["MdfKeyLength"] = (uint)keyLen;
                    }

                    foreach (var s in argPsbPaths.Values)
                    {
                        if (File.Exists(s))
                        {
                            var fileName = Path.GetFileName(s);
                            context["MdfKey"] = key + fileName;

                            try
                            {
                                var dir = Path.GetDirectoryName(Path.GetFullPath(s));
                                var name = fileName.Substring(0, fileName.IndexOf("_info."));
                                var body = Path.Combine(dir, name + "_body.bin");
                                if (!File.Exists(body))
                                {
                                    Console.WriteLine($"Can not find body: {body}");
                                    continue;
                                }

                                PSB psb = null;
                                using (var fs = File.OpenRead(s))
                                {
                                    psb = new PSB(MdfConvert(fs, context));
                                }

                                File.WriteAllText(Path.GetFullPath(s) + ".json", PsbDecompiler.Decompile(psb));
                                PsbResourceJson resx = new PsbResourceJson(psb, context);
                                File.WriteAllText(Path.GetFullPath(s) + "resx.json", resx.SerializeToJson());

                                var dic = psb.Objects["file_info"] as PsbDictionary;
                                var suffixList = ((PsbCollection)psb.Objects["expire_suffix_list"]);
                                var suffix = "";
                                if (suffixList.Count > 0)
                                {
                                    suffix = suffixList[0] as PsbString ?? "";
                                }

                                Console.WriteLine($"Extracting info from {fileName} ...");

                                var bodyBytes = File.ReadAllBytes(body);
                                var extractDir = Path.Combine(dir, name);
                                if (!Directory.Exists(extractDir))
                                {
                                    Directory.CreateDirectory(extractDir);
                                }

                                foreach (var pair in dic)
                                {
                                    Console.WriteLine($"{(extractAll? "Decompiling": "Extracting")} {pair.Key} ...");
                                    var range = ((PsbCollection)pair.Value);
                                    var start = ((PsbNumber)range[0]).IntValue;
                                    var len = ((PsbNumber)range[1]).IntValue;

                                    using (var ms = new MemoryStream(bodyBytes, start, len))
                                    {
                                        context["MdfKey"] = key + pair.Key + suffix;
                                        var mms = MdfConvert(ms, context);
                                        if (extractAll)
                                        {
                                            try
                                            {
                                                PSB bodyPsb = new PSB(mms);

                                            }
                                            catch (Exception e)
                                            {
                                                Console.WriteLine($"Decompile failed: {pair.Key}");
                                                File.WriteAllBytes(Path.Combine(extractDir, pair.Key + suffix), mms.ToArray());
                                            }
                                        }
                                        else
                                        {
                                            File.WriteAllBytes(Path.Combine(extractDir, pair.Key + suffix), mms.ToArray());
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                        }
                    }
                });
            });

            app.OnExecute(() =>
            {
                if (optOom.HasValue())
                {
                    PsbConstants.InMemoryLoading = false;
                }

                if (optArray.HasValue())
                {
                    PsbConstants.JsonArrayCollapse = false;
                }

                if (optHex.HasValue())
                {
                    PsbConstants.JsonUseHexNumber = true;
                }

                bool useRaw = optRaw.HasValue();
                PsbImageFormat format = optFormat.HasValue() ? optFormat.ParsedValue : PsbImageFormat.Png;
                uint? key = optKey.HasValue() ? optKey.ParsedValue : (uint?) null;

                foreach (var s in argPath.Values)
                {
                    if (File.Exists(s))
                    {
                        Decompile(s, useRaw, format, key);
                    }
                    else if (Directory.Exists(s))
                    {
                        foreach (var file in Directory.EnumerateFiles(s, "*.psb")
                            .Union(Directory.EnumerateFiles(s, "*.mmo"))
                            .Union(Directory.EnumerateFiles(s, "*.pimg"))
                            .Union(Directory.EnumerateFiles(s, "*.scn"))
                            .Union(Directory.EnumerateFiles(s, "*.dpak"))
                            .Union(Directory.EnumerateFiles(s, "*.psz"))
                            .Union(Directory.EnumerateFiles(s, "*.psp"))
                        )
                        {
                            Decompile(s, useRaw, format, key);
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
  PsbDecompile -e bmp -k 123456789 sample.psb");
            return sb.ToString();

            //            Console.WriteLine("Usage: .exe [Mode] [Setting] <PSB path>");
            //            Console.WriteLine(@"Mode:
            //-raw : Keep resource in original format.
            //-er : Similar to raw mode but uncompress those compressed resources.
            //-eb : Convert images to BMP format.
            //-ep : [Default] Convert images to PNG format.
            //Setting:
            //-oom : Disable In-Memory Loading. (Lower memory usage but longer time for loading)
            //-k<Key> : Set PSB key. use `-k` (without key specified) to reset.
            //");
            //            Console.WriteLine("Example: PsbDecompile -ep emt.pure.psb");
            //            Console.WriteLine("\t PsbDecompile C:\\\\EMTfolder");
        }
        private static MemoryStream MdfConvert(Stream stream, Dictionary<string, object> context = null)
        {
            var ctx = FreeMount.CreateContext(context);
            string currentType = "MDF";
            var ms = ctx.OpenFromShell(stream, ref currentType);
            return ms;
        }

        static void Decompile(string path, bool keepRaw = false, PsbImageFormat format = PsbImageFormat.Png,
            uint? key = null)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            Console.WriteLine($"Decompiling: {name}");

#if !DEBUG
            try
#endif
            {
                if (keepRaw)
                {
                    PsbDecompiler.DecompileToFile(path, key: key);
                }
                else
                {
                    PsbDecompiler.DecompileToFile(path, PsbImageOption.Extract, format, key: key);
                }
            }
#if !DEBUG
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
#endif
        }
    }
}