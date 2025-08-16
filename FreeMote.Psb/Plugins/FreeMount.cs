using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FreeMote.Psb;

namespace FreeMote.Plugins
{
    /// <summary>
    /// FreeMote Plugin System
    /// </summary>
    public class FreeMount : IDisposable
    {
        private const string PLUGIN_DLL = "FreeMote.Plugins.dll";
        private const string PLUGIN_X64_DLL = "FreeMote.Plugins.x64.dll";
        private const string PLUGIN_DIR = "Plugins";
        private const string LIB_DIR = "lib";
        public const string ARG_DISABLE_PLUGINS = "--disable-plugins";


        [ImportMany] private IEnumerable<Lazy<IPsbShell, IPsbPluginInfo>> _shells;
        [ImportMany] private IEnumerable<Lazy<IPsbSpecialType, IPsbPluginInfo>> _specialTypes;

        [ImportMany] private IEnumerable<Lazy<IPsbImageFormatter, IPsbPluginInfo>> _imageFormatters;
        [ImportMany] private IEnumerable<Lazy<IPsbAudioFormatter, IPsbPluginInfo>> _audioFormatters;

        [Import(AllowDefault = true)] private Lazy<IPsbKeyProvider, IPsbPluginInfo> _keyProvider;

        public Dictionary<string, IPsbShell> Shells { get; private set; } = new();
        public Dictionary<string, IPsbSpecialType> SpecialTypes { get; private set; } = new();

        public List<(string Extension, IPsbImageFormatter Formatter)> ImageFormatters { get; private set; } = new();

        public List<(string Extension, IPsbAudioFormatter Formatter)> AudioFormatters { get; private set; } = new();

        private CompositionContainer _container;
        private readonly Dictionary<IPsbPlugin, IPsbPluginInfo> _plugins = new();
        private int _maxShellSigLength = 4;

        private static FreeMount _mount = null;
        internal static FreeMount _ => _mount ??= new FreeMount();

        public static string CurrentPath => Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ??
                                            Environment.CurrentDirectory;

        public static IEnumerable<IPsbPluginInfo> PluginInfos => _._plugins.Values;

        /// <summary>
        /// Loaded Plugins count
        /// </summary>
        public static int PluginsCount => _._plugins?.Count ?? 0;

        public static IPsbKeyProvider KeyProvider => _._keyProvider?.Value;

        /// <summary>
        /// Add inherit plugins
        /// </summary>
        /// <param name="catalog"></param>
        private void AddDefaultCatalogs(AggregateCatalog catalog)
        {
            catalog.Catalogs.Add(new TypeCatalog(typeof(MdfShell))); //MDF
            catalog.Catalogs.Add(new TypeCatalog(typeof(WavFormatter))); //Wav
            catalog.Catalogs.Add(new TypeCatalog(typeof(AudioFileFormatter))); //Audio file
        }

        /// <summary>
        /// Init Plugins
        /// <para>Must be called before using FreeMount features</para>
        /// <param name="path">Base path to find plugins</param>
        /// </summary>
        public static void Init(string path = null)
        {
            _.InitPlugins(path);
        }

        /// <summary>
        /// Dispose Plugins
        /// </summary>
        public static void Free()
        {
            _mount.Dispose();
            _mount = null;
        }

        /// <summary>
        /// Print Plugin infos
        /// </summary>
        /// <returns></returns>
        public static string PrintPluginInfos(int indent = 0)
        {
            if (_ == null || _._plugins.Count == 0)
            {
                return "";
            }

            StringBuilder sb = new StringBuilder();
            foreach (var psbShell in _._plugins)
            {
                if (indent > 0)
                {
                    sb.Append("".PadLeft(indent));
                }

                sb.AppendLine($"{psbShell.Value.Name} by {psbShell.Value.Author} : {psbShell.Value.Comment}");
            }

            return sb.ToString();
        }

        public static FreeMountContext CreateContext(Dictionary<string, object> context = null)
        {
            return new FreeMountContext(context ?? new Dictionary<string, object>());
        }

        /// <summary>
        /// Find plugin DLLs and init
        /// </summary>
        /// <param name="path">Base path to find plugins</param>
        public void InitPlugins(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = Path.Combine(CurrentPath, PLUGIN_DIR);
            }

            //An aggregate catalog that combines multiple catalogs
            var catalog = new AggregateCatalog();
            AddDefaultCatalogs(catalog);

            //Adds all the parts found in the same assembly as the Program class
            AddCatalog(Path.Combine(CurrentPath, PLUGIN_DLL), catalog);
            AddCatalog(Path.Combine(CurrentPath, LIB_DIR, PLUGIN_DLL), catalog); //Allow load in lib folder
            if (Environment.Is64BitProcess)
            {
                AddCatalog(Path.Combine(CurrentPath, PLUGIN_X64_DLL), catalog);
                AddCatalog(Path.Combine(CurrentPath, LIB_DIR, PLUGIN_X64_DLL), catalog); //Allow load in lib folder
            }
            AddCatalog(path, catalog); //Plugins folder can override default plugin

