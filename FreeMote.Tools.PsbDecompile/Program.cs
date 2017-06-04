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
        static void Main(string[] args)
        {
            Console.WriteLine("FreeMote PSB Decompiler");
            Console.WriteLine("by Ulysses, wdwxy12345@gmail.com");
            Console.WriteLine();

            foreach (var s in args)
            {
                if (File.Exists(s))
                {
                    Decompile(s);
                }
                else if(Directory.Exists(s))
                {
                    foreach (var file in Directory.EnumerateFiles(s, "*.psb"))
                    {
                        Decompile(file);
                    }
                    foreach (var file in Directory.EnumerateFiles(s, "*.mmo"))
                    {
                        Decompile(file);
                    }
                }
            }

            Console.WriteLine("Done.");
        }

        static void Decompile(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            Console.WriteLine($"Decompiling: {name}");
            try
            {
                PsbDecompiler.DecompileToFile(path);

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
