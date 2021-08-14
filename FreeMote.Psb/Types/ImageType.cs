using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FreeMote.Plugins;
using FreeMote.Psb.Textures;

namespace FreeMote.Psb.Types
{
    class ImageType : BaseImageType, IPsbType
    {
        public const string ImageSourceKey = "imageList";
        public PsbType PsbType => PsbType.Tachie;
        public bool IsThisType(PSB psb)
        {
            return psb.TypeId == "image"; //&& psb.Objects.ContainsKey("imageList")
        }

        public override Dictionary<string, string> OutputResources(PSB psb, FreeMountContext context, string name, string dirPath,
            PsbExtractOption extractOption = PsbExtractOption.Original)
        {
            bool allExtracted = true;
            //Extra Extract
            if (extractOption == PsbExtractOption.Extract)
            {
                if (psb.Type == PsbType.Tachie)
                {
                    var bitmaps = TextureCombiner.CombineTachie(psb);
                    foreach (var kv in bitmaps)
                    {
                        kv.Value.CombinedImage.Save(Path.Combine(dirPath, $"{kv.Key}{context.ImageFormat.DefaultExtension()}"), context.ImageFormat.ToImageFormat());
                    }

                    //Only output combined image
                    context.TryGet(Consts.Context_DisableCombinedImage, out bool disableCombinedImage);
                    if (!disableCombinedImage)
                    {
                        Dictionary<string, string> resources = new Dictionary<string, string>();
                        var images = psb.CollectResources<ImageMetadata>();
                        foreach (var md in images)
                        {
                            if (bitmaps.ContainsKey(md.Part))
                            {
                                if (!bitmaps[md.Part].Parts.Any(p => p.Resource.Index != null && p.Index == md.Index))
                                {
                                    Console.WriteLine($"[WARN] Image is not fully combined: {md}");
                                    allExtracted = false;
                                    break;
                                }
                                resources.Add(md.Index.ToString(), $"{name}/{md.Part}{context.ImageFormat.DefaultExtension()}");
                            }
                        }

                        if (allExtracted)
                        {
                            return resources;
                        }
                    }

                    Console.WriteLine("[WARN] Combined image won't be used when compiling. Now extracting all chunks...");
                }
            }

            return base.OutputResources(psb, context, name, dirPath, extractOption);
        }
        
        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : IResourceMetadata
        {
            List<T> resourceList = psb.Resources == null
                ? new List<T>()
                : new List<T>(psb.Resources.Count);

            FindTachieResources(resourceList, psb.Objects[ImageSourceKey]);

            return resourceList;
        }

        private static void FindTachieResources<T>(List<T> list, IPsbValue obj, string currentLabel = "") where T : IResourceMetadata
        {
            switch (obj)
            {
                case PsbList c:
                    c.ForEach(o => FindTachieResources(list, o, currentLabel));
                    break;
                case PsbDictionary d:
                    if (d["label"] is PsbString label)
                    {
                        if (string.IsNullOrWhiteSpace(currentLabel))
                        {
                            currentLabel = label;
                        }
                        else
                        {
                            currentLabel = string.Join("-", currentLabel, label);
                        }
                    }

                    if (d[Consts.ResourceKey] is PsbResource r)
                    {
                        list.Add((T)(IResourceMetadata)GenerateTachieResMetadata(d, r, currentLabel));
                    }

                    foreach (var o in d.Values)
                    {
                        FindTachieResources(list, o, currentLabel);
                    }

                    break;
            }
        }

        private static ImageMetadata GenerateTachieResMetadata(PsbDictionary d, PsbResource r, string label = "")
        {
            int width = 1, height = 1;
            int top = 0, left = 0;
            var dd = d.Parent as PsbDictionary ?? d;
            if ((d["width"] ?? d["truncated_width"] ?? dd["width"]) is PsbNumber nw)
            {
                width = (int)nw;
            }

            if ((d["height"] ?? d["truncated_height"] ?? dd["height"]) is PsbNumber nh)
            {
                height = (int)nh;
            }

            if ((dd["top"] ?? d["top"]) is PsbNumber nx)
            {
                top = nx.AsInt;
            }

            if ((dd["left"] ?? d["left"]) is PsbNumber ny)
            {
                left = ny.AsInt;
            }

            var md = new ImageMetadata()
            {
                Top = top,
                Left = left,
                TypeString = d["type"] as PsbString,
                Width = width,
                Height = height,
                Name = r.Index.ToString(),
                Part = label,
                Resource = r,
                PsbType = PsbType.Tachie
            };

            if (md.PixelFormat == PsbPixelFormat.ASTC_8BPP)
            {
                md.Compress = PsbCompressType.Astc;
            }

            return md;
        }
    }
}
