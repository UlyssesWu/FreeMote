using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FreeMote.Plugins;

namespace FreeMote.Psb.Types
{
    //M2, our dear inventor of broken wheels

    class SprBlockType : BaseImageType, IPsbType
    {
        public PsbType PsbType => PsbType.SprBlock;

        public bool IsThisType(PSB psb)
        {
            if (psb.Objects != null && psb.Objects.All(kv => kv.Value is PsbDictionary {Count: 3} dic && dic.ContainsKey("w") &&
                                                             dic.ContainsKey("h") &&
                                                             dic.ContainsKey("image"))) return true;

            if (psb.Objects is PsbDictionary
                    {
                        Count: 3
                    }
                    dic2 && dic2.ContainsKey("w") &&
                dic2.ContainsKey("h") &&
                dic2.ContainsKey("image"))
            {
                return true;
            }

            return false;
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : class, IResourceMetadata
        {
            var results = new List<IResourceMetadata>();
            if (psb.Objects is
                    {
                        Count: 3
                    }
                    dic && dic.ContainsKey("w") &&
                dic.ContainsKey("h") &&
                dic.ContainsKey("image"))
            {
                var md = GenerateMetadata("0", dic, dic["image"] as PsbResource);
                results.Add(md);
            }

            foreach (var kv in psb.Objects)
            {
                var d = kv.Value as PsbDictionary;
                if (d?["image"] is PsbResource res)
                {
                    var md = GenerateMetadata(kv.Key, d, res);
                    results.Add(md);
                }
            }

            return results.Cast<T>().ToList();

            ImageMetadata GenerateMetadata(string name, PsbDictionary dic, PsbResource res)
            {
                //test bit depth, for image it's 8bit (1 byte 1 pixel); for palette it's 32bit (4 byte 1 pixel)
                var width = dic["w"].GetInt();
                var height = dic["h"].GetInt();
                var dataLen = res.Data.Length;
                var depth = dataLen / (width * height);
                PsbPixelFormat format = PsbPixelFormat.A8;
                byte[] palette = null;
                switch (depth)
                {
                    case 1:
                        format = PsbPixelFormat.CI8;
                        palette = new byte[256 * 4];
                        // fill with 256 degrees of gray
                        for (int i = 0; i < 256; i++)
                        {
                            palette[i * 4] = (byte) i;
                            palette[i * 4 + 1] = (byte) i;
                            palette[i * 4 + 2] = (byte) i;
                            palette[i * 4 + 3] = 0xFF;
                        }

                        break;
                    case 4:
                        format = PsbPixelFormat.LeRGBA8;
                        break;
                    default:
                        Logger.LogWarn($"Unknown color format: {depth} bytes per pixel. Please submit sample.");
                        return null;
                }

                var md = new ImageMetadata()
                {
                    Name = name,
                    PsbType = PsbType,
                    Resource = res,
                    Width = width,
                    Height = height,
                    Spec = PsbSpec.none,
                    TypeString = format.ToStringForPsb().ToPsbString(),
                    Palette = new PsbResource() {Data = palette},
                };

                return md;
            }
        }
    }

    internal struct SprTile
    {
        public ushort TexId;
        public ushort Id;
        public int X;
        public int Y;

        public override string ToString()
        {
            return $"[{TexId},{Id},{X},{Y}]";
        }
    }

    //spr: data: 0: image no. 1:max to 1024 2:max to block[0]-1 3:max to block[1]-1
    //block: [0] * [1] = total sprites in a tex
    //spr tile: 32x32 tile
    class SprDataType : BaseImageType, IPsbType
    {
        public PsbType PsbType => PsbType.SprData;

        public bool IsThisType(PSB psb)
        {
            return psb.Objects != null && psb.Objects.ContainsKey("spr_data") && psb.Objects.ContainsKey("tex_size");
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : class, IResourceMetadata
        {
            return [];
        }

        public override Dictionary<string, string> OutputResources(PSB psb, FreeMountContext context, string name, string dirPath,
            PsbExtractOption extractOption = PsbExtractOption.Original)
        {
            string basePath = string.Empty;
            if (context.TryGet(Consts.Context_RT_SprBasePath, out string sprBasePath))
            {
                context.Context.Remove(Consts.Context_RT_SprBasePath);
                if (Directory.Exists(sprBasePath))
                {
                    basePath = sprBasePath;
                }
            }

            if (string.IsNullOrEmpty(basePath) && !string.IsNullOrEmpty(psb.FilePath))
            {
                basePath = Path.GetDirectoryName(psb.FilePath);
            }

            if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
            {
                //Logger.LogWarn("Cannot find related files. To get images from SprData PSB, you have to put all related PSBs in same folder.");
                return [];
            }

            return [];
        }
    }


    class ClutType : BaseImageType, IPsbType
    {
        public PsbType PsbType => PsbType.ClutImg;

