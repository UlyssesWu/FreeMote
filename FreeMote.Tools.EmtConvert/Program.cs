using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using FreeMote.Plugins;
using FreeMote.Psb;
using McMaster.Extensions.CommandLineUtils;
using static FreeMote.Consts;

namespace FreeMote.Tools.EmtConvert
{
    public enum PsbImageConvertMethod
    {
        /// <summary>
        /// Switch byte [0] [2]
        /// </summary>
        /// 如果你是情迷弗兰克斯的女二号，面对泽拉图的威胁你会怎么做？
        Switch02,
        /// <summary>
        /// Round Shift Right
        /// </summary>
        ROR,
        /// <summary>
        /// Round Shift Left
        /// </summary>
        ROL,
        /// <summary>
        /// Extend 2 bytes pixel to 4 bytes pixel
        /// </summary>
        LeARGB_4To8,
        /// <summary>
        /// Convert color to L8 style GrayScale
        /// </summary>
        LeARGB_To_L8Grayscale,
        /// <summary>
        /// Untitle
        /// </summary>
        Untile,
        /// <summary>
        /// Unswizzle
        /// </summary>
        Unswizzle,
        /// <summary>
        /// Tile
        /// </summary>
        Tile,
        /// <summary>
        /// Swizzle
        /// </summary>
        Swizzle,
    }

    public enum PsbFixMethod
    {
        /// <summary>
        /// Fix [/metadata/base/motion] missing issue for partial exported motion PSB
        /// </summary>
        MetadataBase,
    }

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

            //command: pixel
            app.Command("pixel", pixelCmd =>
            {
                //help
                pixelCmd.Description = "Convert pixel colors of extracted images (RGBA BMP/PNG)";
                pixelCmd.HelpOption();
                pixelCmd.ExtendedHelpText = @"
Example:
  EmtConvert pixel -m Switch02 sample.png
";
                //options
                var optMethod = pixelCmd.Option<PsbImageConvertMethod>("-m|--method <METHOD>",
                    "Set convert method",
                    CommandOptionType.SingleValue);

                //args
                var argPaths = pixelCmd.Argument("Image", "Image Paths", true);

                pixelCmd.OnExecute(() =>
                {
                    if (!optMethod.HasValue())
                    {
                        Console.WriteLine("Convert Method is not specified!");
                        return;
                    }

                    foreach (var path in argPaths.Values)
                    {
                        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        {
                            continue;
                        }

                        int width = 0;
                        int height = 0;
                        PixelFormat pixelFormat = PixelFormat.Format32bppArgb;
                        try
                        {
                            var img = Image.FromFile(path);
                            width = img.Width;
                            height = img.Height;
                            pixelFormat = img.PixelFormat;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            continue;
                        }

                        var bts = RL.GetPixelBytesFromImageFile(path);

                        switch (optMethod.ParsedValue)
                        {
                            case PsbImageConvertMethod.Switch02:
                                RL.Switch_0_2(ref bts);
                                break;
                            case PsbImageConvertMethod.ROR:
                                RL.Argb2Rgba(ref bts);
                                break;
                            case PsbImageConvertMethod.ROL:
                                RL.Argb2Rgba(ref bts, true);
                                break;
                            case PsbImageConvertMethod.LeARGB_4To8:
                                bts = RL.Argb428(bts);
                                break;
                            case PsbImageConvertMethod.LeARGB_To_L8Grayscale:
                                bts = RL.Argb2L8(bts);
                                bts = RL.ReadL8(bts, height, width);
                                break;
                            case PsbImageConvertMethod.Untile:
                                bts = PostProcessing.UntileTexture(bts, width, height, pixelFormat);
                                break;
                            case PsbImageConvertMethod.Unswizzle:
                                bts = PostProcessing.UnswizzleTexture(bts, width, height, pixelFormat);
                                break;
                            case PsbImageConvertMethod.Tile:
                                bts = PostProcessing.TileTexture(bts, width, height, pixelFormat);
                                break;
                            case PsbImageConvertMethod.Swizzle:
                                bts = PostProcessing.SwizzleTexture(bts, width, height, pixelFormat);
                                break;
                            default:
                                continue;
                        }

                        RL.ConvertToImageFile(bts, Path.ChangeExtension(path, ".converted.png"), height, width, PsbImageFormat.png);
                    }
                   
                });
            });

