//TexturePacker by mfascia
//https://github.com/mfascia/TexturePacker

#define USE_FASTBITMAP

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
#if USE_FASTBITMAP
using FastBitmapLib;

#endif

namespace FreeMote.Psb.Textures
{
    public enum TextureEdgeProcess
    {
        None = 0,

        /// <summary>
        /// Expand for 1px
        /// </summary>
        ///TODO: Expand n px?
        ///TODO: Since the packer starts from top-left, there is no padding space left for top & left, only down & right. Need more samples.
        Expand1Px,
    }

    /// <summary>
    /// Represents a Texture in an atlas
    /// </summary>
    public class TextureInfo
    {
        /// <summary>
        /// Path of the source texture on disk
        /// </summary>
        public string Source;

        /// <summary>
        /// Width in Pixels
        /// </summary>
        public int Width;

        /// <summary>
        /// Height in Pixels
        /// </summary>
        public int Height;

        /// <summary>
        /// Source texture in memory
        /// </summary>
        public Image SourceImage;
    }

    /// <summary>
    /// Indicates in which direction to split an unused area when it gets used
    /// </summary>
    public enum SplitType
    {
        /// <summary>
        /// Split Horizontally (textures are stacked up)
        /// </summary>
        Horizontal,

        /// <summary>
        /// Split verticaly (textures are side by side)
        /// </summary>
        Vertical,
    }

    /// <summary>
    /// Different types of heuristics in how to use the available space
    /// </summary>
    public enum BestFitHeuristic
    {
        /// <summary>
        /// 
        /// </summary>
        Area,

        /// <summary>
        /// 
        /// </summary>
        MaxOneAxis,
    }

    /// <summary>
    /// A node in the Atlas structure
    /// </summary>
    public class Node
    {
        /// <summary>
        /// Bounds of this node in the atlas
        /// </summary>
        public Rectangle Bounds;

        /// <summary>
        /// Texture this node represents
        /// </summary>
        public TextureInfo Texture;

        /// <summary>
        /// If this is an empty node, indicates how to split it when it will be used
        /// </summary>
        public SplitType SplitType;
    }

