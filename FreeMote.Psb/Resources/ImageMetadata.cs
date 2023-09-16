using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using FastBitmapLib;
using FreeMote.Plugins;

namespace FreeMote.Psb
{
    /// <summary>
    /// Information for Image Resource
    /// </summary>
    [DebuggerDisplay("{" + nameof(DebuggerString) + "}")]
    public class ImageMetadata : IResourceMetadata
    {
        private static readonly List<string> SupportedImageExt = new List<string> { ".png", ".bmp", ".jpg", ".jpeg" };

        /// <summary>
        /// Name 1
        /// </summary>
        public string Part { get; set; }

        /// <summary>
        /// Name 2
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Index is a value for tracking resource when compiling. For index appeared in texture name, see <seealso cref="TextureIndex"/>
        /// </summary>
        public uint Index
        {
            get => Resource.Index ?? uint.MaxValue;
            set
            {
                if (Resource != null)
                {
                    Resource.Index = value;
                }
            }
        }

        /// <summary>
        /// The texture index
        /// <code>"tex#001".TextureIndex = 1; "tex".Index = 0</code>
        /// </summary>
        public uint? TextureIndex => GetTextureIndex(Part);

        /// <summary>
        /// The texture index. e.g.
        /// <code>GetTextureIndex("tex#001") = 1</code>
        /// </summary>
        internal static uint? GetTextureIndex(string texName)
        {
            if (texName.EndsWith("tex") || texName.EndsWith("tex#000") || texName.EndsWith("tex000"))
            {
                return 0;
            }

            var texIdx = texName.LastIndexOf("tex", StringComparison.Ordinal);
            if (texIdx < 0)
            {
                return null;
            }

            var isValid = uint.TryParse(
                new string(texName.Skip(texIdx).SkipWhile(c => c < 48 || c > 57).TakeWhile(c => c > 48 || c < 57)
                    .ToArray()), out var index);
            if (!isValid)
            {
                return null;
            }

            return index;
        }

        public PsbCompressType Compress { get; set; }
        public bool Is2D { get; set; } = true;
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>
        /// [Type2]
        /// </summary>
        public int Top { get; set; }

        /// <summary>
        /// [Type2]
        /// </summary>
        public int Left { get; set; }

        public float OriginX { get; set; }
        public float OriginY { get; set; }

        /// <summary>
        /// Pixel Format Type
        /// </summary>
        public string Type => TypeString?.Value;

        public PsbString TypeString { get; set; }
        public RectangleF Clip { get; set; }
        public PsbResource Resource { get; set; }
        /// <summary>
        /// PIMG layer_type
        /// </summary>
        public int LayerType { get; set; }

        /// <summary>
        /// Pal
        /// </summary>
        public PsbResource Palette { get; set; }

        public PsbString PaletteTypeString { get; set; }

        /// <summary>
        /// Palette Pixel Format Type
        /// </summary>
        public string PalType => PaletteTypeString?.Value;

        public PsbPixelFormat PalettePixelFormat => PalType.ToPsbPixelFormat(Spec);

        public byte[] Data
        {
            get => Resource?.Data;

            internal set
            {
                if (Resource == null)
                {
                    throw new NullReferenceException("Resource is null");
                }

                Resource.Data = value;
            }
        }

        public byte[] PalData
        {
            get => Palette?.Data;

            internal set
            {
                Palette ??= new PsbResource();

                Palette.Data = value;
            }
        }

        /// <summary>
        /// Additional z-index info
        /// </summary>
        public float ZIndex { get; set; }

        /// <summary>
        /// The Label which this resource belongs to
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Name under object/{part}/motion/
        /// </summary>
        public string MotionName { get; set; }

        public int Opacity { get; set; } = 10;
        public bool Visible { get; set; } = true;

        /// <summary>
        /// Platform
        /// <para>Spec can not be get from source part, so set it before use</para>
        /// </summary>
        public PsbSpec Spec { get; set; } = PsbSpec.other;