            //Create the CompositionContainer with the parts in the catalog
            _container = new CompositionContainer(catalog);
            //_container.ReleaseExport();
            //Fill the imports of this object
            try
            {
                _container.ComposeParts(this);
            }
            catch (CompositionException compositionException)
            {
                Logger.Log(compositionException.ToString());
            }

            UpdatePluginsCollection();
        }

        private void UpdatePluginsCollection()
        {
            Shells = new Dictionary<string, IPsbShell>();
            SpecialTypes = new Dictionary<string, IPsbSpecialType>();
            ImageFormatters = new List<(string, IPsbImageFormatter)>();
            AudioFormatters = new List<(string, IPsbAudioFormatter)>();

            foreach (var shell in _shells)
            {
                if (shell.Value.Signature?.Length > _maxShellSigLength)
                {
                    _maxShellSigLength = shell.Value.Signature.Length;
                }

                if (!_plugins.ContainsKey(shell.Value))
                {
                    _plugins.Add(shell.Value, shell.Metadata);
                }
                if (!Shells.ContainsKey(shell.Value.Name))
                {
                    Shells.Add(shell.Value.Name, shell.Value);
                }
            }

            foreach (var type in _specialTypes)
            {
                if (!_plugins.ContainsKey(type.Value))
                    _plugins.Add(type.Value, type.Metadata);
                if (!SpecialTypes.ContainsKey(type.Value.TypeId))
                    SpecialTypes.Add(type.Value.TypeId, type.Value);
            }

            foreach (var imageFormatter in _imageFormatters)
            {
                _plugins.Add(imageFormatter.Value, imageFormatter.Metadata);
                foreach (var extension in imageFormatter.Value.Extensions)
                {
                    var tuple = (extension, imageFormatter.Value);
                    if (!ImageFormatters.Contains(tuple))
                    {
                        ImageFormatters.Add(tuple);
                    }
                }
            }

            foreach (var audioFormatter in _audioFormatters)
            {
                if (!_plugins.ContainsKey(audioFormatter.Value))
                {
                    _plugins.Add(audioFormatter.Value, audioFormatter.Metadata);
                }
                foreach (var extension in audioFormatter.Value.Extensions)
                {
                    var tuple = (extension, audioFormatter.Value);
                    if (!AudioFormatters.Contains(tuple))
                    {
                        AudioFormatters.Add(tuple);
                    }
                }
            }

            if (_keyProvider != null)
            {
                _plugins.Add(_keyProvider.Value, _keyProvider.Metadata);
            }
        }