    /// <summary>
    /// The texture atlas
    /// </summary>
    public class Atlas
    {
        public Atlas(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Padding = 0;

        /// <summary>
        /// Width in pixels
        /// </summary>
        public int Width;

        /// <summary>
        /// Height in Pixel
        /// </summary>
        public int Height;

        /// <summary>
        /// List of the nodes in the Atlas. This will represent all the textures that are packed into it and all the remaining free space
        /// </summary>
        public List<Node> Nodes;

#if USE_FASTBITMAP
        private void ApplyEdgeProcessFast(TextureEdgeProcess edge, FastBitmap f, Bitmap s, Node n)
        {
            if (edge == TextureEdgeProcess.Expand1Px)
            {
                if (Padding < 2) // partL expand 1px to right, partR expand 1px to left, therefore 2px needed
                {
                    throw new ArgumentOutOfRangeException(nameof(edge), "Padding must > 2 when using Texture Edge Expand");
                }

                //source: pick a slice from part; dest: put slice to atlas
                //top
                f.CopyRegion(s, new Rectangle(0, 0, s.Width, 1),
                    new Rectangle(n.Bounds.X, n.Bounds.Y - 1, s.Width, 1));
                //down
                f.CopyRegion(s, new Rectangle(0, s.Height - 1, s.Width, 1),
                    new Rectangle(n.Bounds.X, n.Bounds.Y + n.Bounds.Height, s.Width, 1));
                //left
                f.CopyRegion(s, new Rectangle(0, 0, 1, s.Height),
                    new Rectangle(n.Bounds.X - 1, n.Bounds.Y, 1, s.Height));
                //right
                f.CopyRegion(s, new Rectangle(s.Width - 1, 0, 1, s.Height),
                    new Rectangle(n.Bounds.X + n.Bounds.Width, n.Bounds.Y, 1, s.Height));
            }
        }
#endif

        public Image ToImage(bool debugMode = false, TextureEdgeProcess edge = TextureEdgeProcess.None, Color? background = null)
        {
            var bgColor = background ?? Color.FromArgb(0, Color.Black);
            Bitmap img = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            //avoid using Graphics
#if USE_FASTBITMAP
            if (!debugMode)
            {
                using (var f = img.FastLock())
                {
                    if (background != null)
                    {
                        f.Clear(bgColor);
                    }

                    foreach (Node n in Nodes)
                    {
                        if (n.Texture != null)
                        {
                            Image sourceImg = n.Texture.SourceImage ?? new Bitmap(n.Texture.Source);
                            if (!(sourceImg is Bitmap s))
                            {
                                s = new Bitmap(sourceImg);
                            }

                            f.CopyRegion(s, new Rectangle(0, 0, s.Width, s.Height), n.Bounds);

                            ApplyEdgeProcessFast(edge, f, s, n);
                        }
                    }
                }

                //img.Save("tex.png", ImageFormat.png);
                return img;
            }
#endif

            Graphics g = Graphics.FromImage(img);
            g.Clear(bgColor);

            //g.PixelOffsetMode = PixelOffsetMode.Half;
            //g.InterpolationMode = InterpolationMode.Default;
            //g.SmoothingMode = SmoothingMode.None;

            //ImageAttributes attributes = new ImageAttributes();
            //attributes.SetWrapMode(WrapMode.TileFlipXY);

            if (debugMode)
            {
                g.FillRectangle(Brushes.Green, new Rectangle(0, 0, Width, Height));
            }

            foreach (Node n in Nodes)
            {
                if (n.Texture != null)
                {
                    Image sourceImg = n.Texture.SourceImage ?? new Bitmap(n.Texture.Source);
#if USE_FASTBITMAP
                    using (var f = img.FastLock())
                    {
                        if (!(sourceImg is Bitmap s))
                        {
                            s = new Bitmap(sourceImg);
                        }

                        f.CopyRegion(s, new Rectangle(0, 0, s.Width, s.Height), n.Bounds);

                        ApplyEdgeProcessFast(edge, f, s, n);
                    }
#else
                    g.DrawImage(sourceImg, n.Bounds);
                    //g.DrawImage(sourceImg, n.Bounds, 0,0, sourceImg.Width, sourceImg.Height, GraphicsUnit.Pixel, attributes);
#endif

                    if (debugMode)
                    {
                        string label = Path.GetFileNameWithoutExtension(n.Texture.Source);
                        SizeF labelBox = g.MeasureString(label, SystemFonts.MenuFont, new SizeF(n.Bounds.Size));
                        RectangleF rectBounds = new Rectangle(n.Bounds.Location,
                            new Size((int) labelBox.Width, (int) labelBox.Height));
                        g.FillRectangle(Brushes.Black, rectBounds);
                        g.DrawString(label, SystemFonts.MenuFont, Brushes.White, rectBounds);
                    }
                }
                else
                {
                    g.FillRectangle(Brushes.DarkMagenta, n.Bounds);

                    if (debugMode)
                    {
                        string label = n.Bounds.Width + "x" + n.Bounds.Height;
                        SizeF labelBox = g.MeasureString(label, SystemFonts.MenuFont, new SizeF(n.Bounds.Size));
                        RectangleF rectBounds = new Rectangle(n.Bounds.Location,
                            new Size((int) labelBox.Width, (int) labelBox.Height));
                        g.FillRectangle(Brushes.Black, rectBounds);
                        g.DrawString(label, SystemFonts.MenuFont, Brushes.White, rectBounds);
                    }
                }
            }

            return img;
        }
    }

    class TexturePacker
    {
        /// <summary>
        /// List of all the textures that need to be packed
        /// </summary>
        public List<TextureInfo> SourceTextures;

        /// <summary>
        /// Stream that recieves all the info logged
        /// </summary>
        public StringWriter Log;

        /// <summary>
        /// Stream that recieves all the error info
        /// </summary>
        public StringWriter Error;

        /// <summary>
        /// Number of pixels that separate textures in the atlas
        /// </summary>
        public int Padding;

        /// <summary>
        /// Size of the atlas in pixels. Represents one axis, as atlases are square
        /// </summary>
        public int AtlasSize;