        public PsbType PsbType { get; set; } = PsbType.Motion;
        
        private static bool IsPowOf2(int n)
        {
            return n >= 2 && (n & (n - 1)) == 0;
        }

        public ImageMetadata Clone()
        {
            return (ImageMetadata)MemberwiseClone();
        }

        /// <summary>
        /// Check if the <see cref="Data"/> looks correct
        /// </summary>
        /// <returns>Whether check is ok, and error message</returns>
        public (bool Valid, string CheckResult) Validate()
        {
            if (Data == null)
            {
                return (false, "Data is null");
            }

            if (PixelFormat.UsePalette() && PalData == null)
            {
                return (false, "PalData is null");
            }

            if (Compress == PsbCompressType.None)
            {
                if (PsbType == PsbType.Tachie && IsPowOf2(Width) && IsPowOf2(Height)) //could be combined image
                {
                    //skip
                }
                else
                {
                    var bitDepth = PixelFormat.GetBitDepth();
                    if (bitDepth != null)
                    {
                        var shouldBeLength = Math.Ceiling(Width * Height * (bitDepth.Value / 8.0));
                        if (Math.Abs(shouldBeLength - Data.Length) > 1)
                        {
                            return (false,
                                $"Data length check failed: Loaded content size = {Data.Length}, expected size = {shouldBeLength}");
                        }
                    }
                }
            }

            if (PixelFormat == PsbPixelFormat.CI4_SW || PixelFormat == PsbPixelFormat.CI8_SW)
            {
                if (!IsPowOf2(Width) || !IsPowOf2(Height))
                {
                    return (false, $"For Swizzle images, Width and Height should be 2^n, but currently are {Width} x {Height}");
                }
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Load <see cref="Data"/> and <see cref="PalData"/> from image file
        /// </summary>
        /// <param name="fullPath"></param>
        /// <param name="context"></param>
        public void Link(string fullPath, FreeMountContext context)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            {
                if (Consts.StrictMode)
                {
                    throw new FileNotFoundException("[ERROR] Cannot find file to Link.", fullPath);
                }
                else
                {
                    Logger.LogWarn($"[WARN] Cannot find file to Link at {fullPath}.");
                }

                return;
            }

            Data = LoadImageBytes(fullPath, context, out var palette);
            PalData = palette;
            var (valid, checkResult) = Validate();
            if (!valid)
            {
                Logger.LogWarn($"[WARN] Validation failed when linking {fullPath} . {checkResult}");
                Logger.LogWarn("Check your image format (bit depth, size, pixel format). You should keep it same as original.");
                if (Consts.StrictMode)
                {
                    throw new FormatException(checkResult);
                }
            }
            //return Data;
        }

        public PsbPixelFormat PixelFormat => Type.ToPsbPixelFormat(Spec);

        private string DebuggerString =>
            $"{(string.IsNullOrWhiteSpace(Part) ? "" : Part + "/")}{Name}({Width}*{Height}){(Compress == PsbCompressType.RL ? "[RL]" : "")}(#{Index})";

        /// <summary>
        /// Convert Resource to Image
        /// <para>Only works if <see cref="Resource"/>.Data is not null</para>
        /// </summary>
        /// <returns></returns>
        public Bitmap ToImage()
        {
            if (Resource.Data == null)
            {
                throw new Exception("Resource data is null");
            }

            switch (Compress)
            {
                case PsbCompressType.RL:
                    return RL.DecompressToImage(Resource.Data, Width, Height, PixelFormat);
                case PsbCompressType.Tlg:
                    using (var ms = new MemoryStream(Resource.Data))
                    {
                        return new TlgImageConverter().Read(new BinaryReader(ms));
                    }
                default:
                    return RL.ConvertToImage(Resource.Data, PalData, Width, Height, PixelFormat, PalettePixelFormat);
            }
        }

        /// <summary>
        /// Set Image to <see cref="PsbResource.Data"/>
        /// <para>(in memory version of <seealso cref="Link"/>)</para>
        /// </summary>
        /// <param name="bmp"></param>
        public void SetData(Bitmap bmp)
        {
            Data = null;

            switch (PixelFormat)
            {
                case PsbPixelFormat.ASTC_8BPP:
                case PsbPixelFormat.BC7:
                    Data = FreeMount.CreateContext().BitmapToResource(PixelFormat.ToExtensionString(), Spec, bmp);
                    break;
            }

            if (Data == null)
            {
                switch (Compress)
                {
                    case PsbCompressType.RL:
                        Data = RL.CompressImage(bmp, PixelFormat);
                        break;
                    case PsbCompressType.Tlg:
                        Data = FreeMount.CreateContext().BitmapToResource(Compress.ToExtensionString(), Spec, bmp);
                        break;
                    default:
                        Data = RL.GetPixelBytesFromImage(bmp, PixelFormat);
                        break;
                }
            }
            if (PixelFormat.UsePalette())
            {
                PalData = bmp.Palette.GetPaletteBytes(PalettePixelFormat);
            }
        }

        public override string ToString()
        {
            return $"{Part}/{Name}";
        }
        
        /// <summary>
        /// Name for export and import
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public string GetFriendlyName(PsbType type)
        {
            if (type == PsbType.Pimg && !string.IsNullOrWhiteSpace(Name))
            {
                return Path.GetFileNameWithoutExtension(PsbResHelper.EscapeStringForPath(Name));
            }

            if (string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Part))
            {
                if (Resource.Index != null)
                {
                    return Index.ToString();
                }

                return "";
            }

            return $"{PsbResHelper.EscapeStringForPath(Part)}{Consts.ResourceNameDelimiter}{PsbResHelper.EscapeStringForPath(Name)}";
        }

