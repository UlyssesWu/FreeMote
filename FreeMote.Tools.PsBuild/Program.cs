using System;
using System.IO;
using System.Linq;
using FreeMote.Plugins;
using FreeMote.Psb;
using FreeMote.PsBuild;

namespace FreeMote.Tools.PsBuild
{
    class Program
    {
        //Not thread safe
        private static PsbSpec? _platform = null;
        //private static PsbPixelFormat _pixelFormat = PsbPixelFormat.None;
        private static uint? _key = null;
        private static ushort? _version = null;
        private static bool _noRename = false;
        private static bool _keepShell = true;

        static void Main(string[] args)
        {
            Console.WriteLine("FreeMote PSB Compiler");
            Console.WriteLine("by Ulysses, wdwxy12345@gmail.com");

            FreeMount.Init();
            Console.WriteLine($"{FreeMount.PluginsCount} Plugins Loaded.");

            PsbConstants.InMemoryLoading = true;
            Console.WriteLine();

            if (args.Length <= 0 || args[0].ToLowerInvariant() == "/h" || args[0].ToLowerInvariant() == "?")
            {
                PrintHelp();
                return;
            }

            foreach (var s in args)
            {
                if (File.Exists(s))
                {
                    Compile(s);
                }
                else if (s.StartsWith("/v"))
                {
                    if (ushort.TryParse(s.Replace("/v", ""), out var ver))
                    {
                        _version = ver;
                    }
                }
                else if (s.StartsWith("/p"))
                {
                    if (Enum.TryParse(s.Replace("/p", ""), true, out PsbSpec platform))
                    {
                        _platform = platform;
                    }
                }
                //else if (s == "/no-tlg")
                //{
                //    TlgConverter.PreferManaged = true;
                //}
                //else if (s == "/tlg")
                //{
                //    TlgConverter.PreferManaged = false;
                //}
                else if (s == "/no-rename")
                {
                    _noRename = true;
                }
                else if (s == "/rename")
                {
                    _noRename = false;
                }
                else if (s == "/no-shell")
                {
                    _keepShell = false;
                }
                else if (s == "/shell")
                {
                    _keepShell = true;
                }
                //else if (s == "/no-key")
                //{
                //    _key = null;
                //}
                //else if (s.StartsWith("/f"))
                //{
                //    if (Enum.TryParse(s.Replace("/f", ""), true, out PsbPixelFormat format))
                //    {
                //        _pixelFormat = format;
                //    }
                //}
                else if (s.StartsWith("/k"))
                {
                    if (uint.TryParse(s.Replace("/k", ""), out var key))
                    {
                        _key = key;
                    }
                    else
                    {
                        _key = null;
                    }
                }
            }

            Console.WriteLine("Done.");
        }

        private static void Compile(string s)
        {
            var name = Path.GetFileNameWithoutExtension(s);
            var ext = Path.GetExtension(s);
            if (!string.IsNullOrEmpty(ext) && (ext.EndsWith(".psb") || ext.EndsWith(".emtbytes")))
            {
                if (_platform != null)
                {
                    Console.WriteLine($"Converting {name} to {_platform} platform...");
                    PSB psb = new PSB(s);
                    if (psb.Platform == _platform.Value)
                    {
                        Console.WriteLine("Already on the same platform, Skip.");
                    }
                    else
                    {
                        psb.SwitchSpec(_platform.Value);
                        psb.Merge();
                        File.WriteAllBytes(Path.ChangeExtension(s, $".{_platform.Value}.psb"), psb.Build());
                        Console.WriteLine($"Convert {name} succeed.");
                    }
                    return;
                }
            }
            Console.WriteLine($"Compiling {name} ...");
            try
            {
                //var filename = name + (_key == null ? _noRename ? ".psb" : "-pure.psb" : "-impure.psb");
                var filename = name + ".psb";
                PsbCompiler.CompileToFile(s, filename, null, _version, _key, _platform, !_noRename, _keepShell);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Compile {name} failed.\r\n{e}");
            }
            Console.WriteLine($"Compile {name} succeed.");
        }

        private static void PrintHelp()
        {
            var pluginInfo = FreeMount.PrintPluginInfos();
            if (!string.IsNullOrEmpty(pluginInfo))
            {
                Console.WriteLine(pluginInfo);
            }

            Console.WriteLine("Usage: .exe [Param] <PSB json path>");
            Console.WriteLine(@"Param:
/v<VerNumber> : Set compile version from [2,4] . Default: 3.
/k<CryptKey> : Set CryptKey. Default: none(Pure PSB). Requirement: uint, dec.
/p<Platform> : Set platform. Default: keep original platform. Support: krkr/win/common/ems.
    Warning: Platform ONLY works with .bmp/.png format textures.
/no-shell : Do not compress PSB to shell types even if shell type is specified in resx.json.
/no-rename : Compiled filename will be same as the json filename (with .psb extension).
    Warning: This setting may overwrite your original PSB files!
");
//no-key : Ignore any key setting and output pure PSB.
            Console.WriteLine("Example: PsBuild /v4 /k123456789 /pkrkr sample.psb.json");
        }
    }
}

