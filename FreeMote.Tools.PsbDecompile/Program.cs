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
        static void Main(string[] args)
        {
            Console.WriteLine("FreeMote PSB Decompiler");
            Console.WriteLine("by Ulysses, wdwxy12345@gmail.com");

            FreeMount.Init();
            Console.WriteLine($"{FreeMount.PluginInfos.Count()} Plugins Loaded.");

            PsbConstants.InMemoryLoading = true;
            Console.WriteLine();

            if (args.Length <= 0 || args[0].ToLowerInvariant() == "/h" || args[0].ToLowerInvariant() == "?")
            {
                PrintHelp();
                return;
            }

            foreach (var s in args)
            {
                // Convert resources to BMP
                if (s.ToLowerInvariant() == "/eb" || s.ToLowerInvariant() == "/extract" || s.ToLowerInvariant() == "/bmp")
                {
                    _extractImage = true;
                    _png = false;
                    continue;
                }
                // Convert resources to PNG
                if (s.ToLowerInvariant() == "/ep" || s.ToLowerInvariant() == "/png")
                {
                    _extractImage = true;
                    _png = true;
                    continue;
                }
                // Convert resources to BIN
                if (s.ToLowerInvariant() == "/er" || s.ToLowerInvariant() == "/uncompress")
                {
                    _extractImage = false;
                    _uncompressImage = true;
                    continue;
                }
                // Keep Original
                if (s.ToLowerInvariant() == "/ne" || s.ToLowerInvariant() == "/raw")
                {
                    _extractImage = false;
                    _uncompressImage = false;
                    continue;
                }

                // Disable MM IO
                //メモリ足りない もうどうしよう : https://soundcloud.com/ulysses-wu/Heart-Chrome
                if (s.ToLowerInvariant() == "/oom" || s.ToLowerInvariant() == "/low-mem")
                {
                    PsbConstants.InMemoryLoading = false;
                    continue;
                }

                //Enable MM IO
                if (s.ToLowerInvariant() == "/mem" || s.ToLowerInvariant() == "/fast")
                {
                    PsbConstants.InMemoryLoading = true;
                    continue;
                }

                if (File.Exists(s))
                {
                    Decompile(s, _extractImage, _uncompressImage, _png);
                }
                else if (Directory.Exists(s))
                {
                    foreach (var file in Directory.EnumerateFiles(s, "*.psb")
                        .Union(Directory.EnumerateFiles(s, "*.mmo"))
                        .Union(Directory.EnumerateFiles(s, "*.pimg"))
                        .Union(Directory.EnumerateFiles(s, "*.scn"))
                        .Union(Directory.EnumerateFiles(s, "*.dpak"))
                        .Union(Directory.EnumerateFiles(s, "*.psz"))
                    )
                    {
                        Decompile(file, _extractImage, _uncompressImage, _png);
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
/raw : Keep resource in original format.
/er : Similar to raw mode but uncompress those compressed resources.
/eb : Convert images to BMP format.
/ep : [Default] Convert images to PNG format.
Setting:
/oom : Disable In-Memory Loading. (Lower memory usage but longer time for loading)
");
            Console.WriteLine("Example: PsbDecompile /ep Emote.pure.psb");
            Console.WriteLine("\t PsbDecompile C:\\\\EmoteFolder");
        }

        static void Decompile(string path, bool extractImage = false, bool uncompress = false, bool usePng = false)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            Console.WriteLine($"Decompiling: {name}");

            try
            {
                if (extractImage)
                {
                    PsbDecompiler.DecompileToFile(path, PsbImageOption.Extract,
                        usePng ? PsbImageFormat.Png : PsbImageFormat.Bmp);
                }
                else if (uncompress)
                {
                    PsbDecompiler.DecompileToFile(path, PsbImageOption.Uncompress);
                }
                else
                {
                    PsbDecompiler.DecompileToFile(path);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
