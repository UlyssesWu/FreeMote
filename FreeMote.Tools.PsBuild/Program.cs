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
using static FreeMote.Consts;
using static FreeMote.Psb.PsbExtension;

namespace FreeMote.Tools.PsBuild
{
    class Program
    {
        private static Encoding _encoding = Encoding.UTF8;
        //private static PsbPixelFormat _pixelFormat = PsbPixelFormat.None;

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
                "Prevent output file renaming, may overwrite your original PSB files", CommandOptionType.NoValue);
            var optNoShell = app.Option("-ns|--no-shell", "Prevent shell packing (compression)", CommandOptionType.NoValue);
            var optDouble = app.Option("-double|--json-double", "(Json) Use double numbers only (no float)",
                CommandOptionType.NoValue, true);
            var optEncoding = app.Option<string>("-e|--encoding <ENCODING>", "Set encoding (e.g. SHIFT-JIS). Default=UTF-8",
                CommandOptionType.SingleValue, inherited: true);
            var optFastMode = app.Option<bool>("-o0|--fast", "Disable compile optimization, good for speed but bad for output size.", CommandOptionType.NoValue, true);
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
                    foreach (var s in psbPaths)
                    {
                        if (File.Exists(s))
                        {
                            Port(s, portSpec, enableResolution);
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

                    if (optFastMode.HasValue())
                    {
                        OptimizeMode = false;
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

                    Stopwatch sw = Stopwatch.StartNew();
                    foreach (var s in argPsbPaths.Values)
                    {
                        PsbCompiler.PackArchive(s, key, intersect, preferPacked, context, enableParallel, keyLen, keepRaw);
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

                if (optFastMode.HasValue())
                {
                    OptimizeMode = false;
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

        private static void Port(string s, PsbSpec portSpec, bool resolution = false)
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
                File.WriteAllBytes(savePath, psb.Build());
                Console.WriteLine($"Convert output: {savePath}");
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

                var ctx = FreeMount.CreateContext();
                using var psbStream = ctx.OpenStreamFromPsbFile(psbPath, out var hasShell);
                PSB psb = new PSB(psbStream, true, _encoding);
                psb.Link(texs, order: order, isExternal: true);
                psb.Merge();
                if (hasShell)
                {
                    ext = psb.Type.DefaultExtension();
                }
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

            if (name.Contains("_info.psb.m"))
            {
                Logger.LogWarn(
                    "[WARN] It seems that you are going to compile a info.psb.m directly.\r\nIf you want to pack the folder generated by `PsbDecompile info-psb`, you should use `PsBuild info-psb` command instead.");
            }

            Console.WriteLine($"Compiling {name} ...");
            try
            {
                //var filename = name + (_key == null ? _noRename ? ".psb" : "-pure.psb" : "-impure.psb");
                var filename = name + ".psb"; //rename later //TODO: support set output path
                PsbCompiler.CompileToFile(s, filename, null, version, key, spec, canRename, canPackShell);
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
    }
}