        internal byte[] LoadImageBytes(string path, FreeMountContext context,
            out byte[] palette)
        {
            palette = null;
            byte[] data;
            Bitmap image = null;
            var ext = Path.GetExtension(path)?.ToLowerInvariant();

            if (Compress == PsbCompressType.ByName && ext != null && Name != null &&
                Name.EndsWith(ext, true, null))
            {
                return File.ReadAllBytes(path);
            }

            switch (ext)
            {
                //tlg
                case ".tlg" when Compress == PsbCompressType.Tlg:
                    return File.ReadAllBytes(path);
                case ".tlg":
                    image = context.ResourceToBitmap(PsbCompressType.Tlg.ToExtensionString(), Width, Height, Spec, File.ReadAllBytes(path));
                    break;
                //astc
                case ".astc" when PixelFormat == PsbPixelFormat.ASTC_8BPP:
                    return AstcFile.CutHeader(File.ReadAllBytes(path));
                case ".astc":
                    image = context.ResourceToBitmap(PixelFormat.ToExtensionString(), Width, Height, Spec, File.ReadAllBytes(path));
                    break;
                //rl
                case ".rl" when Compress == PsbCompressType.RL:
                    return File.ReadAllBytes(path);
                case ".rl" when Compress == PsbCompressType.None:
                    return RL.Decompress(File.ReadAllBytes(path));
                case ".rl":
                    image = RL.DecompressToImage(File.ReadAllBytes(path), Width,
                        Height, PixelFormat);
                    break;
                //raw
                case ".raw" when Compress == PsbCompressType.None:
                    return File.ReadAllBytes(path);
                case ".raw" when PixelFormat == PsbPixelFormat.ASTC_8BPP:
                    return File.ReadAllBytes(path);
                case ".raw" when Compress == PsbCompressType.RL:
                    return RL.Compress(File.ReadAllBytes(path));
                case ".raw":
                    image = RL.ConvertToImage(File.ReadAllBytes(path), Width,
                        Height, PixelFormat);
                    break;
                //bin
                case ".bin":
                    if (!File.Exists(path) && File.Exists(Path.ChangeExtension(path, ".raw"))) //fallback
                    {
                        return File.ReadAllBytes(Path.ChangeExtension(path, ".raw"));
                    }
                    return File.ReadAllBytes(path);
                //image
                default:
                    if (SupportedImageExt.Contains(ext))
                    {
                        if (PixelFormat.UsePalette())
                        {
                            image = BitmapHelper.LoadBitmap(File.ReadAllBytes(path));
                            palette = image.Palette.GetPaletteBytes(PalettePixelFormat);
                        }
                        else
                        {
                            image = new Bitmap(path);
                        }
                    }
                    else if (context.SupportImageExt(ext))
                    {
                        image = context.ResourceToBitmap(ext, Width, Height, Spec, File.ReadAllBytes(path));
                    }
                    else
                    {
                        //MARK: No longer try to read files we don't know
                        //return File.ReadAllBytes(path);
                        return null;
                    }

                    break;
            }

            //From now we have get the image, now fetch pixel data
            context.TryGet(Consts.Context_DisableCombinedImage, out bool disableCombinedImage);
            if (PsbType == PsbType.Tachie && !disableCombinedImage) //Let's split Tachie
            {
                //Check if the source image is a combined image
                if (image.Width == Width && image.Height == Height && IsPowOf2(Width) && IsPowOf2(Height))
                {
                    //it's not a combined image, do nothing
                }
                else if ((image.Width >= Width || image.Height >= Height) && (image.Width >= Left || image.Height >= Height)) //there could be some redundant area in pieces in order to fit 2^n
                {
                    Bitmap chunk = new Bitmap(Width, Height, image.PixelFormat);
                    //it should be a combined image
                    using (FastBitmap f = chunk.FastLock())
                    {
                        f.CopyRegion(image, new Rectangle(Left, Top, Width, Height), new Rectangle(0, 0, Width, Height));
                    }

                    image.Dispose();
                    image = chunk;
                }
            }

            //Plugin Encoder, for PixelFormat or Compress
            switch (PixelFormat)
            {
                case PsbPixelFormat.ASTC_8BPP:
                case PsbPixelFormat.BC7:
                    data = context.BitmapToResource(PixelFormat.ToExtensionString(), Spec, image);
                    if (data != null)
                    {
                        return data;
                    }

                    if (PixelFormat == PsbPixelFormat.ASTC_8BPP)
                    {
                        var astcPath = Path.ChangeExtension(path, ".astc");
                        if (File.Exists(astcPath))
                        {
                            Logger.LogWarn($"[WARN] Can not encode ASTC, using {astcPath}");
                            return AstcFile.CutHeader(File.ReadAllBytes(astcPath));
                        }

                        Logger.LogWarn($"[WARN] Can not convert image to ASTC: {path}");
                        //data = File.ReadAllBytes(path);
                    }
                    break;
            }

            switch (Compress)
            {
                case PsbCompressType.RL:
                    data = RL.CompressImage(image, PixelFormat);
                    break;
                case PsbCompressType.Tlg:
                    data = context.BitmapToResource(Compress.ToExtensionString(), Spec, image);
                    if (data == null)
                    {
                        var tlgPath = Path.ChangeExtension(path, ".tlg");
                        if (File.Exists(tlgPath))
                        {
                            Logger.LogWarn($"[WARN] Can not encode TLG, using {tlgPath}");
                            data = File.ReadAllBytes(tlgPath);
                        }
                        else
                        {
                            Logger.LogWarn($"[WARN] Can not convert image to TLG: {path}");
                            //data = File.ReadAllBytes(path);
                        }
                    }
                    break;
                case PsbCompressType.ByName:
                    var imgExt = Path.GetExtension(Name);
                    if (context.SupportImageExt(imgExt))
                    {
                        data = context.BitmapToResource(imgExt, Spec, image);
                    }
                    else
                    {
                        Logger.LogWarn($"[WARN] Unsupported image: {path}");
                        data = File.ReadAllBytes(path);
                    }
                    break;
                case PsbCompressType.None:
                default:
                    data = RL.GetPixelBytesFromImage(image, PixelFormat);
                    break;
            }

            return data;
        }
    }
}