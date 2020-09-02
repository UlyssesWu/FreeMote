using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using FreeMote.Plugins;
using FreeMote.Psb.Types;

namespace FreeMote.Psb
{
    public static class PsbResHelper
    {
        public static byte[] TryToWave(this IArchData archData, FreeMountContext context)
        {
            return context?.ArchDataToWave(archData.Extension, archData);
        }

        /// <summary>
        /// Get all resources with necessary info
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="deDuplication">if true, we focus on Resource itself </param>
        /// <returns></returns>
        public static List<T> CollectResources<T>(this PSB psb, bool deDuplication = true) where T : IResourceMetadata
        {
            List<T> resourceList;
            if (psb.TypeHandler != null)
            {
                resourceList = psb.TypeHandler.CollectResources<T>(psb, deDuplication);
            }
            else
            {
                resourceList = new List<T>();
                if (psb.Resources != null)
                    resourceList.AddRange(psb.Resources.Select(r => new ImageMetadata {Resource = r}).Cast<T>());
            }

            //Set Spec
            resourceList.ForEach(r => r.Spec = psb.Platform);
            resourceList.Sort((md1, md2) => (int) (md1.Index - md2.Index));

            return resourceList;
        }

        public static void LinkImages(PSB psb, FreeMountContext context, IList<string> resPaths, string baseDir = null,
            PsbLinkOrderBy order = PsbLinkOrderBy.Convention, bool isExternal = false)
        {
            if (isExternal)
            {
                MotionType.MotionResourceInstrument(psb);
            }

            var rawResList = psb.CollectResources<ImageMetadata>();
            if (order == PsbLinkOrderBy.Order)
            {
                for (int i = 0; i < rawResList.Count; i++)
                {
                    var resMd = rawResList[i];
                    var fullPath = Path.Combine(baseDir ?? "", resPaths[i]);
                    resMd.Link(fullPath, context);
                }

                return;
            }

            var resList = rawResList.ToList();

            if (order == PsbLinkOrderBy.Name)
            {
                if (psb.Platform == PsbSpec.krkr)
                {
                    throw new InvalidOperationException(
                        $"Can not link by file name for krkr PSB. Please consider using {PsbLinkOrderBy.Convention}");
                }

                resList.Sort((md1, md2) =>
                    (int) (((ImageMetadata) md1).TextureIndex ?? 0) - (int) (((ImageMetadata) md2).TextureIndex ?? 0));
            }

            for (var i = 0; i < resPaths.Count; i++)
            {
                var resPath = resPaths[i];
                var resName = Path.GetFileNameWithoutExtension(resPath);
                //var resMd = uint.TryParse(resName, out uint rid)
                //    ? resList.FirstOrDefault(r => r.Index == rid)
                //    : resList.FirstOrDefault(r =>
                //        resName == $"{r.Part}{PsbResCollector.ResourceNameDelimiter}{r.Name}");

                //Scan for Resource
                ImageMetadata resMd = null;
                if (order == PsbLinkOrderBy.Name)
                {
                    if (resName == null)
                    {
                        continue;
                    }

                    if (resList.Count == 1 && resPaths.Count == 1)
                    {
                        //If there is only one resource and one texture, we won't care about file name.
                        resMd = resList[0];
                    }
                    else
                    {
                        var texIdx = ImageMetadata.GetTextureIndex(resName);

                        if (texIdx == null)
                        {
                            Console.WriteLine($"[WARN]{resPath} is not used since the file name cannot be recognized.");
                            continue;
                        }

                        if (resList.Count <= texIdx.Value)
                        {
                            Console.WriteLine($"[WARN]{resPath} is not used since the tex No. is too large.");
                            continue;
                        }

                        resMd = resList[(int) texIdx.Value];
                    }
                }
                else //if (order == PsbLinkOrderBy.Convention)
                {
                    resMd = resList.FirstOrDefault(r =>
                        resName == $"{r.Part}{Consts.ResourceNameDelimiter}{r.Name}");
                    if (resMd == null && uint.TryParse(resName, out uint rid))
                    {
                        //This Link has no support for raw palette
                        resMd = resList.FirstOrDefault(r => r.Index == rid);
                    }

                    if (resMd == null && psb.Type == PsbType.Pimg)
                    {
                        resMd = resList.FirstOrDefault(r => resName == Path.GetFileNameWithoutExtension(r.Name));
                    }
                }


                if (resMd == null)
                {
                    Console.WriteLine($"[WARN]{resPath} is not used.");
                    continue;
                }

                var fullPath = Path.Combine(baseDir ?? "", resPath.Replace('/', '\\'));
                resMd.Link(fullPath, context);
            }
        }

