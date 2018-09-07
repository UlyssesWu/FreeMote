using System;
using System.IO;
using FreeMote.Plugins;
using FreeMote.Psb;
using FreeMote.PsBuild;

namespace FreeMote.Tools.EmtMake
{
    class Program
    {
        static void Main(string[] args)
        {
            FreeMount.Init();
            Console.WriteLine("FreeMote MMO Decompiler (Preview)");
            Console.WriteLine("by Ulysses, wdwxy12345@gmail.com");
            Console.WriteLine();
            Console.WriteLine("This is a preview version. If it crashes, send the sample PSB to me.");
            Console.WriteLine("All output files from this tool should follow CC-BY-NC-SA 4.0. Agree this license by pressing Enter:");
            Console.ReadLine();
            if (args.Length < 1 || !File.Exists(args[0]))
            {
                return;
            }

            PSB psb = null;
            try
            {
                psb = new PSB(args[0]);
            }
            catch (Exception e)
            {
                Console.WriteLine("Input PSB is invalid.");
            }

            if (psb != null)
            {
                if (psb.Platform != PsbSpec.krkr)
                {
                    if (psb.Platform == PsbSpec.common || psb.Platform == PsbSpec.win)
                    {
                        psb.SwitchSpec(PsbSpec.krkr);
                        psb.Merge();
                    }
                    else
                    {
                        Console.WriteLine(
                            $"EmtMake do not support {psb.Platform} PSB. Please use pure krkr PSB.");
                        goto END;
                    }
                }
#if !DEBUG
                try
#endif
                {
                    MmoBuilder builder = new MmoBuilder();
                    var output = builder.Build(psb);
                    output.Merge();
                    File.WriteAllBytes(Path.ChangeExtension(args[0], ".FreeMote.mmo"), output.Build());
                }
#if !DEBUG
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
#endif
            }

            END:
            Console.WriteLine("Done.");
            Console.ReadLine();
        }
    }
}
