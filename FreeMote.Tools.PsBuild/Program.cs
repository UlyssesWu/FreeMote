using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreeMote.Plugins;
using FreeMote.Psb;
using FreeMote.PsBuild;
using McMaster.Extensions.CommandLineUtils;

namespace FreeMote.Tools.PsBuild
{
    class Program
    {
        //private static PsbPixelFormat _pixelFormat = PsbPixelFormat.None;

        static void Main(string[] args)
        {
            Console.WriteLine("FreeMote PSB Compiler");
            Console.WriteLine("by Ulysses, wdwxy12345@gmail.com");

            FreeMount.Init();
            Console.WriteLine($"{FreeMount.PluginsCount} Plugins Loaded.");

            PsbConstants.InMemoryLoading = true;
            Console.WriteLine();

            var app = new CommandLineApplication();
            app.OptionsComparison = StringComparison.OrdinalIgnoreCase;

            //help
            app.HelpOption();
            app.ExtendedHelpText = PrintHelp();

            //options
            var optVer = app.Option<ushort>("-v|--ver <VER>", "Set PSB version [2,4]. Default=3", CommandOptionType.SingleValue);
            var optKey = app.Option<uint>("-k|--key <KEY>", "Set PSB key (uint, dec)", CommandOptionType.SingleValue);
            var optSpec = app.Option<PsbSpec>("-p|--spec <SPEC>", "Set PSB platform (krkr/common/win/ems)",
                CommandOptionType.SingleValue);
            var optNoRename = app.Option("-no-rename",
                "Prevent output file renaming, may overwrite your original PSB files", CommandOptionType.NoValue);
            var optNoShell = app.Option("-no-shell", "Prevent shell packing (compression)", CommandOptionType.NoValue);
            var optDouble = app.Option("-double|--json-double", "(Json) Use double numbers only (no float)", CommandOptionType.NoValue, true);

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
                    "Set texture link order (ByName/ByOrder/Convention). Default=ByName",
                    CommandOptionType.SingleValue);
                //args
                var argPsbPath = linkCmd.Argument("PSB", "PSB Path").IsRequired().Accepts(v => v.ExistingFile());
                var argTexPath = linkCmd.Argument("Textures", "Texture Paths").IsRequired();

                linkCmd.OnExecute(() =>
                {
                    var order = optOrder.HasValue() ? optOrder.ParsedValue : PsbLinkOrderBy.Name;
                    var psbPath = argPsbPath.Value;
                    var texPaths = argTexPath.Values;
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
  Hint: If there are `a.scn.m` and `a.scn.m.json` in the same position, `.json` will be selected (unless `-p`).
";
                //options
                //var optMdfSeed = archiveCmd.Option("-s|--seed <SEED>",
                //    "Set complete seed (Key+FileName)",
                //    CommandOptionType.SingleValue);
                var optIntersect = archiveCmd.Option("-i|--intersect",
                    "Only pack files which existed in info.psb.m",
                    CommandOptionType.NoValue);
                var optPacked = archiveCmd.Option("-p|--packed",
                    "Prefer PSB files rather than json files in folder",
                    CommandOptionType.NoValue);
                var optMdfKey = archiveCmd.Option("-k|--key <KEY>",
                    "Set key (Infer file name from path)",
                    CommandOptionType.SingleValue);
                var optMdfKeyLen = archiveCmd.Option<int>("-l|--length <LEN>",
                    "Set key length",
                    CommandOptionType.SingleValue);
                var optInfoOom = archiveCmd.Option("-1by1|--enumerate",
                    "Disable parallel processing when using `-a` (can be very slow)", CommandOptionType.NoValue);
                
                //args
                var argPsbPaths = archiveCmd.Argument("PSB", "Archive Info PSB .json paths", true);

                archiveCmd.OnExecute(() =>
                {
                    bool intersect = optIntersect.HasValue();
                    bool preferPacked = optPacked.HasValue();
                    bool enableParallel = PsbConstants.FastMode;
                    if (optInfoOom.HasValue())
                    {
                        enableParallel = false;
                    }
                    
                    string key = optMdfKey.HasValue() ? optMdfKey.Value() : null;
                    //string seed = optMdfSeed.HasValue() ? optMdfSeed.Value() : null;

                    int keyLen = optMdfKeyLen.HasValue() ? optMdfKeyLen.ParsedValue : -1;
                    Dictionary<string, object> context = new Dictionary<string, object>();

                    if (keyLen >= 0)
                    {
                        context["MdfKeyLength"] = (uint)keyLen;
                    }

                    foreach (var s in argPsbPaths.Values)
                    {
                        if (!File.Exists(s)) continue;
                        PSB infoPsb = PsbCompiler.LoadPsbFromJsonFile(s);
                        if (infoPsb.Type != PsbType.ArchiveInfo)
                        {
                            continue;
                        }
                        //TODO: How to get context?

                        var fileName = Path.GetFileName(s);
                        throw new NotImplementedException("This feature is not finished.");
                    }
                });
            });

            app.OnExecute(() =>
            {
                if (optDouble.HasValue())
                {
                    PsbConstants.JsonUseDoubleOnly = true;
                }

                ushort ver = optVer.HasValue() ? optVer.ParsedValue : (ushort) 3;
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
                File.WriteAllBytes(Path.ChangeExtension(s, $".{portSpec}.psb"), psb.Build());
                Console.WriteLine($"Convert {name} succeed.");
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

            Console.WriteLine($"Link {name} succeed.");
        }

        private static void Compile(string s, ushort version, uint? key, PsbSpec? spec, bool canRename,
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
                var filename = name + ".psb";
                PsbCompiler.CompileToFile(s, filename, null, version, key, spec, canRename,
                    canPackShell);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Compile {name} failed.\r\n{e}");
            }

            Console.WriteLine($"Compile {name} succeed.");
        }

        private static string PrintHelp()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine().AppendLine("Plugins:");
            sb.AppendLine(FreeMount.PrintPluginInfos(2));
            sb.AppendLine(@"Examples: 
  PsBuild -v 4 -k 123456789 -p krkr sample.psb.json");
            return sb.ToString();

//            Console.WriteLine("Usage: .exe [Param] <PSB json path>");
//            Console.WriteLine(@"Param:
//-v<VerNumber> : Set compile version from [2,4] . Default: 3.
//-k<CryptKey> : Set CryptKey. Default: none(Pure PSB). Requirement: uint, dec.
//-p<Platform> : Set platform. Default: keep original platform. Support: krkr/win/common/ems.
//    Warning: Platform ONLY works with .bmp/.png format textures.
//-no-shell : Do not compress PSB to shell types even if shell type is specified in resx.json.
//-no-rename : Compiled filename will be same as the json filename (with .psb extension).
//    Warning: This setting may overwrite your original PSB files!
//");
////-no-key : Ignore any key setting and output pure PSB.
            //Console.WriteLine("Example: PsBuild -v4 -k123456789 -pkrkr sample.psb.json");
        }
    }
}