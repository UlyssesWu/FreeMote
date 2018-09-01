using System;
using System.IO;
using System.Text;
using FreeMote.Plugins;

namespace FreeMote.Tools.EmotePsbConverter
{
    class Program
    {
        private static uint? Key = null;
        private static uint? NewKey = null;
        static void Main(string[] args)
        {
            FreeMount.Init();

            if (args.Length > 1 && args[0].StartsWith("-c")) // compress mode
            {
                Console.WriteLine("FreeMote PSB Shell Converter");
                Console.WriteLine("by Ulysses, wdwxy12345@gmail.com");
                Console.WriteLine();
                var type = args[0].Substring(2);
                for (int i = 1; i < args.Length; i++)
                {
                    var path = args[i];
                    if (Directory.Exists(path))
                    {
                        foreach (var file in Directory.EnumerateFiles(path, "*.psb"))
                        {
                            ShellConvert(file, type);
                        }
                    }
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    ShellConvert(path, type);
                }

                Console.WriteLine("Done.");
                return;
            }

            Console.WriteLine("FreeMote PSB Converter");
            Console.WriteLine("by Ulysses, wdwxy12345@gmail.com");
            Console.WriteLine();

            if (args.Length >= 3)
            {
                if (!File.Exists(args[0]))
                {
                    Console.WriteLine("File not exists.");
                    return;
                }
                uint key;
                if (!uint.TryParse(args[1], out key))
                {
                    Console.WriteLine("Key is not valid.");
                    return;
                }
                Key = key;
                if (!uint.TryParse(args[2], out key))
                {
                    Console.WriteLine("New key is not valid.");
                    return;
                }
                NewKey = key;
                byte[] bytes = File.ReadAllBytes(args[0]);
                File.WriteAllBytes(Path.ChangeExtension(args[0], ".converted.psb"), PsbFile.Transfer(Key.Value, NewKey.Value, bytes));
            }
            else if (args.Length == 2)
            {
                if (!File.Exists(args[0]))
                {
                    Console.WriteLine("File not exists.");
                    return;
                }
                uint key;
                if (!uint.TryParse(args[1], out key))
                {
                    //Console.WriteLine("Key is not valid.");
                    //return;
                }
                Key = key;
                Convert(Key, args[0]);
            }
            else if (args.Length == 1)
            {
                if (!File.Exists(args[0]))
                {
                    PrintHelp();
                    return;
                }
                //AskForKey();
                Convert(Key, args[0]);
            }
            else
            {
                PrintHelp();
                AskForKey();
                AskForNewKey();

                DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory);
                uint count = 0;
                foreach (var file in di.EnumerateFiles("*.psb"))
                {
                    if (NewKey != null)
                    {
                        try
                        {
                            File.WriteAllBytes(Path.ChangeExtension(file.FullName, ".converted.psb"),
                                PsbFile.Transfer(Key.Value, NewKey.Value, File.ReadAllBytes(file.FullName)));
                            count++;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error: This file is not valid.");
                            Console.WriteLine(e);
                        }
                    }
                    else
                    {
                        if (Convert(Key, file.FullName))
                        {
                            count++;
                        }
                    }
                }
                Console.WriteLine($"Completed! {count} files processed in total.");
            }
            Console.WriteLine("Done. Press ENTER to exit...");
            Console.ReadLine();
        }

