using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using Dynamitey.DynamicObjects;

namespace FreeMote.Plugins
{
    /// <summary>
    /// FreeMote.Tlg
    /// <para>Native TLG Plugin</para>
    /// </summary>
    public class TlgPlugin
    {
        private static bool _isEnabled = true;

        /// <summary>
        /// Is plugin enabled
        /// </summary>
        public static bool IsEnabled
        {
            get => IsReady && _isEnabled;
            set
            {
                if (IsReady)
                {
                    _isEnabled = value;
                }
            }
        }

        /// <summary>
        /// Is plugin loaded
        /// </summary>
        public static bool IsReady { get; private set; } = false;
        private const string PluginPath = "TlgLib.dll";
        private static dynamic TlgNative = null;
        //private static dynamic TlgLoader = null;
        static TlgPlugin()
        {
            IsReady = false;
            if (!File.Exists(PluginPath))
            {
                return;
            }

            try
            {
                var asm = Assembly.LoadFile(Path.GetFullPath(PluginPath));
                TlgNative = new LateType(asm, "FreeMote.Tlg.TlgNative");
                //TlgLoader = new LateType(asm, "FreeMote.Tlg.TlgLoader");
                if (TlgNative.IsAvailable)
                {
                    IsReady = true;
                }
            }
            catch (Exception)
            {
                return;
            }
        }

        /// <summary>
        /// Save <see cref="Bitmap"/> as TLG
        /// </summary>
        /// <param name="bmp"></param>
        /// <param name="tlg6">true: save as TLG6; false: save as TLG5</param>
        /// <returns></returns>
        public static byte[] SaveTlg(Bitmap bmp, bool tlg6 = false)
        {
            if (tlg6)
            {
                return TlgNative.ToTlg6(bmp);
            }

            return TlgNative.ToTlg5(bmp);
        }

        /// <summary>
        /// Load TLG as <see cref="Bitmap"/>
        /// </summary>
        /// <param name="tlgBytes"></param>
        /// <param name="version">get TLG version [5,6]</param>
        /// <returns></returns>
        public static Bitmap LoadTlg(byte[] tlgBytes, out int version)
        {
            //Impossible to call `TlgNative.ToBitmap(byte[], out int, bool = false)` because dynamic invoke can not call method with ref/out!
            Tuple<Bitmap, int> tuple = TlgNative.ToBitmap(tlgBytes);
            version = tuple.Item2;
            return tuple.Item1;
        }
    }
}