        /// <summary>
        /// Toggle for debug mode, resulting in debug atlasses to check the packing algorithm
        /// </summary>
        public bool DebugMode;

        /// <summary>
        /// Toggle for Scrapbook mode - just paste all textures to same size
        /// </summary>
        public bool ScrapbookMode = false;
        
        /// <summary>
        /// Which heuristic to use when doing the fit
        /// </summary>
        public BestFitHeuristic FitHeuristic;

        /// <summary>
        /// List of all the output atlases
        /// </summary>
        public List<Atlas> Atlasses;

        public TexturePacker()
        {
            SourceTextures = new List<TextureInfo>();
            Log = new StringWriter();
            Error = new StringWriter();
        }

        public void Process(string sourceDir, string pattern, int atlasSize, int padding, bool debugMode)
        {
            Padding = padding;
            AtlasSize = atlasSize;
            DebugMode = debugMode;

            //1: scan for all the textures we need to pack
            ScanForTextures(sourceDir, pattern);

            Process();
        }

        /// <summary>
        /// Result saved in <see cref="Atlasses"/>
        /// </summary>
        /// <param name="images"></param>
        /// <param name="atlasSize"></param>
        /// <param name="padding"></param>
        /// <param name="debugMode"></param>
        public void Process(IDictionary<string, Image> images, int atlasSize, int padding, bool debugMode = false)
        {
            Padding = padding;
            AtlasSize = atlasSize;
            DebugMode = debugMode;

            //1: Load all the textures we need to pack
            LoadTexturesFromImages(images);

            Process();
        }

        public Bitmap CellProcess(IDictionary<string, Image> images,
            Dictionary<string, (int oriX, int oriY, int width, int height)> origins, int paddingWidth, int paddingHeight,
            out int cellWidth, out int cellHeight, int mode = 0, bool debugMode = false)
        {
            AtlasSize = 8192;
            LoadTexturesFromImages(images);

            cellWidth = 0;
            cellHeight = 0;
            foreach (var image in images)
            {
                //TODO: width and height?
                if (origins[image.Key].width > cellWidth)
                {
                    cellWidth = origins[image.Key].width;
                }

                if (origins[image.Key].height > cellHeight)
                {
                    cellHeight = origins[image.Key].height;
                }
            }

            cellWidth += paddingWidth;
            cellHeight += paddingHeight;

            //TODO: best arrange method?
            //Firstly implement a straight packer
            int texWidth = 3 * cellWidth;
            int texHeight = cellHeight * (1 + images.Count * 2);
            Bitmap img = new Bitmap(texWidth, texHeight, PixelFormat.Format32bppArgb);
            //avoid using Graphics
            int posX = cellWidth;
            int posY = cellHeight;
            Atlasses = new List<Atlas>(1);
            Atlas atlas = new Atlas(texWidth, texHeight)
            {
                Padding = Math.Min(paddingWidth, paddingHeight),
                Nodes = new List<Node>()
            };
            Atlasses.Add(atlas);

#if USE_FASTBITMAP

            using (var f = img.FastLock())
            {
                f.Clear(Color.FromArgb(255, 0, 255, 0));
                foreach (var image in images)
                {
                    Node n = new Node();
                    n.Texture = new TextureInfo
                    {
                        Source = image.Key,
                        Width = image.Value.Width,
                        Height = image.Value.Height
                    };
                    n.Bounds = new Rectangle(posX, posY, cellWidth, cellHeight);
                    atlas.Nodes.Add(n);
                    f.ClearRegion(n.Bounds, Color.Transparent);
                    var bmp = (Bitmap) image.Value;
                    int centerX = posX + cellWidth / 2;
                    int centerY = posY + cellHeight / 2;
                    int leftTopX = centerX - origins[image.Key].oriX;
                    int leftTopY = centerY - origins[image.Key].oriY;
                    f.CopyRegion(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height),
                        new Rectangle(leftTopX, leftTopY, bmp.Width, bmp.Height));
                    posY += 2 * cellHeight;
                }
            }

            //img.Save("tex.png", ImageFormat.png);
            return img;


#else
            throw new NotImplementedException("This feature requires FastBitmapLib.");
#endif
        }