        private static bool ShellConvert(string path, string type)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    var ctx = FreeMount.CreateContext();
                    string currentType = null;
                    var ms = ctx.OpenFromShell(fs, ref currentType);
                    if (ms == null) // no shell, compress
                    {
                        if (string.IsNullOrEmpty(type))
                        {
                            return false;
                        }
                        var mms = ctx.PackToShell(fs, type);
                        if (mms == null)
                        {
                            Console.WriteLine($"Shell type unsupported: {type}");
                            return false;
                        }

                        Console.WriteLine($"[{type}] Shell applied for {path}");
                        File.WriteAllBytes(path + "." + type, mms.ToArray());
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(type) || currentType == type)
                        {
                            Console.WriteLine($"Shell Type [{currentType}] detected for {path}");
                            File.WriteAllBytes(Path.ChangeExtension(path, ".decompressed.psb"), ms.ToArray());
                        }
                        else
                        {
                            var mms = ctx.PackToShell(ms, type);
                            if (mms == null)
                            {
                                Console.WriteLine($"Shell type unsupported: {type}");
                                return false;
                            }

                            Console.WriteLine($"[{type}] Shell applied for {path}");
                            File.WriteAllBytes(Path.ChangeExtension(path, $".compressed.{type}"), mms.ToArray());
                        }
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine($"Shell Convert failed: {path}");
                return false;
            }

            return true;
        }

        private static void PrintHelp()
        {
            Console.WriteLine(@"Usage: .exe [mode] <PSB file path> <key> [new key]
Mode: use `-c<shell type>` for first param to switch to shell compress/decompress mode.
Example: EmoteConv emote_test.psb 123456789
\t EmoteConv emote_test.psb 123456789 987654321
\t EmoteConv -cLZ4 emote_test.psb (compress to LZ4)
\t EmoteConv -c emote_test.psb.lz4 (decompress)
Hint: If ths tool can't decrypt your PSB, try PsbDecompile.
");
        }

        static void AskForKey()
        {
            while (Key == null)
            {
                Console.WriteLine("Please input key (uint, dec):");
                string ans = Console.ReadLine();
                uint key;
                if (uint.TryParse(ans, out key))
                {
                    Key = key;
                }
                else
                {
                    Console.WriteLine("Input not correct.");
                }
            }
        }

        static void AskForNewKey()
        {
            Console.WriteLine("Please input new key (uint, dec), or just ENTER if not using transfer:");
            string ans = Console.ReadLine();
            uint key;
            if (!string.IsNullOrWhiteSpace(ans) && uint.TryParse(ans, out key))
            {
                NewKey = key;
            }
            else
            {
                NewKey = null;
            }
        }

        static bool Convert(uint? key, string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"File:{path} not exists.");
                return false;
            }

            try
            {
                using (var fs = new FileStream(path, FileMode.Open))
                {
                    Stream stream = fs;
                    string type = null;
                    var ms = FreeMount.CreateContext().OpenFromShell(fs, ref type);
                    bool hasShell = false;
                    if (ms != null)
                    {
                        Console.WriteLine($"Shell type: {type}");
                        stream = ms;
                        hasShell = true;
                    }
                    BinaryReader br = new BinaryReader(stream, Encoding.UTF8);
                    if (!key.HasValue)
                    {
                        var k = FreeMount.CreateContext().GetKey(stream);
                        if (k != null)
                        {
                            key = k;
                            Console.WriteLine($"Using key: {key}");
                        }
                        else if (hasShell)
                        {
                            File.WriteAllBytes(Path.ChangeExtension(path, ".decompressed.psb"), ms.ToArray());
                            return true;
                        }
                        else
                        {
                            Console.WriteLine("No Key and No Shell.");
                            return false;
                        }
                    }

                    var header = PsbHeader.Load(br);

                    using (var outMs = new MemoryStream((int)stream.Length))
                    {
                        if (header.Version > 2)
                        {
                            if (PsbFile.TestHeaderEncrypted(stream, header)) //Decrypt
                            {
                                //psb.EncodeToFile(key.Value, path + ".decrypted", EncodeMode.Decrypt);
                                PsbFile.Encode(key.Value, EncodeMode.Decrypt, EncodePosition.Auto, stream, outMs);
                                File.WriteAllBytes(Path.ChangeExtension(path, ".pure.psb"), outMs.ToArray());
                            }
                            else
                            {
                                //psb.EncodeToFile(key.Value, path + ".encrypted", EncodeMode.Encrypt);
                                PsbFile.Encode(key.Value, EncodeMode.Encrypt, EncodePosition.Auto, stream, outMs);
                                File.WriteAllBytes(Path.ChangeExtension(path, ".encrypted.psb"), outMs.ToArray());
                            }
                        }
                        else
                        {
                            if (PsbFile.TestBodyEncrypted(br, header)) //Decrypt
                            {
                                PsbFile.Encode(key.Value, EncodeMode.Decrypt, EncodePosition.Auto, stream, outMs);
                                File.WriteAllBytes(Path.ChangeExtension(path, ".pure.psb"), outMs.ToArray());
                            }
                            else
                            {
                                PsbFile.Encode(key.Value, EncodeMode.Encrypt, EncodePosition.Auto, stream, outMs);
                                File.WriteAllBytes(Path.ChangeExtension(path, ".encrypted.psb"), outMs.ToArray());
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: This file is not valid.");
                Console.WriteLine(e);
                return false;
            }
            return true;
        }
    }
}

