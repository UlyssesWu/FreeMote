using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using FreeMote.Plugins;
using FreeMote.Psb;
using FreeMote.PsBuild;
using McMaster.Extensions.CommandLineUtils;

namespace FreeMote.Tools.Viewer
{
    public static class Core
    {
        public static uint Width { get; set; } = 1280;
        public static uint Height { get; set; } = 720;
        public static bool DirectLoad { get; set; } = false;
        public static string PsbPath { get; set; }
        public static string[] ExtraPaths { get; set; }

        internal static bool NeedRemoveTempFile { get; set; } = false;
    }

    class Program
    {
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool FreeConsole();

        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("FreeMote Viewer");
            Console.WriteLine("by Ulysses, wdwxy12345@gmail.com");
            Console.WriteLine();
            var app = new CommandLineApplication();
            app.OptionsComparison = StringComparison.OrdinalIgnoreCase;

            //help
            app.HelpOption("-?|--help"); //do not inherit
            //app.ExtendedHelpText = PrintHelp();

            //options
            var optWidth = app.Option<uint>("-w|--width", "Set Window width", CommandOptionType.SingleValue);
            var optHeight = app.Option<uint>("-h|--height", "Set Window height", CommandOptionType.SingleValue);
            var optDirectLoad = app.Option("-d|--direct", "Just load with EMT driver, don't try parsing with FreeMote first", CommandOptionType.NoValue);

            //args
            var argPath = app.Argument("Files", "File paths", multipleValues: true);

            app.OnExecute(() =>
            {
                if (argPath.Values.Count == 0)
                {
                    app.ShowHelp();
                    return;
                }

                Core.PsbPath = argPath.Values[0];

                if (!File.Exists(Core.PsbPath))
                {
                    MessageBox.Show("File not exist.");
                    Console.WriteLine("File not exist.");
                    return;
                }

                if (optWidth.HasValue())
                {
                    Core.Width = optWidth.ParsedValue;
                }

                if (optHeight.HasValue())
                {
                    Core.Height = optHeight.ParsedValue;
                }

                if (argPath.Values.Count > 1)
                {
                    Core.ExtraPaths = argPath.Values.Skip(1).ToArray();
                }

                if (!optDirectLoad.HasValue())
                {
                    try
                    {
                        //Consts.FastMode = false;
                        FreeMount.Init();
                        using (var fs = File.OpenRead(Core.PsbPath))
                        {
                            var ctx = FreeMount.CreateContext();
                            string currentType = null;
                            var ms = ctx.OpenFromShell(fs, ref currentType);
                            var psb = ms != null ? new PSB(ms) : new PSB(fs);

                            if (psb.Platform == PsbSpec.krkr)
                            {
                                psb.SwitchSpec(PsbSpec.win, PsbSpec.win.DefaultPixelFormat());
                            }

                            psb.Merge();
                            //File.WriteAllText("output.json", PsbDecompiler.Decompile(psb));
                            Core.PsbPath = Path.GetTempFileName();
                            File.WriteAllBytes(Core.PsbPath, psb.Build());
                            Core.NeedRemoveTempFile = true;
                            ms?.Dispose();
                        }

                        GC.Collect(); //Can save memory from 700MB to 400MB
                    }
                    catch (Exception ex)
                    {
                        //ignore
                    }
                }
                else
                {
                    Core.DirectLoad = true;
                }

                FreeConsole();
                App wpf = new App();
                MainWindow main = new MainWindow();
                wpf.Run(main);
            });

            try
            {
                app.Execute(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
        }
    }
}
