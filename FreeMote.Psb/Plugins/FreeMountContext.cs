using System.Collections.Generic;
using System.Drawing;
using System.IO;
using FreeMote.Psb;

namespace FreeMote.Plugins
{

    public class FreeMountContext
    {
        public PsbImageFormat ImageFormat { get; set; } = PsbImageFormat.png;
        
        public Dictionary<string, object> Context { get; set; }

        public FreeMountContext(Dictionary<string, object> context)
        {
            Context = context;
        }

        public bool TryGet<T>(string key, out T result)
        {
            result = default;
            if (Context == null)
            {
                return false;
            }

            if (!Context.ContainsKey(key))
            {
                return false;
            }

            if (Context[key] is not T)
            {
                return false;
            }

            result = (T)Context[key];
            return true;
        }

        public bool HasShell => Context.ContainsKey(Consts.Context_PsbShellType) && Context[Consts.Context_PsbShellType] != null && !string.IsNullOrEmpty(Context[Consts.Context_PsbShellType].ToString());

        public bool SupportImageExt(string ext)
        {
            return FreeMount._.ImageFormatters.ContainsKey(ext);
        }

        public bool SupportAudioExt(string ext)
        {
            return FreeMount._.AudioFormatters.ContainsKey(ext);
        }

        /// <summary>
        /// <inheritdoc cref="FreeMount.ArchDataToWave"/>
        /// </summary>
        /// <param name="ext"></param>
        /// <param name="metadata"></param>
        /// <param name="archData"></param>
        /// <param name="fileName">desired output file name hint (used to determine channel/pan)</param>
        /// <returns></returns>
        public byte[] ArchDataToWave(string ext, AudioMetadata metadata, IArchData archData, string fileName = "")
        {
            return FreeMount._.ArchDataToWave(ext, metadata, archData, fileName, Context);
        }

        /// <summary>
        /// <inheritdoc cref="FreeMount.WaveToArchData"/>
        /// </summary>
        /// <param name="ext"></param>
        /// <param name="wave"></param>
        /// <param name="fileName"></param>
        /// <param name="waveExt">"ext" in ArchData</param>
        /// <returns></returns>
        public bool WaveToArchData(AudioMetadata md, IArchData archData, string ext, in byte[] wave, string fileName = "", string waveExt = ".wav")
        {
            return FreeMount._.WaveToArchData(md, archData, ext, wave, fileName, waveExt, Context);
        }

        /// <summary>
        /// <inheritdoc cref="FreeMount.TryGetArchData"/>
        /// </summary>
        /// <param name="md"></param>
        /// <param name="channel"><inheritdoc cref="FreeMount.TryGetArchData"/></param>
        /// <param name="archData"></param>
        /// <returns></returns>
        public bool TryGetArchData(AudioMetadata md, PsbDictionary channel, out IArchData archData)
        {
            return FreeMount._.TryGetArchData(md, channel, out archData, Context);
        }

        /// <summary>
        /// Use plugins to convert resource bytes to bitmap
        /// </summary>
        /// <param name="ext"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public Bitmap ResourceToBitmap(string ext, in byte[] data)
        {
            return FreeMount._.ResourceToBitmap(ext, data, Context);
        }

        /// <summary>
        /// Use plugins to convert bitmap to resource bytes
        /// </summary>
        /// <param name="ext"></param>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        public byte[] BitmapToResource(string ext, Bitmap bitmap)
        {
            return FreeMount._.BitmapToResource(ext, bitmap, Context);
        }

        /// <summary>
        /// [RequireUsing] Use plugins to decompress shell types to PSB
        /// </summary>
        /// <param name="stream">the input stream won't be disposed automatically</param>
        /// <param name="type"></param>
        /// <returns>unpacked stream, remember to dispose!</returns>
        public MemoryStream OpenFromShell(Stream stream, ref string type)
        {
            return FreeMount._.OpenFromShell(stream, ref type, Context);
        }

        /// <summary>
        /// [RequireUsing] Use plugins to compress PSB to shell type
        /// </summary>
        /// <param name="input">The input stream. Might be disposed!</param>
        /// <param name="type"></param>
        /// <returns></returns>
        public MemoryStream PackToShell(Stream input, string type = null)
        {
            return FreeMount._.PackToShell(input, type ?? Context[Consts.Context_PsbShellType] as string, Context);
        }

        /// <summary>
        /// Use plugin to try to get PSB key
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public uint? GetKey(Stream input)
        {
            return FreeMount._.GetKey(input, Context);
        }

        /// <summary>
        /// [RequireUsing] Open stream from PSB file, unpack the shell if exists
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Stream OpenStreamFromPsbFile(string path)
        {
            var psbFs = File.OpenRead(path);

            var ctx = FreeMount.CreateContext();
            string type = null;
            Stream stream = psbFs;
            var psbMs = ctx.OpenFromShell(psbFs, ref type);
            if (psbMs != null)
            {
                ctx.Context[Consts.Context_PsbShellType] = type;
                psbFs.Dispose();
                stream = psbMs;
            }

            return stream;
        }
    }
}
