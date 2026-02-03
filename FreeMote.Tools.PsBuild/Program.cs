using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using FreeMote.Plugins;
using FreeMote.Psb;
using FreeMote.PsBuild;
using McMaster.Extensions.CommandLineUtils;
using static FreeMote.Consts;

namespace FreeMote.Tools.PsBuild
{
    class Program
    {
        private static Encoding _encoding = Encoding.UTF8;
        //private static PsbPixelFormat _pixelFormat = PsbPixelFormat.None;
        private static readonly HashSet<string> _allowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".psb", ".psb.m", ".m", ".mmo", ".pimg", ".scn", ".bin", ".mtn", ".lz4",
            ".bmpfont", ".tlg", ".tlg5", ".tlg6", ".bytes", ".psz", ".dpak", ".emtbytes", ".emtproj", ".mdf", ".mpd", ".map", ".ks.scn", ".psd"
        };

        static void Main(string[] args)
        {
            Console.WriteLine("FreeMote PSB Compiler");
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
            app.HelpOption();
            app.ExtendedHelpText = PrintHelp();

            //options
            var optVer = app.Option<ushort>("-v|--ver <VER>", "Set PSB version [2,4]. Default=3",
                CommandOptionType.SingleValue);
            var optKey = app.Option<uint>("-k|--key <KEY>", "Set PSB key (uint, dec)", CommandOptionType.SingleValue);
            var optSpec = app.Option<PsbSpec>("-p|--spec <SPEC>", "Set PSB platform (krkr/common/win/ems)",
                CommandOptionType.SingleValue);
            var optNoRename = app.Option("-nr|--no-rename",
                "Prevent output file renaming, may overwrite your original PSB files!", CommandOptionType.NoValue);
            var optNoShell = app.Option("-ns|--no-shell", "Prevent shell packing (compression)", CommandOptionType.NoValue);
            var optDouble = app.Option("-double|--json-double", "(Json) Use double numbers only (no float)",
                CommandOptionType.NoValue, true);
            var optEncoding = app.Option<string>("-e|--encoding <ENCODING>", "Set encoding (e.g. SHIFT-JIS). Default=UTF-8",
                CommandOptionType.SingleValue, inherited: true);
            var optOutputPath =
             app.Option<string>("-o|--output", "Set output file path. May overwrite your original PSB files!", CommandOptionType.SingleValue);
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
                    if (optEncoding.HasValue())
                    {
                        try
                        {
                            _encoding = Encoding.GetEncoding(optEncoding.ParsedValue);
                            PsbCompiler.Encoding = _encoding;
                        }
                        catch (ArgumentException e)
                        {
                            Console.WriteLine($"[WARN] Encoding {optEncoding.Value()} is not valid.");
                        }
                    }

                    var order = optOrder.HasValue() ? optOrder.ParsedValue : PsbLinkOrderBy.Name;
                    var psbPath = argPsbPath.Value;
                    var texPaths = argTexPaths.Values;
                    var outputPath = optOutputPath.HasValue() ? ResolveOutputPath(optOutputPath.Value()) : null;
                    Link(psbPath, texPaths, order, outputPath);
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
                var optEnableResolution = portCmd.Option<bool>("-r|--resolution",
                    "Enable resolution support (may scaling images, quality is not guaranteed)", CommandOptionType.NoValue);
                //args
                var argPsbPath = portCmd.Argument("PSB", "PSB Path", multipleValues: true).IsRequired();

                portCmd.OnExecute(() =>
                {
                    if (optEncoding.HasValue())
                    {
                        try
                        {
                            _encoding = Encoding.GetEncoding(optEncoding.ParsedValue);
                            PsbCompiler.Encoding = _encoding;
                        }
                        catch (ArgumentException e)
                        {
                            Console.WriteLine($"[WARN] Encoding {optEncoding.Value()} is not valid.");
                        }
                    }

                    var portSpec = optPortSpec.ParsedValue;
                    var psbPaths = argPsbPath.Values;
                    var enableResolution = optEnableResolution.HasValue();
                    var outputPath = optOutputPath.HasValue() ? ResolveOutputPath(optOutputPath.Value()) : null;
                    foreach (var s in psbPaths)
                    {
                        if (File.Exists(s))
                        {
                            Port(s, portSpec, enableResolution, outputPath);
                        }
                        else
                        {
                            Console.WriteLine($"Input path not found: {s}");
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
                var optBodyBinName = archiveCmd.Option<string>("-b|--body <NAME>",
                    "Set body.bin file name (cannot be path). Default={xxx}_body.bin", CommandOptionType.SingleValue);
                //args
                var argPsbPaths = archiveCmd.Argument("PSB", "Archive Info PSB .json paths", true);

                archiveCmd.OnExecute(() =>
                {
                    //bool noFolder = optNoFolder.HasValue();
                    //bool noFolder = false; //not worth time to support it for now
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

                    if (optEncoding.HasValue())
                    {
                        try
                        {
                            _encoding = Encoding.GetEncoding(optEncoding.ParsedValue);
                            PsbCompiler.Encoding = _encoding;
                        }
                        catch (ArgumentException e)
                        {
                            Console.WriteLine($"[WARN] Encoding {optEncoding.Value()} is not valid.");
                        }
                    }

                    Dictionary<string, object> context = null;
                    if (optBodyBinName.HasValue() && !string.IsNullOrWhiteSpace(optBodyBinName.ParsedValue))
                    {
                        context = new Dictionary<string, object>
                        {
                            {Context_BodyBinName, optBodyBinName.ParsedValue}
                        };
                    }

                    string key = optMdfKey.HasValue() ? optMdfKey.Value() : null;
                    //string seed = optMdfSeed.HasValue() ? optMdfSeed.Value() : null;
                    int keyLen = optMdfKeyLen.HasValue() ? optMdfKeyLen.ParsedValue : 131;
                    string outputFolder = optOutputPath.HasValue() ? ResolveOutputPath(optOutputPath.Value()) : null;
                    bool hasSetOutputFolder = !string.IsNullOrEmpty(outputFolder) && Directory.Exists(outputFolder);

                    Stopwatch sw = Stopwatch.StartNew();
                    foreach (var s in argPsbPaths.Values)
                    {
                        PsbCompiler.PackArchive(s, key, intersect, preferPacked, context, enableParallel, keyLen, keepRaw, hasSetOutputFolder ? outputFolder : null);
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
                    var outputPath = Path.ChangeExtension(argPsbPath.Value, "IR.psb");
                    var savePath = GetOutputPath(outputPath, optOutputPath.HasValue() ? ResolveOutputPath(optOutputPath.Value()) : null);
                    PsbCompiler.InplaceReplaceToFile(argPsbPath.Value, argJsonPath.Value, savePath);
                    Console.WriteLine($"In-place Replace output: {savePath}");
                });
            });

            app.OnExecute(() =>
            {
                if (optEncoding.HasValue())
                {
                    try
                    {
                        _encoding = Encoding.GetEncoding(optEncoding.ParsedValue);
                        PsbCompiler.Encoding = _encoding;
                    }
                    catch (ArgumentException e)
                    {
                        Console.WriteLine($"[WARN] Encoding {optEncoding.Value()} is not valid.");
                    }
                }

                if (optDouble.HasValue())
                {
                    JsonUseDoubleOnly = true;
                }

                ushort? ver = optVer.HasValue() ? optVer.ParsedValue : null;
                uint? key = optKey.HasValue() ? optKey.ParsedValue : null;
                PsbSpec? spec = optSpec.HasValue() ? optSpec.ParsedValue : null;
                var canRename = !optNoRename.HasValue();
                var canPack = !optNoShell.HasValue();
                var outputPath = optOutputPath.HasValue() ? ResolveOutputPath(optOutputPath.Value()) : null;
                bool hasSetOutputPath = !string.IsNullOrEmpty(outputPath);
                bool hasSetOutputFolder = hasSetOutputPath && Directory.Exists(outputPath);

                if (argPath.Values.Count == 1 && hasSetOutputPath)
                {
                    Compile(argPath.Value, ver, key, spec, canRename, canPack, outputPath);
                }
                else
                {
                    foreach (var file in argPath.Values)
                    {
                        Compile(file, ver, key, spec, canRename, canPack, hasSetOutputFolder ? outputPath : null);
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

        private static string GetOutputPath(string defaultPath, string outputPath)
        {
            string savePath;
            if (!string.IsNullOrEmpty(outputPath))
            {
                if (Directory.Exists(outputPath))
                {
                    savePath = Path.Combine(outputPath, Path.GetFileName(defaultPath));
                }
                else if (_allowedExtensions.Contains(Path.GetExtension(outputPath)))
                {
                    savePath = outputPath;
                    if (File.Exists(savePath))
                    {
                        Logger.LogWarn($"[WARN] Output path already exists and will be overwritten: {savePath}");
                    }
                }
                else
                {
                    Logger.LogWarn($"[WARN] Output path is not valid: {outputPath} . Using default path: {defaultPath}");
                    savePath = defaultPath;
                }
            }
            else
            {
                savePath = defaultPath;
            }
            return savePath;
        }

        private static bool IsOutputOptionAllowed()
        {
            var licensePath = Path.Combine(AppContext.BaseDirectory, "FreeMote.LICENSE.txt");
            return File.Exists(licensePath);
        }

        private static string ResolveOutputPath(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return null;
            }

            if (!IsOutputOptionAllowed())
            {
                return null;
            }

            return outputPath;
        }

        private static void Port(string s, PsbSpec portSpec, bool resolution = false, string outputPath = null)
        {
            var name = Path.GetFileNameWithoutExtension(s);
            var ext = Path.GetExtension(s);
            Console.WriteLine($"Converting {name} to {portSpec} platform...");
            var ctx = FreeMount.CreateContext();
            using var psbStream = ctx.OpenStreamFromPsbFile(s, out var hasShell);
            PSB psb = new PSB(psbStream, true, _encoding);
            if (psb.Platform == portSpec)
            {
                Console.WriteLine("Already at the same platform, Skip.");
            }
            else
            {
                PsbSpecConverter.EnableResolution = resolution;
                psb.SwitchSpec(portSpec);
                psb.Merge();
                var savePath = Path.ChangeExtension(s, $".{portSpec}{psb.Type.DefaultExtension()}");
                savePath = GetOutputPath(savePath, outputPath);
                File.WriteAllBytes(savePath, psb.Build());
                Console.WriteLine($"Convert output: {savePath}");
            }
        }

        private static void Link(string psbPath, List<string> texPaths, PsbLinkOrderBy order, string outputPath = null)
        {
            if (!File.Exists(psbPath))
            {
                return;
            }

            var name = Path.GetFileNameWithoutExtension(psbPath);
            var ext = Path.GetExtension(psbPath);
            string savePath = string.Empty;
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

                var ctx = FreeMount.CreateContext();
                using var psbStream = ctx.OpenStreamFromPsbFile(psbPath, out var hasShell);
                PSB psb = new PSB(psbStream, true, _encoding);
                psb.Link(texs, order: order, isExternal: true);
                psb.Merge();
                if (hasShell)
                {
                    ext = psb.Type.DefaultExtension();
                }
                savePath = Path.ChangeExtension(psbPath, "linked" + ext);
                savePath = GetOutputPath(savePath, outputPath);
                File.WriteAllBytes(savePath, psb.Build());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return;
            }

            Console.WriteLine($"Link output: {savePath}");
        }

        private static void Compile(string s, ushort? version, uint? key, PsbSpec? spec, bool canRename,
            bool canPackShell, string outputPath = null)
        {
            if (!File.Exists(s))
            {
                //此処にいて何処にもいない　キミの面影はいつも朧 : https://soundcloud.com/yuhyuhyuhxibbd2/parallel-utau
                return;
            }

            var name = Path.GetFileNameWithoutExtension(s);
            var ext = Path.GetExtension(s);

            if (name.Contains("_info.psb.m"))
            {
                Logger.LogWarn(
                    "[WARN] It seems that you are going to compile a info.psb.m directly.\r\nIf you want to pack the folder generated by `PsbDecompile info-psb`, you should use `PsBuild info-psb` command instead.");
            }

            //var filename = name + (_key == null ? _noRename ? ".psb" : "-pure.psb" : "-impure.psb");
            var filename = name + ".psb"; //rename later
            var savePath = GetOutputPath(filename, outputPath);
            if (!string.IsNullOrEmpty(outputPath))
            {
                canRename = false;
            }
            Console.WriteLine($"Compiling {name} ...");
            try
            {
                PsbCompiler.CompileToFile(s, savePath, null, version, key, spec, canRename, canPackShell);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Compile {name} failed.\r\n{e}");
            }

            Console.WriteLine(!string.IsNullOrEmpty(outputPath)? $"Compile output: {savePath}" : $"Compile {name} done.");
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
    }
}