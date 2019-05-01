using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FreeMote.Plugins;
using McMaster.Extensions.CommandLineUtils;

namespace FreeMote.Tools.EmtConvert
{
    class Program
    {
        private static uint? Key = null;
        private static uint? NewKey = null;

        static void Main(string[] args)
        {
            Console.WriteLine("FreeMote PSB Converter");
            Console.WriteLine("by Ulysses, wdwxy12345@gmail.com");
            FreeMount.Init();
            Console.WriteLine($"{FreeMount.PluginsCount} Plugins Loaded.");
            Console.WriteLine();

            var app = new CommandLineApplication();
            app.OptionsComparison = StringComparison.OrdinalIgnoreCase;

            //help
            app.HelpOption();
            app.ExtendedHelpText = PrintHelp();

            //options
            var optKey = app.Option<uint>("-k|--key <KEY>", "PSB key (uint, dec)", CommandOptionType.SingleValue);
            var optNewKey = app.Option<uint>("-nk|--new-key <KEY>", "New PSB key for transfer (uint, dec)",
                CommandOptionType.SingleValue);
            //args
            var argPath =
                app.Argument("Files", "File paths", multipleValues: true);

            //command: pack
            app.Command("pack", packCmd =>
            {
                //help
                packCmd.Description = "Packing/unpacking PSBs to/from shell (FreeMote.Plugins required)";
                packCmd.HelpOption();
                packCmd.ExtendedHelpText = @"
Example:
  EmtConvert pack -s LZ4 sample.psb 
";
                //options
                var optType = packCmd.Option("-s|--shell <SHELL>",
                    "Set shell type. No need to specify if unpack",
                    CommandOptionType.SingleValue);
                //args
                var argPsbPaths = packCmd.Argument("PSB", "PSB Paths", true);

                packCmd.OnExecute(() =>
                {
                    string type = optType.HasValue() ? optType.Value() : null;
                    foreach (var s in argPsbPaths.Values)
                    {
                        if (File.Exists(s))
                        {
                            ShellConvert(s, type);
                        }
                    }
                });
            });

            //mdf
            app.Command("mdf", mdfCmd =>
            {
                //help
                mdfCmd.Description = "Decrypt MT19937 encrypted MDF";
                mdfCmd.HelpOption();
                mdfCmd.ExtendedHelpText = @"
Example:
  EmtConvert mdf -k 1234567890ab -l 129 sample.psb 
  EmtConvert mdf -s 1234567890absample.psb -l 129 sample.psb 
";
                //options
                var optMdfSeed = mdfCmd.Option("-s|--seed <SEED>",
                    "Set complete seed (Key+FileName)",
                    CommandOptionType.SingleValue);
                var optMdfKey = mdfCmd.Option("-k|--key <KEY>",
                    "Set key (Infer file name from path)",
                    CommandOptionType.SingleValue);
                var optMdfKeyLen = mdfCmd.Option<uint>("-l|--length <LEN>",
                    "Set key length (not required if decrypt all bytes)",
                    CommandOptionType.SingleValue);
                //args
                var argPsbPaths = mdfCmd.Argument("PSB", "PSB Paths", true);

                mdfCmd.OnExecute(() =>
                {
                    string key = optMdfKey.HasValue() ? optMdfKey.Value() : null;
                    string seed = optMdfSeed.HasValue() ? optMdfKey.Value() : null;
                    if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(seed))
                    {
                        throw new ArgumentNullException("No key or seed specified.");
                    }
                    uint? len = optMdfKeyLen.HasValue() ? optMdfKeyLen.ParsedValue : (uint?)null;
                    Dictionary<string, object> context = new Dictionary<string, object>();
                    if (len.HasValue)
                    {
                        context["MdfKeyLength"] = len;
                    }
                    foreach (var s in argPsbPaths.Values)
                    {
                        if (File.Exists(s))
                        {
                            var fileName = Path.GetFileName(s);
                            string finalSeed = seed;
                            if (key != null)
                            {
                                finalSeed = key + fileName;
                            }

                            context["MdfKey"] = finalSeed;
                            ShellConvert(s, "MDF", context);
                        }
                    }
                });
            });

            app.OnExecute(() =>
            {
                uint? key = optKey.HasValue() ? optKey.ParsedValue : (uint?) null;
                uint? newKey = optNewKey.HasValue() ? optNewKey.ParsedValue : (uint?) null;

                foreach (var s in argPath.Values)
                {
                    if (File.Exists(s))
                    {
                        if (key != null && newKey != null) //Transfer
                        {
                            File.WriteAllBytes(Path.ChangeExtension(s, ".converted.psb"),
                                PsbFile.Transfer(key.Value, newKey.Value, File.ReadAllBytes(s)));
                        }
                        else
                        {
                            Convert(key, s);
                        }
                    }
                }
            });

            if (args.Length == 0)
            {
                app.ShowHelp();
                Console.WriteLine("Convert all PSBs in current directory:");
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
                Console.WriteLine("Press ENTER to exit...");
                Console.ReadLine();
                return;
            }

            app.Execute(args);

            Console.WriteLine("Done.");
        }

        private static bool ShellConvert(string path, string type, Dictionary<string, object> context = null)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    var ctx = FreeMount.CreateContext(context);
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

        private static string PrintHelp()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine().AppendLine("Plugins:");
            sb.AppendLine(FreeMount.PrintPluginInfos(2));
            sb.AppendLine(@"Examples: 
  EmtConvert sample.psb    //Unpack from shell, and decrypt if there is a KeyProvider plugin
  EmtConvert -k 123456789 sample.psb    //Decrypt or encrypt using key
  EmtConvert -k 123456789 -nk 987654321 sample.psb    //Transfer from old key to new key
  Hint: If EmtConvert can't decrypt your PSB, try PsbDecompile.");
            return sb.ToString();
            //            Console.WriteLine(@"Usage: .exe [mode] <PSB file path> <key> [new key]
            //Mode: use `-c<shell type>` as the first param to enable shell compress/decompress mode.
            //Example: emtconvert test.psb 123456789 (encrypt or decrypt using key)
            //\t emtconvert test.psb 123456789 987654321 (transfer an impure PSB to another key)
            //\t emtconvert -cLZ4 test.psb (compress to LZ4)
            //\t emtconvert -c test.psb.lz4 (decompress)
            //Hint: If EmtConvert can't decrypt your PSB, try PsbDecompile.
            //");
        }

        static void AskForKey()
        {
            while (Key == null)
            {
                Console.WriteLine("Please input key (uint, dec):");
                string ans = Console.ReadLine();
                if (string.IsNullOrEmpty(ans))
                {
                    return;
                }

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

                    using (var outMs = new MemoryStream((int) stream.Length))
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