using System;
using System.IO;
using System.Linq;
using FreeMote.Plugins;
using FreeMote.PsBuild;

namespace FreeMote.Tools.PsbDecompile
{
    class Program
    {
        //Not thread safe
        static bool _extractImage = true;
        static bool _uncompressImage = false;
        static bool _png = true;
        private static uint? _key = null;

        static void Main(string[] args)
        {
            Console.WriteLine("FreeMote PSB Decompiler");
            Console.WriteLine("by Ulysses, wdwxy12345@gmail.com");

            FreeMount.Init();
            Console.WriteLine($"{FreeMount.PluginsCount} Plugins Loaded.");

            PsbConstants.InMemoryLoading = true;
            Console.WriteLine();

            if (args.Length <= 0 || args[0].ToLowerInvariant() == "-h" || args[0].ToLowerInvariant() == "?")
            {
                PrintHelp();
                return;
            }

            foreach (var s in args)
            {
                // Convert resources to BMP
                if (s.ToLowerInvariant() == "-eb" || s.ToLowerInvariant() == "-extract" ||
                    s.ToLowerInvariant() == "-bmp")
                {
                    _extractImage = true;
                    _png = false;
                    continue;
                }

                // Convert resources to PNG
                if (s.ToLowerInvariant() == "-ep" || s.ToLowerInvariant() == "-png")
                {
                    _extractImage = true;
                    _png = true;
                    continue;
                }

                // Convert resources to BIN
                if (s.ToLowerInvariant() == "-er" || s.ToLowerInvariant() == "-uncompress")
                {
                    _extractImage = false;
                    _uncompressImage = true;
                    continue;
                }

                // Keep Original
                if (s.ToLowerInvariant() == "-ne" || s.ToLowerInvariant() == "-raw")
                {
                    _extractImage = false;
                    _uncompressImage = false;
                    continue;
                }

                // Disable MM IO
                //メモリ足りない もうどうしよう : https://soundcloud.com/ulysses-wu/Heart-Chrome
                if (s.ToLowerInvariant() == "-oom" || s.ToLowerInvariant() == "-low-mem")
                {
                    PsbConstants.InMemoryLoading = false;
                    continue;
                }

                //Enable MM IO
                if (s.ToLowerInvariant() == "-mem" || s.ToLowerInvariant() == "-fast")
                {
                    PsbConstants.InMemoryLoading = true;
                    continue;
                }


                if (s.StartsWith("-k"))
                {
                    if (s == "-k")
                    {
                        _key = null;
                    }

                    if (uint.TryParse(s.Replace("-k", ""), out var k))
                    {
                        _key = k;
                    }
                    else
                    {
                        _key = null;
                    }
                }

                if (File.Exists(s))
                {
                    Decompile(s, _extractImage, _uncompressImage, _png, _key);
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
                        Decompile(file, _extractImage, _uncompressImage, _png, _key);
                    }
                }
            }

            Console.WriteLine("Done.");
        }

        private static void PrintHelp()
        {
            var pluginInfo = FreeMount.PrintPluginInfos();
            if (!string.IsNullOrEmpty(pluginInfo))
            {
                Console.WriteLine(pluginInfo);
            }

            Console.WriteLine("Usage: .exe [Mode] [Setting] <PSB path>");
            Console.WriteLine(@"Mode:
-raw : Keep resource in original format.
-er : Similar to raw mode but uncompress those compressed resources.
-eb : Convert images to BMP format.
-ep : [Default] Convert images to PNG format.
Setting:
-oom : Disable In-Memory Loading. (Lower memory usage but longer time for loading)
-k<Key> : Set PSB key. use `-k` (without key specified) to reset.
");
            Console.WriteLine("Example: PsbDecompile -ep emt.pure.psb");
            Console.WriteLine("\t PsbDecompile C:\\\\EMTfolder");
        }

        static void Decompile(string path, bool extractImage = false, bool uncompress = false, bool usePng = false,
            uint? key = null)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            Console.WriteLine($"Decompiling: {name}");

#if !DEBUG
            try
#endif
            {
                if (extractImage)
                {
                    PsbDecompiler.DecompileToFile(path, PsbImageOption.Extract,
                        usePng ? PsbImageFormat.Png : PsbImageFormat.Bmp, key: key);
                }
                else if (uncompress)
                {
                    PsbDecompiler.DecompileToFile(path, PsbImageOption.Uncompress, key: key);
                }
                else
                {
                    PsbDecompiler.DecompileToFile(path, key: key);
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