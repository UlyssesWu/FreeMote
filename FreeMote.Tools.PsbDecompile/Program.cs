using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreeMote.PsBuild;

namespace FreeMote.Tools.PsbDecompile
{
    class Program
    {
        //Not thread safe
        static bool _extractImage = false;
        static bool _uncompressImage = false;
        static bool _png = false;
        static void Main(string[] args)
        {

            Console.WriteLine("FreeMote PSB Decompiler");
            Console.WriteLine("by Ulysses, wdwxy12345@gmail.com");
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

                if (File.Exists(s))
                {
                    Decompile(s, _extractImage, _uncompressImage, _png);
                }
                else if (Directory.Exists(s))
                {
                    foreach (var file in Directory.EnumerateFiles(s, "*.psb"))
                    {
                        Decompile(file, _extractImage, _uncompressImage, _png);
                    }
                    foreach (var file in Directory.EnumerateFiles(s, "*.mmo"))
                    {
                        Decompile(file, _extractImage, _uncompressImage, _png);
                    }
                }
            }

            Console.WriteLine("Done.");
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Usage: .exe [Mode] <PSB path>");
            Console.WriteLine(@"Mode:
/raw : Default mode. Keep resource in original format.
/er : Similar to default mode but uncompress those compressed resources.
/eb : Convert images to BMP format.
/ep : Convert images to PNG format.
");
            Console.WriteLine("Example: PsbDecompile /ep Emote.pure.psb");
            Console.WriteLine("\t PsbDecompile C:\\\\EmoteFolder");
        }

        static void Decompile(string path, bool extractImage = false, bool uncompress = false, bool usePng = false)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            Console.WriteLine($"Decompiling: {name}");

#if DEBUG
            if (extractImage)
            {
                PsbDecompiler.DecompileToFile(path, PsbImageOption.Extract,
                    usePng ? PsbImageFormat.Png : PsbImageFormat.Bmp);
            }
            else if (uncompress)
            {
                PsbDecompiler.DecompileToFile(path, PsbImageOption.Raw);
            }
            else
            {
                PsbDecompiler.DecompileToFile(path);
            }
            return;
#else

            try
            {
                if (extractImage)
                {
                    PsbDecompiler.DecompileToFile(path, PsbImageOption.Extract,
                        usePng ? PsbImageFormat.Png : PsbImageFormat.Bmp);
                }
                else if (uncompress)
                {
                    PsbDecompiler.DecompileToFile(path, PsbImageOption.Raw);
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

#endif
        }
    }
}