        internal static void LinkImages(PSB psb, FreeMountContext context, IDictionary<string, string> resources,
            string baseDir = null)
        {
            var resList = psb.CollectResources<ImageMetadata>();

            foreach (var resxResource in resources)
            {
                //Scan for Resource
                var resMd = resList.FirstOrDefault(r =>
                    resxResource.Key == r.GetFriendlyName(psb.Type));
                if (resMd == null && psb.Type == PsbType.Pimg)
                {
                    resMd = resList.FirstOrDefault(r => resxResource.Key == Path.GetFileNameWithoutExtension(r.Name));
                }

                if (resMd == null && uint.TryParse(resxResource.Key, out uint rid))
                {
                    resMd = resList.FirstOrDefault(r => r.Index == rid);
                    if (resMd == null)
                    {
                        //support raw palette
                        var palResMds = resList.FindAll(r => r.Palette?.Index == rid);
                        if (palResMds.Count > 0)
                        {
                            var palFullPath = Path.IsPathRooted(resxResource.Value)
                                ? resxResource.Value
                                : Path.Combine(baseDir ?? "", resxResource.Value.Replace('/', '\\'));
                            var palRawData = File.ReadAllBytes(palFullPath);
                            foreach (var palResMd in palResMds)
                            {
                                palResMd.PalData = palRawData;
                            }

                            continue;
                        }
                    }
                }

                if (resMd == null)
                {
                    Console.WriteLine($"[WARN]{resxResource.Key} is not used.");
                    continue;
                }

                var fullPath = Path.IsPathRooted(resxResource.Value)
                    ? resxResource.Value
                    : Path.Combine(baseDir ?? "", resxResource.Value.Replace('/', '\\'));
                resMd.Link(fullPath, context);
            }
        }

        /// <summary>
        /// Inlined PSB -> External Texture PSB. Inverse of Link
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="order">To make a regular external texture PSB you should set it to <see cref="PsbLinkOrderBy.Name"/>.</param>
        /// <param name="disposeResInPsb">Whether to remove resources in PSB. To make a real external texture PSB you should set it to true.</param>
        /// <returns>Ordered textures</returns>
        public static List<Bitmap> UnlinkImages(PSB psb, PsbLinkOrderBy order = PsbLinkOrderBy.Name, bool disposeResInPsb = true)
        {
            var resources = psb.CollectResources<ImageMetadata>();
            List<Bitmap> texs = new List<Bitmap>();

            for (int i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                var tex = RL.ConvertToImage(resource.Data, resource.PalData, resource.Height, resource.Width,
                    resource.PixelFormat, resource.PalettePixelFormat);

                switch (order)
                {
                    case PsbLinkOrderBy.Convention:
                        tex.Tag = resource.GetFriendlyName(psb.Type);
                        break;
                    default:
                        var tId = resource.TextureIndex;
                        if (tId == null)
                        {
                            throw new FormatException(
                                "Unable to unlink with texture names since they can't be recognized.");
                        }

                        tex.Tag = $"tex{tId.Value:D3}";
                        break;
                }

                texs.Add(tex);

                //Finally, dispose
                if (disposeResInPsb)
                {
                    resource.Data = null;
                }
            }

            return texs;
        }

        public static void UnlinkImagesToFile(PSB psb, FreeMountContext context, string name, string dirPath,
            bool disposeResInPsb = true,
            PsbLinkOrderBy order = PsbLinkOrderBy.Name)
        {
            var texs = UnlinkImages(psb, order, disposeResInPsb);

            var texExt = context.ImageFormat.DefaultExtension();
            var texFormat = context.ImageFormat.ToImageFormat();

            switch (order)
            {
                case PsbLinkOrderBy.Convention:
                    foreach (var tex in texs)
                    {
                        tex.Save(Path.Combine(dirPath, tex.Tag + texExt), texFormat);
                    }

                    break;
                case PsbLinkOrderBy.Name:
                    foreach (var tex in texs)
                    {
                        tex.Save(Path.Combine(dirPath, $"{name}_{tex.Tag}{texExt}"), texFormat);
                    }

                    break;
                case PsbLinkOrderBy.Order:
                    for (var i = 0; i < texs.Count; i++)
                    {
                        var tex = texs[i];
                        tex.Save(Path.Combine(dirPath, $"{i}{texExt}"), texFormat);
                    }

                    break;
            }

            return;
        }

