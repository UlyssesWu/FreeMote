using System;
using System.IO;
using System.Linq;
using System.Text;
using FreeMote.Plugins;
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
                "Convert textures to Png/Bmp. Default=Png", CommandOptionType.SingleValue);
            var optRaw = app.Option("-raw|--raw", "Keep raw textures", CommandOptionType.NoValue);
            //メモリ足りない もうどうしよう : https://soundcloud.com/ulysses-wu/Heart-Chrome
            var optOom = app.Option("-oom|--memory-limit", "Disable In-Memory Loading", CommandOptionType.NoValue);

            var optHex = app.Option("-hex|--json-hex", "(Json) Use hex numbers", CommandOptionType.NoValue, true);
            var optArray = app.Option("--array-indent", "(Json) Indent arrays", CommandOptionType.NoValue, true);

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
                    //var order = optOrder.HasValue() ? optOrder.ParsedValue : PsbLinkOrderBy.Name;
                    var psbPaths = argPsbPath.Values;
                    foreach (var psbPath in psbPaths)
                    {
                        if (File.Exists(psbPath))
                        {
                            try
                            {
                                PsbDecompiler.UnlinkToFile(psbPath);
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