        private void Process()
        {
            List<TextureInfo> textures = new List<TextureInfo>();
            textures = SourceTextures.ToList();

            //2: generate as many atlasses as needed (with the latest one as small as possible)
            Atlasses = new List<Atlas>();
            while (textures.Count > 0)
            {
                Atlas atlas = new Atlas(AtlasSize, AtlasSize) {Padding = Padding};

                List<TextureInfo> leftovers = LayoutAtlas(textures, atlas);

                if (leftovers.Count == 0)
                {
                    // we reached the last atlas. Check if this last atlas could have been twice smaller
                    while (leftovers.Count == 0)
                    {
                        atlas.Width /= 2;
                        atlas.Height /= 2;
                        leftovers = LayoutAtlas(textures, atlas);
                    }

                    // we need to go 1 step larger as we found the first size that is to small
                    atlas.Width *= 2;
                    atlas.Height *= 2;
                    leftovers = LayoutAtlas(textures, atlas);
                }

                Atlasses.Add(atlas);

                textures = leftovers;
            }
        }

        public void SaveAtlasses(string destination)
        {
            int atlasCount = 0;
            string prefix = destination.Replace(Path.GetExtension(destination), "");

            string descFile = destination;
            StreamWriter tw = new StreamWriter(destination);
            tw.WriteLine("source_tex, atlas_tex, u, v, scale_u, scale_v");

            foreach (Atlas atlas in Atlasses)
            {
                string atlasName = String.Format(prefix + "{0:000}" + ".png", atlasCount);

                //1: Save images
                Image img = atlas.ToImage(debugMode: DebugMode);
                img.Save(atlasName, System.Drawing.Imaging.ImageFormat.Png);

                //2: save description in file
                foreach (Node n in atlas.Nodes)
                {
                    if (n.Texture != null)
                    {
                        tw.Write(n.Texture.Source + ", ");
                        tw.Write(atlasName + ", ");
                        tw.Write(((float) n.Bounds.X / atlas.Width) + ", ");
                        tw.Write(((float) n.Bounds.Y / atlas.Height) + ", ");
                        tw.Write(((float) n.Bounds.Width / atlas.Width) + ", ");
                        tw.WriteLine(((float) n.Bounds.Height / atlas.Height).ToString(CultureInfo.InvariantCulture));
                    }
                }

                ++atlasCount;
            }

            tw.Close();

            tw = new StreamWriter(prefix + ".log");
            tw.WriteLine("--- LOG -------------------------------------------");
            tw.WriteLine(Log.ToString());
            tw.WriteLine("--- ERROR -----------------------------------------");
            tw.WriteLine(Error.ToString());
            tw.Close();
        }

        private void LoadTexturesFromImages(IDictionary<string, Image> images)
        {
            foreach (var imgPair in images)
            {
                var img = imgPair.Value;
                if (img.Width <= AtlasSize && img.Height <= AtlasSize)
                {
                    TextureInfo ti = new TextureInfo
                    {
                        Source = imgPair.Key,
                        Width = img.Width,
                        Height = img.Height,
                        SourceImage = imgPair.Value
                    };

                    SourceTextures.Add(ti);
                }
                else
                {
                    throw new ArgumentOutOfRangeException($"Image {imgPair.Key} is too large to be hold in texture!");
                }
            }
        }

        internal void LoadTexturesFromImages(IList<Image> images)
        {
            foreach (var img in images)
            {
                if (img.Width <= AtlasSize && img.Height <= AtlasSize)
                {
                    TextureInfo ti = new TextureInfo
                    {
                        Source = img.Tag.ToString(),
                        Width = img.Width,
                        Height = img.Height,
                        SourceImage = img
                    };

                    SourceTextures.Add(ti);
                }
                else
                {
                    throw new ArgumentOutOfRangeException($"Image {img.Tag} is too large to be hold in texture!");
                }
            }
        }

