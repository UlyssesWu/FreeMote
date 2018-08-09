using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.IO;

namespace FreeMote.Plugins
{
    [Export(typeof(IPsbImageFormatter))]
    [ExportMetadata("Name", "FreeMote.Tlg")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "TLG support via TlgLib.")]
    public class TlgImageFormatter : IPsbImageFormatter
    {
        private const string TlgVersion = "TlgVersion";
        /// <summary>
        /// Use managed TLG loader if possible
        /// </summary>
        public static bool PreferManaged { get; set; } = false;
        /// <summary>
        /// Is TLG encode plugin enabled
        /// </summary>
        public static bool CanSaveTlg => TlgNativePlugin.IsEnabled;

        private static TlgImageConverter _managedConverter = null;

        /// <summary>
        /// Load TLG
        /// </summary>
        /// <param name="tlgData"></param>
        /// <param name="version">TLG version, can be 0(unknown),5,6</param>
        /// <returns></returns>
        public static Bitmap LoadTlg(byte[] tlgData, out int version)
        {
            if (!PreferManaged && TlgNativePlugin.IsEnabled)
            {
                try
                {
                    return TlgNativePlugin.LoadTlg(tlgData, out version);
                }
                catch (Exception)
                {
                    //ignored, fallback to managed decoder
                }
            }

            if (_managedConverter == null)
            {
                _managedConverter = new TlgImageConverter();
            }

            using (var ms = new MemoryStream(tlgData))
            {
                using (var br = new BinaryReader(ms))
                {
                    var bmp = _managedConverter.ReadAndGetMetaData(br, out var md);
                    version = md.Version;
                    return bmp;
                }
            }
        }

        /// <summary>
        /// Save TLG
        /// </summary>
        /// <param name="bmp"></param>
        /// <param name="tlg6">true: Save as TLG6; false: Save as TLG5</param>
        /// <returns></returns>
        public static byte[] SaveTlg(Bitmap bmp, bool tlg6 = false)
        {
            if (!CanSaveTlg)
            {
                throw new NotSupportedException("Can not encoding TLG");
            }

            return TlgNativePlugin.SaveTlg(bmp, tlg6);
        }

        public List<string> Extensions { get; } = new List<string> { ".tlg", ".tlg5", ".tlg6" };
        public bool CanToBitmap(in byte[] data, Dictionary<string, object> context = null)
        {
            return true;
        }

        public bool CanToBytes(Bitmap bitmap, Dictionary<string, object> context = null)
        {
            return TlgNativePlugin.IsEnabled;
        }

        public Bitmap ToBitmap(in byte[] data, Dictionary<string, object> context = null)
        {
            var bmp = LoadTlg(data, out var v);
            if (v > 5 && context != null)
            {
                context[TlgVersion] = v;
            }

            return bmp;
        }

        public byte[] ToBytes(Bitmap bitmap, Dictionary<string, object> context = null)
        {
            return SaveTlg(bitmap, context != null && context[TlgVersion] is int v && v == 6);
        }
    }
}
