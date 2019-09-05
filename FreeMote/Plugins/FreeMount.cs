using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;

namespace FreeMote.Plugins
{
    /// <inheritdoc />
    /// <summary>
    /// FreeMote Plugin System
    /// </summary>
    public class FreeMount : IDisposable
    {
        private const string PLUGIN_DLL = "FreeMote.Plugins.dll";
        private const string PLUGIN_DIR = "Plugins";
        private const string LIB_DIR = "lib";


        [ImportMany] private IEnumerable<Lazy<IPsbShell, IPsbPluginInfo>> _shells;

        [ImportMany] private IEnumerable<Lazy<IPsbImageFormatter, IPsbPluginInfo>> _imageFormatters;

        [Import(AllowDefault = true)] private Lazy<IPsbKeyProvider, IPsbPluginInfo> _keyProvider;

        public Dictionary<string, IPsbShell> Shells { get; private set; } = new Dictionary<string, IPsbShell>();

        public Dictionary<string, IPsbImageFormatter> ImageFormatters { get; private set; } =
            new Dictionary<string, IPsbImageFormatter>();

        private CompositionContainer _container;
        private Dictionary<IPsbPlugin, IPsbPluginInfo> _plugins = new Dictionary<IPsbPlugin, IPsbPluginInfo>();
        private int _maxShellSigLength = 4;

        private static FreeMount _mount = null;
        internal static FreeMount _ => _mount ?? (_mount = new FreeMount());

        public static string CurrentPath => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
                                            Environment.CurrentDirectory;

        public static IEnumerable<IPsbPluginInfo> PluginInfos => _._plugins.Values;

        /// <summary>
        /// Loaded Plugins count
        /// </summary>
        public static int PluginsCount => _._plugins?.Count ?? 0;

        public static IPsbKeyProvider KeyProvider => _._keyProvider?.Value;

        /// <summary>
        /// Init Plugins
        /// <para>Must be called before using FreeMount features</para>
        /// </summary>
        public static void Init()
        {
            _.Init(null);
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

        public void Init(string path)
        {
            if (path == null)
            {
                path = Path.Combine(CurrentPath, PLUGIN_DIR);
            }

            //An aggregate catalog that combines multiple catalogs
            var catalog = new AggregateCatalog();
            //Adds all the parts found in the same assembly as the Program class
            AddCatalog(Path.Combine(CurrentPath, PLUGIN_DLL), catalog);
            AddCatalog(Path.Combine(CurrentPath, LIB_DIR, PLUGIN_DLL), catalog); //Allow load in lib folder
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
                Debug.WriteLine(compositionException.ToString());
            }

            UpdatePluginsCollection();
        }

        private void UpdatePluginsCollection()
        {
            Shells = new Dictionary<string, IPsbShell>();
            ImageFormatters = new Dictionary<string, IPsbImageFormatter>();
            foreach (var shell in _shells)
            {
                if (shell.Value.Signature?.Length > _maxShellSigLength)
                {
                    _maxShellSigLength = shell.Value.Signature.Length;
                }

                _plugins.Add(shell.Value, shell.Metadata);
                Shells.Add(shell.Value.Name, shell.Value);
            }

            foreach (var imageFormatter in _imageFormatters)
            {
                _plugins.Add(imageFormatter.Value, imageFormatter.Metadata);
                foreach (var extension in imageFormatter.Value.Extensions)
                {
                    ImageFormatters[extension] = imageFormatter.Value;
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
                catalog.Catalogs.Add(new AssemblyCatalog(Assembly.LoadFile(path)));
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
                f.Extensions.ForEach(ext => ImageFormatters.Remove(ext));
            }

            if (plugin is IPsbShell s)
            {
                Shells.Remove(s.Name);
            }

            if (plugin is IPsbKeyProvider)
            {
                _keyProvider = null;
            }

            _plugins.Remove(plugin);
        }

        public Bitmap ResourceToBitmap(string ext, in byte[] data, Dictionary<string, object> context = null)
        {
            if (ImageFormatters[ext] == null || !ImageFormatters[ext].CanToBitmap(data))
            {
                return null;
            }

            return ImageFormatters[ext].ToBitmap(data, context);
        }

        public byte[] BitmapToResource(string ext, Bitmap bitmap, Dictionary<string, object> context = null)
        {
            if (ImageFormatters[ext] == null || !ImageFormatters[ext].CanToBytes(bitmap))
            {
                return null;
            }

            return ImageFormatters[ext].ToBytes(bitmap, context);
        }

        public MemoryStream OpenFromShell(Stream stream, ref string type, Dictionary<string, object> context = null)
        {
            if (type != null)
            {
                return Shells[type]?.ToPsb(stream, context);
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