        private void ScanForTextures(string path, string wildcard)
        {
            DirectoryInfo di = new DirectoryInfo(path);
            FileInfo[] files = di.GetFiles(wildcard, SearchOption.AllDirectories);

            foreach (FileInfo fi in files)
            {
                Image img;
                try
                {
                    img = Image.FromFile(fi.FullName);
                }
                catch (Exception e)
                {
                    Error.WriteLine(fi.FullName + " load failed. Skipping! " + e.ToString());
                    continue;
                }

                if (img.Width <= AtlasSize && img.Height <= AtlasSize)
                {
                    TextureInfo ti = new TextureInfo
                    {
                        Source = fi.FullName,
                        Width = img.Width,
                        Height = img.Height
                    };

                    SourceTextures.Add(ti);

                    Log.WriteLine("Added " + fi.FullName);
                }
                else
                {
                    Error.WriteLine(fi.FullName + " is too large to fix in the atlas. Skipping!");
                }
            }
        }

        private void HorizontalSplit(Node toSplit, int width, int height, List<Node> list)
        {
            Node n1 = new Node
            {
                Bounds =
                {
                    X = toSplit.Bounds.X + width + Padding,
                    Y = toSplit.Bounds.Y,
                    Width = toSplit.Bounds.Width - width - Padding,
                    Height = height
                },
                SplitType = SplitType.Vertical
            };

            Node n2 = new Node
            {
                Bounds =
                {
                    X = toSplit.Bounds.X,
                    Y = toSplit.Bounds.Y + height + Padding,
                    Width = toSplit.Bounds.Width,
                    Height = toSplit.Bounds.Height - height - Padding
                },
                SplitType = SplitType.Horizontal
            };

            if (n1.Bounds.Width > 0 && n1.Bounds.Height > 0)
                list.Add(n1);
            if (n2.Bounds.Width > 0 && n2.Bounds.Height > 0)
                list.Add(n2);
        }

        private void VerticalSplit(Node toSplit, int width, int height, List<Node> list)
        {
            Node n1 = new Node
            {
                Bounds =
                {
                    X = toSplit.Bounds.X + width + Padding,
                    Y = toSplit.Bounds.Y,
                    Width = toSplit.Bounds.Width - width - Padding,
                    Height = toSplit.Bounds.Height
                },
                SplitType = SplitType.Vertical
            };

            Node n2 = new Node
            {
                Bounds =
                {
                    X = toSplit.Bounds.X,
                    Y = toSplit.Bounds.Y + height + Padding,
                    Width = width,
                    Height = toSplit.Bounds.Height - height - Padding
                },
                SplitType = SplitType.Horizontal
            };

            if (n1.Bounds.Width > 0 && n1.Bounds.Height > 0)
                list.Add(n1);
            if (n2.Bounds.Width > 0 && n2.Bounds.Height > 0)
                list.Add(n2);
        }

        private TextureInfo FindBestFitForNode(Node node, List<TextureInfo> textures)
        {
            TextureInfo bestFit = null;

            float nodeArea = node.Bounds.Width * node.Bounds.Height;
            float maxCriteria = 0.0f;

            foreach (TextureInfo ti in textures)
            {
                switch (FitHeuristic)
                {
                    // Max of Width and Height ratios
                    case BestFitHeuristic.MaxOneAxis:
                        if (ti.Width <= node.Bounds.Width && ti.Height <= node.Bounds.Height)
                        {
                            float wRatio = (float) ti.Width / (float) node.Bounds.Width;
                            float hRatio = (float) ti.Height / (float) node.Bounds.Height;
                            float ratio = wRatio > hRatio ? wRatio : hRatio;
                            if (ratio > maxCriteria)
                            {
                                maxCriteria = ratio;
                                bestFit = ti;
                            }
                        }

                        break;

                    // Maximize Area coverage
                    case BestFitHeuristic.Area:

                        if (ti.Width <= node.Bounds.Width && ti.Height <= node.Bounds.Height)
                        {
                            float textureArea = ti.Width * ti.Height;
                            float coverage = textureArea / nodeArea;
                            if (coverage > maxCriteria)
                            {
                                maxCriteria = coverage;
                                bestFit = ti;
                            }
                        }

                        break;
                }
            }

            return bestFit;
        }

