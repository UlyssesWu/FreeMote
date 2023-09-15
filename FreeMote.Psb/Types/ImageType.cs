using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FreeMote.Plugins;
using FreeMote.Psb.Textures;

namespace FreeMote.Psb.Types
{
    /// <summary>
    /// ImageList (Tachie) type
    /// </summary>
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
                    var bitmaps = TextureCombiner.CombineTachie(psb, out var hasPalette);
                    foreach (var kv in bitmaps)
                    {
                        kv.Value.CombinedImage.Save(Path.Combine(dirPath, $"{kv.Key}{context.ImageFormat.DefaultExtension()}"), context.ImageFormat.ToImageFormat());
                        //if (kv.Value.OriginHasPalette)
                        //{
                        //    if (kv.Value.CombinedWithPalette)
                        //    {
                        //        Console.WriteLine($"[Hint] {kv.Key} is a combined image with palette rebuilt. The colors in it may loss or differs from original.{Environment.NewLine}  Use `-dci` to generate original pieces for recompile.");
                        //    }
                        //    else
                        //    {
                        //        Console.WriteLine($"[WARN]{kv.Key} is a combined image with palette dropped. Piece images for this image will be generated and used when compiling.");
                        //    }
                        //}
                    }

                    ////Remove combined images which are not keeping palettes, so pieces will be generated and used.
                    //var notCombinedWithPalettes = bitmaps
                    //    .Where(pair => pair.Value.OriginHasPalette && !pair.Value.CombinedWithPalette)
                    //    .Select(pair => pair.Key).ToList();
                    //foreach (var notCombinedWithPalette in notCombinedWithPalettes)
                    //{
                    //    bitmaps.Remove(notCombinedWithPalette);
                    //}

                    //Only output combined image
                    context.TryGet(Consts.Context_DisableCombinedImage, out bool disableCombinedImage);
                    if (hasPalette && !disableCombinedImage)
                    {
                        Logger.LogWarn("[WARN] Found images with palette (Indexed images). Piece images will be used when compiling.");
                        disableCombinedImage = true;
                    }

                    if (psb.Platform == PsbSpec.ps3)
                    {
                        Logger.LogWarn("[WARN] PS3 PSB cannot be recompiled with combined image. Piece images will be used when compiling.");
                        disableCombinedImage = true;
                    }

                    if (!disableCombinedImage) //try only output combined image, but check if all resources are combined
                    {
                        Dictionary<string, string> resources = new Dictionary<string, string>();
                        var images = psb.CollectResources<ImageMetadata>();
                        foreach (var md in images)
                        {
                            if (bitmaps.ContainsKey(md.Part))
                            {
                                if (!bitmaps[md.Part].Parts.Any(p => p.Resource.Index != null && p.Index == md.Index))
                                {
                                    Logger.LogWarn($"[WARN] Image is not fully combined: {md}");
                                    allExtracted = false;
                                    break;
                                }

                                var resourceIdx = md.Index.ToString();
                                if (!resources.ContainsKey(resourceIdx)) //prevent resource reuse //TODO: will there be same pixel but not same pal? That will be horrible...
                                {
                                    resources.Add(resourceIdx, $"{name}/{md.Part}{context.ImageFormat.DefaultExtension()}");
                                }
                            }
                        }

                        if (allExtracted)
                        {
                            return resources;
                        }
                    }

                    Logger.LogWarn("[WARN] Combined image won't be used when compiling. Now extracting all pieces...");
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
                        list.Add((T)(IResourceMetadata)GenerateTachieResMetadata(d, r, false, currentLabel));
                    }

                    foreach (var o in d.Values)
                    {
                        FindTachieResources(list, o, currentLabel);
                    }

                    break;
            }
        }

        private static ImageMetadata GenerateTachieResMetadata(PsbDictionary d, PsbResource r, bool duplicatePalette = false, string label = "")
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

            PsbResource palResource = null;
            PsbString palTypeString = null;
            if (d["pal"] is PsbResource palRes)
            {
                if (duplicatePalette)
                {
                    palResource = new PsbResource(palRes.Index);
                    d["pal"] = palResource;
                }
                else
                {
                    palResource = palRes;
                }

                palTypeString = d["palType"] as PsbString;
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
                Palette = palResource,
                PaletteTypeString = palTypeString,
                PsbType = PsbType.Tachie
            };

            return md;
        }
    }
}
