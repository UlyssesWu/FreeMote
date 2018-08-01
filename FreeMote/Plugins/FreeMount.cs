using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FreeMote.Plugins
{
    /// <summary>
    /// FreeMote Plugin System
    /// </summary>
    internal class FreeMount : IDisposable
    {
        public Dictionary<string, IPsbShell> Shells { get; private set; } = new Dictionary<string, IPsbShell>();

        public Dictionary<string, IPsbImageFormatter> ImageFormatters { get; private set; } =
            new Dictionary<string, IPsbImageFormatter>();

        public string CurrentPath => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;
        private const string PLUGIN_DLL = "FreeMote.Plugins.dll";
        private const string PLUGIN_DIR = "Plugins";
        private CompositionContainer _container;
        private Dictionary<IPsbPlugin, IPsbPluginInfo> _plugins = new Dictionary<IPsbPlugin, IPsbPluginInfo>();

        [ImportMany] private IEnumerable<Lazy<IPsbShell, IPsbPluginInfo>> _shells;

        [ImportMany] private IEnumerable<Lazy<IPsbImageFormatter, IPsbPluginInfo>> _imageFormatters;

        private static FreeMount _mount = null;
        public static FreeMount _ => _mount ?? (_mount = new FreeMount());

        public void Init(string path = null)
        {
            if (path == null)
            {
                path = Path.Combine(CurrentPath, PLUGIN_DIR);
            }
            //An aggregate catalog that combines multiple catalogs
            var catalog = new AggregateCatalog();
            //Adds all the parts found in the same assembly as the Program class
            AddCatalog(path, catalog);
            AddCatalog(Path.Combine(CurrentPath, PLUGIN_DLL), catalog);

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

        public Bitmap ResourceToBitmap(string ext, in byte[] data, Dictionary<string, object> context = null)
        {
            if (ImageFormatters[ext] == null || !ImageFormatters[ext].CanToBitmap(data))
            {
                return null;
            }

            return ImageFormatters[ext].ToBitmap(data);
        }

        public byte[] BitmapToResource(string ext, Bitmap bitmap, Dictionary<string, object> context = null)
        {
            if (ImageFormatters[ext] == null || !ImageFormatters[ext].CanToBytes(bitmap))
            {
                return null;
            }

            return ImageFormatters[ext].ToBytes(bitmap);
        }

        public Stream OpenFromShell(Stream stream, out string type, Dictionary<string, object> context = null)
        {
            foreach (var psbShell in Shells.Values)
            {
                if (psbShell.IsInShell(stream))
                {
                    type = psbShell.Name;
                    return psbShell.ToPsb(stream);
                }
            }

            type = null;
            return stream;
        }

        public bool PackToShell(Stream stream, string type, out Stream output, Dictionary<string, object> context = null)
        {
            if (Shells[type] == null)
            {
                output = stream;
                return false;
            }

            output = Shells[type].ToShell(stream);
            return true;
        }

        public void Dispose()
        {
            _container?.Dispose();
        }
    }
}
