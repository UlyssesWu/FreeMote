using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FreeMote.Plugins.Images
{
    [Export(typeof(IPsbImageFormatter))]
    [ExportMetadata("Name", "FreeMote.Astc")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "ASTC support.")]
    class AstcFormatter : IPsbImageFormatter
    {
        public AstcFormatter()
        {
            var toolPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? "", "Tools");
            foreach (var encoderTool in EncoderTools)
            {
                var tool = Path.Combine(toolPath, encoderTool);
                if (File.Exists(tool))
                {
                    ToolPath = tool;
                    return;
                }
            }
        }

        public string ToolPath { get; set; } = null;

        public List<string> Extensions { get; } = new() {".astc"};

        public static List<string> EncoderTools { get; } = new()
            {"astcenc.exe", "astcenc-sse2.exe", "astcenc-sse4.1.exe", "astcenc-avx2.exe"};

        public bool CanToBitmap(in byte[] data, Dictionary<string, object> context = null)
        {
            if (data.Take(4).SequenceEqual(AstcHeader.Magic))
            {
                return true;
            }

            return false;
        }

        public bool CanToBytes(Bitmap bitmap, Dictionary<string, object> context = null)
        {
            if (string.IsNullOrEmpty(ToolPath))
            {
                return false;
            }

            return true;
        }

        public Bitmap ToBitmap(in byte[] data, Dictionary<string, object> context = null)
        {
            if (AstcFile.IsAstcHeader(data))
            {
                var header = AstcFile.ParseAstcHeader(data);

                return RL.ConvertToImage(data, header.Height, header.Width, PsbPixelFormat.ASTC_8BPP);
            }

            return null;
        }

        public byte[] ToBytes(Bitmap bitmap, Dictionary<string, object> context = null)
        {
            return null;
        }
    }
}
