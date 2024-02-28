using System.Collections.Generic;
using System.Linq;

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
            //8bit (1 byte 1 pixel)
            if (psb.Objects["image"] is PsbResource res)
            {
                ImageMetadata md = new ImageMetadata()
                {
                    PsbType = PsbType,
                    Resource = res,
                    Width = psb.Objects["w"].GetInt(),
                    Height = psb.Objects["h"].GetInt(),
                    Spec = PsbSpec.none,
                    TypeString = PsbPixelFormat.A8.ToStringForPsb().ToPsbString()
                };
                return [md as T];
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
                    TypeString = PsbPixelFormat.CI8_PC.ToStringForPsb().ToPsbString(),
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
            //TODO:
            return default;
        }
    }
}
