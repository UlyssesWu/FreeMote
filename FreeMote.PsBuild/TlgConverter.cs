using System;
using System.Drawing;
using System.IO;
using FreeMote.Plugins;

namespace FreeMote.PsBuild
{
    public static class TlgConverter
    {
        /// <summary>
        /// Use managed TLG loader if possible
        /// </summary>
        public static bool PreferManaged { get; set; } = false;
        /// <summary>
        /// Is TLG encode plugin enabled
        /// </summary>
        public static bool CanSaveTlg => TlgPlugin.IsEnabled;

        private static TlgImageConverter _managedConverter = null;

        /// <summary>
        /// Load TLG
        /// </summary>
        /// <param name="tlgData"></param>
        /// <param name="version">TLG version, can be 0(unknown),5,6</param>
        /// <returns></returns>
        public static Bitmap LoadTlg(byte[] tlgData, out int version)
        {
            if (!PreferManaged && TlgPlugin.IsEnabled)
            {
                try
                {
                    return TlgPlugin.LoadTlg(tlgData, out version);
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
        public static byte[] SaveTlg(this Bitmap bmp, bool tlg6 = false)
        {
            if (!CanSaveTlg)
            {
                throw new NotSupportedException("Can not encoding TLG");
            }

            return TlgPlugin.SaveTlg(bmp, tlg6);
        }
    }
}