        public bool IsThisType(PSB psb)
        {
            return psb.Objects is {Count: 2} && psb.Objects.ContainsKey("clut") && psb.Objects.ContainsKey("image");
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : class, IResourceMetadata
        {
            var cluts = psb.Objects["clut"] as PsbList;
            var images = psb.Objects["image"] as PsbList;

            if (images == null || images.Count == 0 || cluts == null || cluts.Count != images.Count)
            {
                return [];
            }

            List<T> resList = new List<T>(images.Count);
            for (int i = 0; i < images.Count; i++)
            {
                var img = images[i] as PsbDictionary;
                if (img == null)
                {
                    continue;
                }

                var clut = cluts[i] as PsbDictionary;
                if (clut == null)
                {
                    continue;
                }

                var h = img["h"].GetInt();
                var w = img["w"].GetInt();
                var r = img["image"] as PsbResource;
                var cr = clut["image"] as PsbResource;
                var ch = clut["h"].GetInt();
                var cw = clut["w"].GetInt();
                if (ch * cw != 256)
                {
                    Logger.LogWarn($"Not supported: clut [{i}] is a > 256 colors palette ({cw}x{ch}). Skip.");
                    continue;
                }

                resList.Add(new ImageMetadata()
                {
                    Name = i.ToString(),
                    Width = w,
                    Height = h,
                    Resource = r,
                    Palette = cr,
                    PsbType = PsbType,
                    TypeString = PsbPixelFormat.CI8.ToStringForPsb().ToPsbString(),
                } as T);
            }

            return resList;
        }
    }

    class ChipType : BaseImageType, IPsbType
    {
        public PsbType PsbType => PsbType.Chip;

        public bool IsThisType(PSB psb)
        {
            return psb.Objects is {Count: 1} && psb.Objects.ContainsKey("chip") && psb.Objects["chip"] is PsbList;
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : class, IResourceMetadata
        {
            //there is nothing we can do for a 3D model's UV tex
            return [];
        }
    }

    class MpdType : IPsbType
    {
        public PsbType PsbType => PsbType.Mpd;

        public bool IsThisType(PSB psb)
        {
            return psb.Objects.ContainsKey("mpd") && psb.Objects["mpd"] is PsbResource && psb.Objects.ContainsKey("tex") && psb.Objects.ContainsKey("offset");
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : class, IResourceMetadata
        {
            List<T> resourceList = psb.Resources == null
                ? new List<T>()
                : new List<T>(psb.Resources.Count);

            if (psb.Objects.ContainsKey("mpd") && psb.Objects["mpd"] is PsbResource mpd)
            {
                resourceList.Add((T)(IResourceMetadata)new BinaryMetadata() { Resource = mpd });
            }
            return resourceList;
        }

        public void Link(PSB psb, FreeMountContext context, IList<string> resPaths, string baseDir = null, PsbLinkOrderBy order = PsbLinkOrderBy.Convention)
        {
            var path = resPaths.Select(p => Path.Combine(baseDir ?? string.Empty, p)).FirstOrDefault(File.Exists);
            if (path == null)
            {
                return;
            }

            var mpdList = psb.CollectResources<BinaryMetadata>();
            PsbResource mpd;
            if (mpdList == null || mpdList.Count == 0)
            {
                mpd = new PsbResource();
                psb.Objects["mpd"] = mpd;
            }
            else
            {
                mpd = mpdList[0].Resource;
            }

            mpd.Data = File.ReadAllBytes(path);
        }

        public void Link(PSB psb, FreeMountContext context, IDictionary<string, string> resPaths, string baseDir = null)
        {
            if (!resPaths.TryGetValue("mpd", out var path))
            {
                return;
            }

            var fullPath = Path.Combine(baseDir ?? string.Empty, path);
            if (!File.Exists(fullPath))
            {
                return;
            }

            var mpdList = psb.CollectResources<BinaryMetadata>();
            PsbResource mpd;
            if (mpdList == null || mpdList.Count == 0)
            {
                mpd = new PsbResource();
                psb.Objects["mpd"] = mpd;
            }
            else
            {
                mpd = mpdList[0].Resource;
            }
            
            mpd.Data = File.ReadAllBytes(fullPath);
        }

        public void UnlinkToFile(PSB psb, FreeMountContext context, string name, string dirPath, bool outputUnlinkedPsb = true,
            PsbLinkOrderBy order = PsbLinkOrderBy.Name)
        {
            var mpdList = psb.CollectResources<BinaryMetadata>();
            if (mpdList == null || mpdList.Count == 0)
            {
                return;
            }

            var mpd = mpdList[0];
            //var pureName = Path.GetFileNameWithoutExtension(name);
            var outPath = Path.Combine(dirPath, $"{name}/0.mpd");
            File.WriteAllBytes(outPath, mpd.Data);
            if (outputUnlinkedPsb)
            {
                psb.Objects["mpd"] = PsbNull.Null;
                psb.Resources.Remove(mpd.Resource);
            }
        }

        public Dictionary<string, string> OutputResources(PSB psb, FreeMountContext context, string name, string dirPath,
            PsbExtractOption extractOption = PsbExtractOption.Original)
        {
            var mpdList = psb.CollectResources<BinaryMetadata>();
            if (mpdList == null || mpdList.Count == 0)
            {
                return [];
            }

            var mpd = mpdList[0];
            //var pureName = Path.GetFileNameWithoutExtension(name);
            var outPath = Path.Combine(dirPath, "0.mpd");
            File.WriteAllBytes(outPath, mpd.Data);
            return new Dictionary<string, string> { { "mpd", $"{name}/0.mpd" } };
        }
    }
}