        public static Dictionary<string, string> OutputImageResources(PSB psb, FreeMountContext context, string name,
            string dirPath,
            PsbExtractOption extractOption = PsbExtractOption.Original,
            PsbImageFormat extractFormat = PsbImageFormat.png)
        {
            var resources = psb.CollectResources<ImageMetadata>();

            Dictionary<string, string> resDictionary = new Dictionary<string, string>();

            ImageFormat pixelFormat;
            switch (extractFormat)
            {
                case PsbImageFormat.png:
                    pixelFormat = ImageFormat.Png;
                    break;
                default:
                    pixelFormat = ImageFormat.Bmp;
                    break;
            }

            if (extractOption == PsbExtractOption.Original)
            {
                for (int i = 0; i < psb.Resources.Count; i++)
                {
                    var relativePath = psb.Resources[i].Index == null ? $"#{i}.bin" : $"{psb.Resources[i].Index}.bin";

                    File.WriteAllBytes(
                        Path.Combine(dirPath, relativePath),
                        psb.Resources[i].Data);
                    resDictionary.Add(Path.GetFileNameWithoutExtension(relativePath), $"{name}/{relativePath}");
                }
            }
            else
            {
                for (int i = 0; i < resources.Count; i++)
                {
                    var resource = resources[i];
                    //Generate Friendly Name
                    var friendlyName = resource.GetFriendlyName(psb.Type);
                    string relativePath = friendlyName;
                    if (string.IsNullOrWhiteSpace(friendlyName))
                    {
                        relativePath = resource.Resource.Index?.ToString() ?? $"({i})";
                        friendlyName = i.ToString();
                    }

                    var currentExtractOption = extractOption;
                    if (resource.Compress != PsbCompressType.Tlg && resource.Compress != PsbCompressType.ByName && (resource.Width <= 0 || resource.Height <= 0)) //impossible to extract, just keep raw
                    {
                        if (currentExtractOption == PsbExtractOption.Extract)
                        {
                            currentExtractOption = PsbExtractOption.Original;
                        }
                    }

                    switch (currentExtractOption)
                    {
                        case PsbExtractOption.Extract:
                            switch (extractFormat)
                            {
                                case PsbImageFormat.png:
                                    relativePath += ".png";
                                    break;
                                default:
                                    relativePath += ".bmp";
                                    break;
                            }

                            relativePath = CheckPath(relativePath, i);
                            if (resource.Compress == PsbCompressType.RL)
                            {
                                RL.DecompressToImageFile(resource.Data, Path.Combine(dirPath, relativePath),
                                    resource.Height, resource.Width, extractFormat, resource.PixelFormat);
                            }
                            else if (resource.Compress == PsbCompressType.Tlg ||
                                     resource.Compress == PsbCompressType.ByName)
                            {
                                var bmp = context.ResourceToBitmap(resource.Compress == PsbCompressType.Tlg
                                    ? ".tlg"
                                    : Path.GetExtension(resource.Name), resource.Data);
                                if (bmp == null)
                                {
                                    if (resource.Compress == PsbCompressType.Tlg) //Fallback to managed TLG decoder
                                    {
                                        using var ms = new MemoryStream(resource.Data);
                                        using var br = new BinaryReader(ms);
                                        bmp = new TlgImageConverter().Read(br);
                                        bmp.Save(Path.Combine(dirPath, relativePath), pixelFormat);
                                        bmp.Dispose();
                                    }

                                    relativePath = Path.ChangeExtension(relativePath, Path.GetExtension(resource.Name));
                                    File.WriteAllBytes(Path.Combine(dirPath, relativePath), resource.Data);
                                }
                                else
                                {
                                    bmp.Save(Path.Combine(dirPath, relativePath), pixelFormat);
                                    bmp.Dispose();
                                }
                            }
                            //else if (resource.Compress == PsbCompressType.ByName)
                            //{
                            //    relativePath = Path.ChangeExtension(relativePath, Path.GetExtension(resource.Name));
                            //    File.WriteAllBytes(Path.Combine(dirPath, relativePath), resource.Data);
                            //}
                            else
                            {
                                RL.ConvertToImageFile(resource.Data, Path.Combine(dirPath, relativePath),
                                    resource.Height, resource.Width, extractFormat, resource.PixelFormat, resource.PalData,
                                    resource.PalettePixelFormat);
                            }

                            break;
                        case PsbExtractOption.Original:
                            if (resources[i].Compress == PsbCompressType.RL)
                            {
                                relativePath += ".rl";
                                relativePath = CheckPath(relativePath, i);
                                File.WriteAllBytes(Path.Combine(dirPath, relativePath), resource.Data);
                            }
                            else if (resource.Compress == PsbCompressType.Tlg)
                            {
                                relativePath += ".tlg";
                                relativePath = CheckPath(relativePath, i);
                                File.WriteAllBytes(Path.Combine(dirPath, relativePath), resource.Data);
                            }
                            else
                            {
                                relativePath += ".raw";
                                relativePath = CheckPath(relativePath, i);
                                File.WriteAllBytes(Path.Combine(dirPath, relativePath), resource.Data);
                            }

                            break;
                        case PsbExtractOption.Decompress:
                            relativePath += ".raw";
                            relativePath = CheckPath(relativePath, i);
                            File.WriteAllBytes(Path.Combine(dirPath, relativePath),
                                resources[i].Compress == PsbCompressType.RL
                                    ? RL.Decompress(resource.Data)
                                    : resource.Data);
                            break;
                        case PsbExtractOption.Compress:
                            relativePath += ".rl";
                            relativePath = CheckPath(relativePath, i);
                            File.WriteAllBytes(Path.Combine(dirPath, relativePath),
                                resources[i].Compress != PsbCompressType.RL
                                    ? RL.Compress(resource.Data)
                                    : resource.Data);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(currentExtractOption), currentExtractOption, null);
                    }

                    try
                    {
                        if (resource.Resource.Index != null)
                        {
                            if (resDictionary.ContainsKey(resource.Index.ToString()))
                            {
                                Console.WriteLine(
                                    "[WARN] Resource Index conflict. May be resource sharing, but may also be something wrong.");
                                continue;
                            }
                        }
                        else
                        {
                            if (resDictionary.ContainsKey(friendlyName.ToString()))
                            {
                                Console.WriteLine(
                                    "[WARN] Resource Name conflict. May be resource sharing, but may also be something wrong.");
                                continue;
                            }
                        }

                        resDictionary.Add(resource.Resource.Index == null ? friendlyName : resource.Index.ToString(),
                            $"{name}/{relativePath}");
                    }
                    catch (ArgumentException e)
                    {
                        throw new PsbBadFormatException(PsbBadFormatReason.Resources,
                            "Resource Export Error: Name conflict, or Index is not specified. Try Raw export mode.", e);
                    }
                }
            }

