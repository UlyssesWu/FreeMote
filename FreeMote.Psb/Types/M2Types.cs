using System.Collections.Generic;
using System.IO;
using FreeMote.Plugins;

namespace FreeMote.Psb.Types
{
    //M2, our dear inventor of broken wheels

    class SprBlockType : BaseImageType, IPsbType
    {
        public PsbType PsbType => PsbType.SprBlock;

        public bool IsThisType(PSB psb)
        {
            return psb.Objects is {Count: 3} && psb.Objects.ContainsKey("w") && psb.Objects.ContainsKey("h") &&
                   psb.Objects.ContainsKey("image");
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : class, IResourceMetadata
        {
            if (psb.Objects["image"] is PsbResource res)
            {
                //test bit depth, for image it's 8bit (1 byte 1 pixel); for palette it's 32bit (4 byte 1 pixel)
                var width = psb.Objects["w"].GetInt();
                var height = psb.Objects["h"].GetInt();
                var dataLen = res.Data.Length;
                var depth = dataLen / (width * height);
                PsbPixelFormat format = PsbPixelFormat.A8;
                switch (depth)
                {
                    case 1:
                        format = PsbPixelFormat.A8;
                        break;
                    case 4:
                        format = PsbPixelFormat.LeRGBA8;
                        break;
                    default:
                        Logger.LogWarn($"Unknown color format: {depth} bytes per pixel. Please submit sample.");
                        return [];
                }

                ImageMetadata md = new ImageMetadata()
                {
                    PsbType = PsbType,
                    Resource = res,
                    Width = width,
                    Height = height,
                    Spec = PsbSpec.none,
                    TypeString = format.ToStringForPsb().ToPsbString()
                };
                return [md as T];
            }

            return [];
        }
    }

    //spr: data: 0: image no. 1:max to 1024 2:max to block[0]-1 3:max to block[1]-1
    //block: [0] * [1] = total sprites in a tex
    class SprDataType : BaseImageType, IPsbType
    {
        public PsbType PsbType => PsbType.SprData;
        public bool IsThisType(PSB psb)
        {
            return psb.Objects != null && psb.Objects.ContainsKey("spr_data") && psb.Objects.ContainsKey("tex_size");
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : class, IResourceMetadata
        {
            if (string.IsNullOrEmpty(psb.FilePath) || !File.Exists(psb.FilePath))
            {
                //Logger.LogHint("To get images from SprData PSB, you have to put all related PSBs in a folder first.");
                return [];
            }

            //TODO: parse header_ex later, no sample yet.
            return [];
        }

        public override Dictionary<string, string> OutputResources(PSB psb, FreeMountContext context, string name, string dirPath,
            PsbExtractOption extractOption = PsbExtractOption.Original)
        {
            if (string.IsNullOrEmpty(psb.FilePath) || !File.Exists(psb.FilePath))
            {
                Logger.LogHint("To get images from SprData PSB, you have to put all related PSBs in a folder first.");
                return [];
            }

            var basePath = Path.GetDirectoryName(psb.FilePath);
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
        public PsbType PsbType => PsbType.ChipImg;
        public bool IsThisType(PSB psb)
        {
            return psb.Objects is { Count: 1 } && psb.Objects.ContainsKey("chip") && psb.Objects["chip"] is PsbList;
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : class, IResourceMetadata
        {
            //there is nothing we can do for a 3D model's UV tex
            return [];
        }
    }
}