        private void AddCatalog(string path, AggregateCatalog catalog)
        {
            if (Directory.Exists(path))
            {
                catalog.Catalogs.Add(new DirectoryCatalog(path));
            }
            else if (File.Exists(path))
            {
                try
                {
                    catalog.Catalogs.Add(new AssemblyCatalog(Assembly.LoadFile(path)));
                }
                catch (NotSupportedException e)
                {
                    Logger.LogError($"[ERROR] Load plugin failed from {path}");
                    Logger.LogWarn(@"1. Are you running program on OneDrive or over local network? Try running on your local drive.
2. Try unblocking this file from properties. If you don't know how to do this, google it.");
                    Logger.LogWarn(e);
                }
            }
        }

        public IPsbPluginInfo GetInfo(IPsbPlugin plugin)
        {
            return _plugins[plugin];
        }

        public void Remove(IPsbPlugin plugin)
        {
            if (plugin is IPsbImageFormatter f)
            {
                ImageFormatters.RemoveAll(tuple => tuple.Formatter == f);
            }

            if (plugin is IPsbAudioFormatter a)
            {
                AudioFormatters.RemoveAll(tuple => tuple.Formatter == a);
            }

            if (plugin is IPsbShell s)
            {
                Shells.Remove(s.Name);
            }

            if (plugin is IPsbSpecialType t)
            {
                SpecialTypes.Remove(t.TypeId);
            }

            if (plugin is IPsbKeyProvider)
            {
                _keyProvider = null;
            }

            _plugins.Remove(plugin);
        }

        /// <summary>
        /// <inheritdoc cref="IPsbImageFormatter.ToBitmap"/>
        /// </summary>
        /// <param name="ext"></param>
        /// <param name="data"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="platform"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Bitmap ResourceToBitmap(string ext, in byte[] data, int width, int height, PsbSpec platform, Dictionary<string, object> context = null)
        {
            foreach (var (_, formatter) in ImageFormatters.Where(tuple => tuple.Extension == ext))
            {
                if (formatter.CanToBitmap(data, context))
                {
                    return formatter.ToBitmap(data, width, height, platform, context);
                }
            }

            return null;
        }

        /// <summary>
        /// <inheritdoc cref="IPsbAudioFormatter.ToWave"/>
        /// </summary>
        /// <param name="ext"></param>
        /// <param name="metadata"></param>
        /// <param name="archData"></param>
        /// <param name="fileName">desired output file name hint (used to determine channel/pan)</param>
        /// <param name="context"></param>
        /// <returns></returns>
        public byte[] ArchDataToWave(string ext, AudioMetadata metadata, IArchData archData, string fileName = null, Dictionary<string, object> context = null)
        {
            foreach (var (_, formatter) in AudioFormatters.Where(tuple => tuple.Extension == ext))
            {
                if (formatter.CanToWave(archData, context))
                {
                    return formatter.ToWave(metadata, archData, fileName, context);
                }
            }

            return null;
        }

        /// <summary>
        /// <inheritdoc cref="IPsbImageFormatter.ToBytes"/>
        /// </summary>
        /// <param name="ext"></param>
        /// <param name="bitmap"></param>
        /// <param name="platform"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public byte[] BitmapToResource(string ext, PsbSpec platform, Bitmap bitmap, Dictionary<string, object> context = null)
        {
            foreach (var (_, formatter) in ImageFormatters.Where(tuple => tuple.Extension == ext))
            {
                if (formatter.CanToBytes(bitmap, context))
                {
                    return formatter.ToBytes(bitmap, platform, context);
                }
            }

            return null;
        }

        /// <summary>
        /// <inheritdoc cref="IPsbAudioFormatter.ToArchData"/>
        /// </summary>
        /// <param name="md"></param>
        /// <param name="archData"></param>
        /// <param name="ext"></param>
        /// <param name="wave"></param>
        /// <param name="fileName"></param>
        /// <param name="waveExt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool WaveToArchData(AudioMetadata md, IArchData archData, string ext, byte[] wave, string fileName, string waveExt, Dictionary<string, object> context = null)
        {
            foreach (var (_, formatter) in AudioFormatters.Where(tuple => tuple.Extension == ext))
            {
                if (formatter.CanToArchData(wave, context))
                {
                    formatter.ToArchData(md, archData, wave, fileName, waveExt, context);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// <inheritdoc cref="IPsbAudioFormatter.TryGetArchData"/>
        /// </summary>
        /// <param name="md"></param>
        /// <param name="channel"><inheritdoc cref="IPsbAudioFormatter.TryGetArchData"/></param>
        /// <param name="archData"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool TryGetArchData(AudioMetadata md, PsbDictionary channel, out IArchData archData, Dictionary<string, object> context = null)
        {
            archData = null;
            foreach (var t in AudioFormatters)
            {
                if (t.Formatter.TryGetArchData(md, channel, out archData, context))
                {
                    return true;
                }
            }

            return false;
        }


        public MemoryStream OpenFromShell(Stream stream, ref string type, Dictionary<string, object> context = null)
        {
            if (type != null)
            {
                type = type.ToUpperInvariant(); //force upper case!
                if (type == "" && !Shells.ContainsKey(type))
                {
                    if (stream is MemoryStream ms)
                        return ms;

                    var mms = new MemoryStream((int)stream.Length);
                    stream.CopyTo(mms);
                    return mms;
                }

                if (Shells.ContainsKey(type))
                {
                    return Shells[type]?.ToPsb(stream, context);
                }

                if (_container == null)
                {
                    throw new NotSupportedException($"Shell type \"{type}\" is not found. The plugin system is not initialized. Call `FreeMount.Init()` first.");
                }

                throw new NotSupportedException($"Shell type \"{type}\" is not found in plugins. Make sure you have Plugins dlls in right place.");
            }

            //Detect signature
            var header = new byte[_maxShellSigLength];
            var pos = stream.Position;
            stream.Read(header, 0, _maxShellSigLength);
            stream.Position = pos;

            foreach (var psbShell in Shells.Values)
            {
                if (psbShell?.Signature == null)
                {
                    continue;
                }

                if (header.Take(psbShell.Signature.Length).SequenceEqual(psbShell.Signature))
                {
                    type = psbShell.Name;
                    return psbShell.ToPsb(stream, context);
                }
            }

            //Detailed detect
            foreach (var psbShell in Shells.Values)
            {
                if (psbShell == null)
                {
                    continue;
                }

                if (psbShell.IsInShell(stream))
                {
                    type = psbShell.Name;
                    return psbShell.ToPsb(stream, context);
                }
            }

            type = null;
            return null;
        }

        public MemoryStream PackToShell(Stream stream, string type, Dictionary<string, object> context = null)
        {
            type = type.ToUpperInvariant(); //force upper case!
            if (type == "PSB" || Shells[type] == null)
            {
                return null;
            }

            return Shells[type].ToShell(stream, context);
        }

        public uint? GetKey(Stream stream, Dictionary<string, object> context = null)
        {
            return _keyProvider?.Value?.GetKey(stream, context);
        }

        public void Dispose()
        {
            _container?.Dispose();
        }
    }
}