            string CheckPath(string rPath, int id)
            {
                var k = Path.GetFileNameWithoutExtension(rPath);
                if (resDictionary.ContainsKey(k))
                {
                    return $"{id}{Path.GetExtension(rPath)}";
                }

                return rPath;
            }

            return resDictionary;
        }

        /// <summary>
        /// Extract resource info
        /// </summary>
        /// <param name="d">PsbObject which contains "pixel"</param>
        /// <param name="r">Resource</param>
        /// <param name="duplicatePalette"></param>
        /// <returns></returns>
        internal static ImageMetadata GenerateImageMetadata(PsbDictionary d, PsbResource r = null,
            bool duplicatePalette = false)
        {
            if (r == null)
            {
                r = d.Values.FirstOrDefault(v => v is PsbResource) as PsbResource;
            }

            bool is2D = false;
            var part = GetPartName(d);
            var name = d.GetName();
            RectangleF clip = RectangleF.Empty;

            if (d["clip"] is PsbDictionary clipDic && clipDic.Count > 0)
            {
                is2D = true;
                clip = RectangleF.FromLTRB(
                    left: clipDic["left"] == null ? 0f : (float) (PsbNumber) clipDic["left"],
                    top: clipDic["top"] == null ? 0f : (float) (PsbNumber) clipDic["top"],
                    right: clipDic["right"] == null ? 1f : (float) (PsbNumber) clipDic["right"],
                    bottom: clipDic["bottom"] == null ? 1f : (float) (PsbNumber) clipDic["bottom"]
                );
            }

            var compress = PsbCompressType.None;
            if (d["compress"] is PsbString sc)
            {
                is2D = true;
                if (sc.Value.ToUpperInvariant() == "RL")
                {
                    compress = PsbCompressType.RL;
                }
            }

            int width = 1, height = 1;
            float originX = 0, originY = 0;
            if (d["width"] is PsbNumber nw)
            {
                is2D = true;
                width = (int) nw;
            }

            if (d["height"] is PsbNumber nh)
            {
                is2D = true;
                height = (int) nh;
            }

            if (d["originX"] is PsbNumber nx)
            {
                is2D = true;
                originX = (float) nx;
            }

            if (d["originY"] is PsbNumber ny)
            {
                is2D = true;
                originY = (float) ny;
            }

            PsbString typeString = null;
            if (d["type"] is PsbString typeStr)
            {
                typeString = typeStr;
            }

            int top = 0, left = 0;
            if (d["top"] is PsbNumber nt)
            {
                is2D = true;
                top = (int) nt;
            }

            if (d["left"] is PsbNumber nl)
            {
                is2D = true;
                left = (int) nl;
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
                Index = r.Index ?? int.MaxValue,
                Compress = compress,
                Name = name,
                Part = part,
                Clip = clip,
                Is2D = is2D,
                OriginX = originX,
                OriginY = originY,
                Top = top,
                Left = left,
                Width = width,
                Height = height,
                TypeString = typeString,
                Resource = r,
                Palette = palResource,
                PaletteTypeString = palTypeString
            };
            return md;
        }

        /// <summary>
        /// Get related name on depth 3 (not a common method)
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        private static string GetPartName(IPsbChild c)
        {
            while (c != null)
            {
                if (c.Parent?.Parent?.Parent == null)
                {
                    return c.GetName();
                }

                c = c.Parent;
            }

            return null;
        }
    }
}