            //command: pack
            app.Command("pack", packCmd =>
            {
                //help
                packCmd.Description = "Pack/Unpack PSBs to/from shell (FreeMote.Plugins required)";
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
                var argPsbPaths = packCmd.Argument("PSB", "MDF/PSB Paths", true);

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

            //command: print
            app.Command("print", printCmd =>
            {
                //help
                printCmd.Description = "Print an EMT PSB (for its initial state, don't expect it working)";
                printCmd.HelpOption();
                printCmd.ExtendedHelpText = @"
Example:
  EmtConvert print -w 4096 -h 4096 sample.psb 
";
                //options
                var optWidth = printCmd.Option<int>("-w|--width <INT>",
                    "Set width. Default=-1 (auto)",
                    CommandOptionType.SingleValue);
                var optHeight = printCmd.Option<int>("-h|--height <INT>",
                    "Set height. Default=-1 (auto)",
                    CommandOptionType.SingleValue);
                //args
                var argPsbPaths = printCmd.Argument("PSB", "MDF/PSB Paths", true);

                printCmd.OnExecute(() =>
                {
                    int width = optWidth.HasValue() ? optWidth.ParsedValue : -1;
                    int height = optHeight.HasValue() ? optHeight.ParsedValue : -1;
                    foreach (var s in argPsbPaths.Values)
                    {
                        if (File.Exists(s))
                        {
                            Draw(s, width, height);
                        }
                    }
                });
            });

            //command: fix
            app.Command("fix", fixCmd =>
            {
                //help
                fixCmd.Description = "Some mysterious fixes for PSB";
                fixCmd.HelpOption();
                fixCmd.ExtendedHelpText = @"
Example:
  EmtConvert fix -m MetadataBase sample.psb 
";
                //options
                var optMethod = fixCmd.Option<PsbFixMethod>("-m|--method <METHOD>",
                    "Set fix method.", CommandOptionType.SingleValue);

                //args
                var argPsbPaths = fixCmd.Argument("PSB", "PSB Paths", true);

                fixCmd.OnExecute(() =>
                {
                    if (!optMethod.HasValue())
                    {
                        Console.WriteLine("Fix Method is not specified!");
                        return;
                    }

                    var method = optMethod.ParsedValue;
                    foreach (var s in argPsbPaths.Values)
                    {
                        if (!File.Exists(s))
                        {
                            continue;
                        }

                        switch(method)
                        {
                            case PsbFixMethod.MetadataBase:
                            
                                {
                                    Console.Write($"Using {method} to fix {s} ...");
                                    PSB psb = new PSB(s);
                                    if (psb.FixMotionMetadata())
                                    {
                                        psb.BuildToFile(Path.ChangeExtension(s, ".fixed.psb"));
                                        Console.WriteLine("Fixed!");
                                    }
                                    else
                                    {
                                        Console.WriteLine("not fixed.");
                                    }
                                }
                                break;
                            default:
                                Console.WriteLine($"Not implemented method: {method}");
                                break;
                        }                        
                    }
                });
            });

            //mdf
            app.Command("mdf", mdfCmd =>
            {
                //help
                mdfCmd.Description = "Pack/Unpack MT19937 encrypted MDF (FreeMote.Plugins required)";
                mdfCmd.HelpOption();
                mdfCmd.ExtendedHelpText = @"
Example:
  EmtConvert mdf -k 1234567890ab -l 131 sample.psb 
  EmtConvert mdf -s 1234567890absample.psb -l 131 sample.psb 
  Hint: To pack a pure MDF, use `EmtConvert pack -s MDF <MDF file>`
";
                //options
                //var optMdfPack = mdfCmd.Option("-p|--pack",
                //    "Pack (Encrypt) a PSB to MT19937 MDF",
                //    CommandOptionType.NoValue);
                var optMdfSeed = mdfCmd.Option("-s|--seed <SEED>",
                    "Set complete seed (Key+FileName)",
                    CommandOptionType.SingleValue);
                var optMdfKey = mdfCmd.Option("-k|--key <KEY>",
                    "Set key (Infer file name from path)",
                    CommandOptionType.SingleValue);
                var optMdfKeyLen = mdfCmd.Option<uint>("-l|--length <LEN>",
                    "Set key length. Usually use 131. If not set, it will be the length of the file (usually you don't expect this).",
                    CommandOptionType.SingleValue);
                //args
                var argPsbPaths = mdfCmd.Argument("PSB", "PSB Paths", true);

                mdfCmd.OnExecute(() =>
                {
                    string key = optMdfKey.HasValue() ? optMdfKey.Value() : null;
                    string seed = optMdfSeed.HasValue() ? optMdfSeed.Value() : null;
                    if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(seed))
                    {
                        //throw new ArgumentNullException(nameof(key), "No key or seed specified.");
                        Console.WriteLine("No key or seed specified. Packing to pure MDF.");

                        foreach (var s in argPsbPaths.Values)
                        {
                            if (File.Exists(s))
                            {
                                ShellConvert(s, "MDF");
                            }
                        }
                        return;
                    }

                    Dictionary<string, object> context = new Dictionary<string, object>();
                    uint? keyLen = optMdfKeyLen.HasValue() ? optMdfKeyLen.ParsedValue : (uint?) null;
                    if (keyLen.HasValue)
                    {
                        context[Context_MdfKeyLength] = keyLen;
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

                            context[Context_MdfKey] = finalSeed;
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

        private static void Draw(string path, int width, int height)
        {
            var psb = new PSB(path);
            var painter = new PsbPainter(psb);
            if (width < 0 || height < 0)
            {
                psb.TryGetCanvasSize(out var cw, out var ch);
                if (width < 0)
                {
                    width = cw;
                }

                if (height < 0)
                {
                    height = ch;
                }
            }

            var bmp = painter.Draw(width, height);
            bmp.Save(Path.ChangeExtension(path, ".FreeMote.png"), ImageFormat.Png);
        }
        
        private static bool ShellConvert(string path, string type, Dictionary<string, object> context = null)
        {
            try
            {
                using var fs = File.OpenRead(path);
                var ctx = FreeMount.CreateContext(context);
                string currentType = null;
                using var ms = ctx.OpenFromShell(fs, ref currentType);
                if (ms == null) // no shell, compress
                {
                    if (string.IsNullOrEmpty(type))
                    {
                        return false;
                    }

                    using var mms = ctx.PackToShell(fs, type);
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
                        using var mms = ctx.PackToShell(ms, type);
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
                using var fs = new FileStream(path, FileMode.Open);
                Stream stream = fs;
                string type = null;
                using var ms = FreeMount.CreateContext().OpenFromShell(fs, ref type);
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