        private List<TextureInfo> LayoutAtlas(List<TextureInfo> _Textures, Atlas _Atlas)
        {
            List<Node> freeList = new List<Node>();
            List<TextureInfo> textureInfos = new List<TextureInfo>();

            _Atlas.Nodes = new List<Node>();

            textureInfos = _Textures.ToList();

            Node root = new Node();
            root.Bounds.Size = new Size(_Atlas.Width, _Atlas.Height);
            root.SplitType = SplitType.Horizontal;

            freeList.Add(root);

            while (freeList.Count > 0 && textureInfos.Count > 0)
            {
                Node node = freeList[0];
                freeList.RemoveAt(0);

                TextureInfo bestFit = FindBestFitForNode(node, textureInfos);
                if (bestFit != null)
                {
                    if (node.SplitType == SplitType.Horizontal)
                    {
                        HorizontalSplit(node, bestFit.Width, bestFit.Height, freeList);
                    }
                    else
                    {
                        VerticalSplit(node, bestFit.Width, bestFit.Height, freeList);
                    }

                    node.Texture = bestFit;
                    node.Bounds.Width = bestFit.Width;
                    node.Bounds.Height = bestFit.Height;

                    textureInfos.Remove(bestFit);
                }

                _Atlas.Nodes.Add(node);
            }

            return textureInfos;
        }
    }

    class Program
    {
        static void DisplayInfo()
        {
            Console.WriteLine("  usage: TexturePacker -sp xxx -ft xxx -o xxx [-s xxx] [-b x] [-d]");
            Console.WriteLine("            -sp | --sourcepath : folder to recursively scan for textures to pack");
            Console.WriteLine("            -ft | --filetype   : types of textures to pack (*.png only for now)");
            Console.WriteLine("            -o  | --output     : name of the atlas file to generate");
            Console.WriteLine("            -s  | --size       : size of 1 side of the atlas file in pixels. Default = 1024");
            Console.WriteLine("            -b  | --border     : nb of pixels between textures in the atlas. Default = 0");
            Console.WriteLine("            -d  | --debug      : output debug info in the atlas");
            Console.WriteLine("  ex: TexturePacker -sp C:\\Temp\\Textures -ft *.png -o C:\\Temp\atlas.txt -s 512 -b 2 --debug");
        }

        static void Main(string[] args)
        {
            Console.WriteLine("TexturePacker - Package rect/non pow 2 textures into square power of 2 atlas");

            if (args.Length == 0)
            {
                DisplayInfo();
                return;
            }

            List<string> prms = args.ToList();

            string sourcePath = "";
            string searchPattern = "";
            string outName = "";
            int textureSize = 1024;
            int border = 0;
            bool debug = false;

            for (int ip = 0; ip < prms.Count; ++ip)
            {
                prms[ip] = prms[ip].ToLowerInvariant();

                switch (prms[ip])
                {
                    case "-sp":
                    case "--sourcepath":
                        if (!prms[ip + 1].StartsWith("-"))
                        {
                            sourcePath = prms[ip + 1];
                            ++ip;
                        }

                        break;

                    case "-ft":
                    case "--filetype":
                        if (!prms[ip + 1].StartsWith("-"))
                        {
                            searchPattern = prms[ip + 1];
                            ++ip;
                        }

                        break;

                    case "-o":
                    case "--output":
                        if (!prms[ip + 1].StartsWith("-"))
                        {
                            outName = prms[ip + 1];
                            ++ip;
                        }

                        break;

                    case "-s":
                    case "--size":
                        if (!prms[ip + 1].StartsWith("-"))
                        {
                            textureSize = int.Parse(prms[ip + 1]);
                            ++ip;
                        }

                        break;

                    case "-b":
                    case "--border":
                        if (!prms[ip + 1].StartsWith("-"))
                        {
                            border = int.Parse(prms[ip + 1]);
                            ++ip;
                        }

                        break;

                    case "-d":
                    case "--debug":
                        debug = true;
                        break;
                }
            }

            if (sourcePath == "" || searchPattern == "" || outName == "")
            {
                DisplayInfo();
                return;
            }

            Console.WriteLine("Processing, please wait");

            TexturePacker packer = new TexturePacker();

            packer.Process(sourcePath, searchPattern, textureSize, border, debug);
            packer.SaveAtlasses(outName);
        }
    }
}