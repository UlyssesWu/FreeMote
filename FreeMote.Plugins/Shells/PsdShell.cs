using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using FreeMote.Plugins.Properties;
using FreeMote.Psb;
using PhotoshopFile;

namespace FreeMote.Plugins.Shells
{
    [Export(typeof(IPsbShell))]
    [ExportMetadata("Name", "FreeMote.Psd")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "PSD export.")]
    class PsdShell : IPsbShell
    {
        public byte[] Signature { get; } = {(byte) '8', (byte) 'B', (byte) 'P', (byte) 'S'};
        public const int MaxLayerId = 65535;
        public const string LayerIdSuffix = "#lyid#";
        private const string PsdTypeEmt = "emt";
        private const string PsdTypePimg = "pimg";

        public int Width { get; set; } = -1;
        public int Height { get; set; } = -1;

        public bool TryGetCanvasSize { get; set; } = false;
        public string Name => "PSD";

        public bool IsInShell(Stream stream, Dictionary<string, object> context = null)
        {
            var header = new byte[4];
            var pos = stream.Position;
            stream.Read(header, 0, 4);
            stream.Position = pos;
            if (header.SequenceEqual(Signature))
            {
                if (context != null)
                {
                    context[Consts.Context_PsbShellType] = Name;
                }

                return true;
            }

            return false;
        }

        public MemoryStream ToPsb(Stream stream, Dictionary<string, object> context = null)
        {
            try
            {
                PsdFile psd = new PsdFile(stream, new LoadContext {Encoding = Encoding.UTF8});
                var xmp = psd.ImageResources.FirstOrDefault(info => info is XmpResource);
                if (xmp is XmpResource xmpRes)
                {
                    var type = xmpRes.Name.ToLowerInvariant();
                    if (type != PsdTypePimg)
                    {
                        Logger.LogWarn("Only pimg PSD can be converted to PSB.");
                        //return null;
                    }
                }

                var layers = new PsbList();
                PSB psb = new PSB(3)
                {
                    Objects = new PsbDictionary
                    {
                        ["width"] = psd.Width.ToPsbNumber(),
                        ["height"] = psd.Height.ToPsbNumber(),
                        ["layers"] = layers
                    }
                }; //TODO: set version

                psd.Layers.Reverse();
                Stack<GroupLayer> groupLayers = new Stack<GroupLayer>();
                List<(GroupLayer Group, Layer Layer)> groupLayerList = new();
                List<ImageMetadata> imageMetadatas = new List<ImageMetadata>();
                Dictionary<Layer, int> layerIdMap = new Dictionary<Layer, int>(psd.Layers.Count);
                Dictionary<Layer, LayerSectionType> layerSectionTypes = new(psd.Layers.Count);
                HashSet<int> idRegistry = new HashSet<int>(psd.Layers.Count);
                int newId = 1;
                foreach (var layer in psd.Layers)
                {
                    var sectionInfo = layer.AdditionalInfo.FirstOrDefault(info => info is LayerSectionInfo);
                    if (sectionInfo is LayerSectionInfo section)
                    {
                        layerSectionTypes[layer] = section.SectionType;
                        if (section.SectionType == LayerSectionType.SectionDivider)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        layerSectionTypes[layer] = LayerSectionType.Layer;
                    }

                    if (layer.Name.Contains(LayerIdSuffix))
                    {
                        try
                        {
                            var lines = layer.Name.Split(new[] {LayerIdSuffix}, StringSplitOptions.None);
                            var idStr = lines[1];
                            var id = int.Parse(idStr);
                            layerIdMap[layer] = id;
                            layer.Name = lines[0];
                        }
                        catch
                        {
                            layerIdMap[layer] = 0;
                        }
                    }
                    else
                    {
                        var idLayer = layer.AdditionalInfo.FirstOrDefault(info => info is LayerId);
                        if (idLayer is LayerId layerId)
                        {
                            var id = (int) layerId.Id;
                            if (!idRegistry.Contains(id))
                            {
                                layerIdMap[layer] = id;
                                idRegistry.Add(id);
                            }
                            else if (id == -1)
                            {
                                layerIdMap[layer] = id;
                            }
                            else
                            {
                                layerIdMap[layer] = 0;
                            }
                        }
                        else
                        {
                            layerIdMap[layer] = 0;
                        }
                    }
                }

                foreach (var l in layerIdMap.Keys)
                {
                    var id = layerIdMap[l];
                    if (id == 0)
                    {
                        while (idRegistry.Contains(newId))
                        {
                            newId++;
                        }

                        layerIdMap[l] = newId;
                        idRegistry.Add(newId);
                    }
                }

                foreach (var layer in psd.Layers)
                {
                    var sectionType = layerSectionTypes[layer];
                    var id = sectionType == LayerSectionType.SectionDivider ? 0 : layerIdMap[layer];

                    if (sectionType is LayerSectionType.OpenFolder or LayerSectionType.ClosedFolder)
                    {
                        var group = new GroupLayer
                        {
                            LayerId = id,
                            Name = layer.Name,
                            Open = sectionType == LayerSectionType.OpenFolder,
                            Parent = groupLayers.Count > 0 ? groupLayers.Peek() : null
                        };
                        groupLayers.Push(group);
                        groupLayerList.Add((group, layer));
                        continue;
                    }

                    if (sectionType == LayerSectionType.SectionDivider)
                    {
                        if (groupLayers.Count > 0)
                        {
                            groupLayers.Pop();
                        }

                        continue;
                    }
                    
                    var rect = layer.Rect;
                    var left = rect.X;
                    var top = rect.Y;
                    var width = rect.Width;
                    var height = rect.Height;
                    int? currentGroupId = groupLayers.Count > 0 ? groupLayers.Peek().LayerId : null;
                    var obj = new PsbDictionary
                    {
                        ["layer_id"] = id.ToPsbNumber(),
                        ["layer_type"] = PsbNumber.Zero,
                        ["name"] = layer.Name.ToPsbString(),
                        ["left"] = left.ToPsbNumber(),
                        ["top"] = top.ToPsbNumber(),
                        ["width"] = width.ToPsbNumber(),
                        ["height"] = height.ToPsbNumber(),
                        ["type"] = 13.ToPsbNumber(),
                        ["visible"] = layer.Visible ? 1.ToPsbNumber() : PsbNumber.Zero,
                        ["opacity"] = new PsbNumber(layer.Opacity),
                    };
                    if (currentGroupId != null)
                    {
                        obj["group_layer_id"] = currentGroupId.Value.ToPsbNumber();
                    }

                    var bitmap = layer.GetBitmap();
                    ImageMetadata imageMetadata = null;
                    if (bitmap != null)
                    {
                        bool useTlg = true;
                        imageMetadata = new ImageMetadata() { LayerType = id, Resource = new PsbResource(), Compress = PsbCompressType.Tlg };
                        imageMetadata.SetData(bitmap);
                        if (imageMetadata.Data == null)
                        {
                            useTlg = false;
                            Logger.LogWarn(
                                $"Cannot convert bitmap to TLG, maybe FreeMote.Plugins.x64 is missing, or you're not working on Windows.");
                            using var pngMs = new MemoryStream();
                            bitmap.Save(pngMs, ImageFormat.Png);
                            imageMetadata.Data = pngMs.ToArray();
                        }

                        int sameImageId = -1;
                        if (imageMetadata.Data != null)
                        {
                            var same = imageMetadatas.FirstOrDefault(r => r.Data.SequenceEqual(imageMetadata.Data));
                            if (same != null)
                            {
                                sameImageId = same.LayerType;
                            }
                        }

                        if (sameImageId < 0)
                        {
                            imageMetadatas.Add(imageMetadata);
                        }

                        if (sameImageId < 0)
                        {
                            if (id >= 0)
                            {
                                //Set resource here
                                psb.Objects[$"{id}.{(useTlg ? "tlg" : "png")}"] = imageMetadata.Resource;
                            }
                        }
                        else
                        {
                            obj["same_image"] = sameImageId.ToPsbNumber();
                        }
                    }

                    layers.Add(obj);
                }

                foreach (var g in groupLayerList)
                {
                    var obj = new PsbDictionary
                    {
                        ["layer_id"] = g.Group.LayerId.ToPsbNumber(),
                        ["layer_type"] = 2.ToPsbNumber(),
                        ["type"] = 13.ToPsbNumber(),
                        ["width"] = g.Layer.Width.ToPsbNumber(),
                        ["height"] = g.Layer.Height.ToPsbNumber(),
                        ["left"] = g.Layer.Rect.X.ToPsbNumber(),
                        ["top"] = g.Layer.Rect.Y.ToPsbNumber(),
                        ["name"] = g.Layer.Name.ToPsbString(),
                        ["visible"] = g.Layer.Visible ? 1.ToPsbNumber() : PsbNumber.Zero,
                        ["opacity"] = new PsbNumber(g.Layer.Opacity),
                    };
                    if (g.Group.Parent != null)
                    {
                        obj["group_layer_id"] = g.Group.Parent.LayerId.ToPsbNumber();
                    }

                    layers.Add(obj);
                }

                psb.Merge();
                return psb.ToStream();
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }

            return null;
        }

        public MemoryStream ToShell(Stream stream, Dictionary<string, object> context = null)
        {
            Logger.LogWarn(
                "[WARN] Exported PSD files should follow CC-BY-NC-SA 4.0. Please keep FreeMote information in PSD files.");
            var psb = new PSB(stream);
            if (psb == null)
            {
                throw new BadImageFormatException("Not a valid PSB file.");
            }

            if (psb.Type == PsbType.Pimg)
            {
                return ConvertPimgToPsd(psb);
            }

            Logger.LogWarn(
                "[WARN] EMT PSB to PSD Conversion is not really implemented. No further plan. No support. \r\nYou're welcomed to contribute or submit samples, but issues won't be fixed.");
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
            EmtPainter painter = new EmtPainter(psb);

            if (TryGetCanvasSize && painter.Resources.Count > 0)
            {
                if (psb.TryGetCanvasSize(out var cw, out var ch))
                {
                    Width = (int) (cw * 1.8f);
                    Height = (int) (ch * 1.4f);
                }
                else
                {
                    //Try get from painter, not accurate if the PSB center is not (0,0)
                    Width = (int) (painter.Resources.Max(r => r.OriginX + r.Width / 2.0f) -
                                   painter.Resources.Min(r => r.OriginX - r.Width / 2.0f));
                    Height = (int) (painter.Resources.Max(r => r.OriginY + r.Height / 2.0f) -
                                    painter.Resources.Min(r => r.OriginY - r.Height / 2.0f));

                    Width = (int) (Width * 1.4f);
                    Height = (int) (Height * 1.4f);
                }

                if (context != null)
                {
                    if (context.ContainsKey("Width") && context["Width"] is int width)
                    {
                        Width = width;
                    }

                    if (context.ContainsKey("Height") && context["Height"] is int height)
                    {
                        Height = height;
                    }
                }
            }

            var psd = ConvertEmtToPsd(painter, Width, Height);
            var ms = new MemoryStream();
            psd.Save(ms, Encoding.UTF8);
            return ms;
        }

        private MemoryStream ConvertPimgToPsd(PSB psb)
        {
            var width = psb.Objects["width"].GetInt();
            var height = psb.Objects["height"].GetInt();

            PsdFile psd = new PsdFile
            {
                Width = width,
                Height = height,
                Resolution = new ResolutionInfo
                {
                    HeightDisplayUnit = ResolutionInfo.Unit.Points,
                    WidthDisplayUnit = ResolutionInfo.Unit.Points,
                    HResDisplayUnit = ResolutionInfo.ResUnit.PxPerInch,
                    VResDisplayUnit = ResolutionInfo.ResUnit.PxPerInch,
                    HDpi = new UFixed16_16(72, 0),
                    VDpi = new UFixed16_16(72, 0),
                },
                ImageCompression = ImageCompression.Rle
            };

            psd.ImageResources.Add(new XmpResource(PsdTypePimg) {XmpMetaString = Resources.Xmp});
            psd.BaseLayer.SetBitmap(new Bitmap(width, height, PixelFormat.Format32bppArgb),
                ImageReplaceOption.KeepCenter, psd.ImageCompression);

            var images = psb.CollectResources<ImageMetadata>();

            //layer type: 0 = image, 2 = folder
            List<ILayer> layers = new List<ILayer>();
            List<GroupLayer> groupLayers = new List<GroupLayer>();
            var layerObjects = (PsbList) psb.Objects["layers"];

            GroupLayer CreateGroupLayer(PsbDictionary lObj, ILayer child = null)
            {
                var layerId = lObj["layer_id"].GetInt();
                var existedGroup = groupLayers.FirstOrDefault(l => l.LayerId == layerId);
                if (existedGroup != null)
                {
                    if (child != null)
                    {
                        existedGroup.Children.Add(child);
                        child.Parent = existedGroup;
                    }

                    return null;
                }

                var groupLayer = new GroupLayer {Object = lObj, LayerId = layerId, Name = lObj["name"].ToString()};
                if (child != null)
                {
                    groupLayer.Children.Add(child);
                    child.Parent = groupLayer;
                }

                groupLayers.Add(groupLayer);
                if (lObj["group_layer_id"] is PsbNumber groupLayerId)
                {
                    var parent1 = groupLayers.FirstOrDefault(g => g.LayerId == groupLayerId.IntValue);
                    if (parent1 != null)
                    {
                        parent1.Children.Add(groupLayer);
                        groupLayer.Parent = parent1;
                        return null;
                    }

                    var parent = layerObjects.FirstOrDefault(l =>
                        l is PsbDictionary lo && lo["layer_type"].GetInt() == 2 && lo["layer_id"].GetInt() == groupLayerId.GetInt());
                    if (parent is PsbDictionary parentObj)
                    {
                        return CreateGroupLayer(parentObj, groupLayer);
                    }
                }

                return groupLayer;
            }

            foreach (var layer in layerObjects)
            {
                if (layer is not PsbDictionary layerObj)
                {
                    continue;
                }

                if (layerObj["layer_type"].GetInt() == 2) // layer group
                {
                    var g = CreateGroupLayer(layerObj);
                    if (g != null)
                    {
                        layers.Add(g);
                    }
                }
                else
                {
                    var layerId = layerObj["layer_id"].GetInt();
                    var imageLayer = new ImageLayer
                    {
                        Object = layerObj, Name = layerObj["name"].ToString(), LayerId = layerId,
                        ImageMetadata = images.FirstOrDefault(md => md.Index == (uint) layerId)
                    };
                    if (imageLayer.ImageMetadata == null && layerObj.TryGetValue("same_image", out var sameImageId))
                    {
                        var sameImage = sameImageId.GetInt();
                        imageLayer.ImageMetadata = images.FirstOrDefault(md => md.Index == (uint) sameImage);
                    }

                    if (layerObj["group_layer_id"] is PsbNumber groupLayerId)
                    {
                        var parent = layerObjects.FirstOrDefault(l =>
                            l is PsbDictionary lo && lo["layer_type"].GetInt() == 2 && lo["layer_id"].GetInt() == groupLayerId.GetInt());
                        if (parent is PsbDictionary parentObj)
                        {
                            var g = CreateGroupLayer(parentObj, imageLayer);
                            if (g != null)
                            {
                                layers.Add(g);
                            }
                        }
                    }
                    else
                    {
                        layers.Add(imageLayer);
                    }
                }
            }

            foreach (var layer in layers)
            {
                layer.CreateLayers(psd);
            }

            psd.Layers.Reverse();
            var ms = new MemoryStream();
            psd.Save(ms, Encoding.UTF8);
            return ms;
        }

        private PsdFile ConvertEmtToPsd(EmtPainter painter, int width, int height)
        {
            float offsetX = 0;
            float offsetY = 0;
            if (width <= 0 && height <= 0)
            {
                (width, height, offsetX, offsetY) = painter.TryGetCanvasSize();
                if (width <= 0 && height <= 0)
                {
                    width = 1500;
                    height = 2000;
                }
                else
                {
                    width = (int) (width * 1.05f);
                    height = (int) (height * 1.1f);
                    offsetX *= 1.025f;
                    offsetY *= 1.05f;

                    Width = width;
                    Height = height;
                }
            }

            PsdFile psd = new PsdFile
            {
                Width = width,
                Height = height,
                Resolution = new ResolutionInfo
                {
                    HeightDisplayUnit = ResolutionInfo.Unit.Points,
                    WidthDisplayUnit = ResolutionInfo.Unit.Points,
                    HResDisplayUnit = ResolutionInfo.ResUnit.PxPerInch,
                    VResDisplayUnit = ResolutionInfo.ResUnit.PxPerInch,
                    HDpi = new UFixed16_16(72, 0),
                    VDpi = new UFixed16_16(72, 0),
                },
                ImageCompression = ImageCompression.Rle
            };

            psd.ImageResources.Add(new XmpResource(PsdTypeEmt) {XmpMetaString = Resources.Xmp});
            psd.BaseLayer.SetBitmap(new Bitmap(width, height, PixelFormat.Format32bppArgb),
                ImageReplaceOption.KeepCenter, psd.ImageCompression);

            string currentGroup = "";
            Layer beginSection = null;
            foreach (var resMd in painter.Resources)
            {
                if (resMd.Label.StartsWith(painter.GroupMark))
                {
                    resMd.Label = resMd.Label.Substring(1);
                }

                string name = $"{resMd.Label}-{resMd.Name}";

                var layer = psd.MakeImageLayer(resMd.ToImage(), name, (int) (resMd.OriginX + width / 2f - resMd.Width / 2f + offsetX),
                    (int) (resMd.OriginY + height / 2f - resMd.Height / 2f + offsetY));
                layer.Visible = resMd.Visible;
                if (resMd.Opacity <= 0)
                {
                    layer.Opacity = 0;
                }
                else
                {
                    layer.Opacity = (byte) (resMd.Opacity / 10.0f * 255);
                }

                if (resMd.MotionName != currentGroup)
                {
                    currentGroup = resMd.MotionName;
                    if (beginSection != null)
                    {
                        psd.Layers.Add(beginSection);
                        beginSection = null;
                    }

                    if (!string.IsNullOrEmpty(currentGroup))
                    {
                        beginSection = psd.MakeSectionLayers(currentGroup, out var endLayer, false);
                        psd.Layers.Add(endLayer);
                    }
                }

                psd.Layers.Add(layer);
            }

            if (beginSection != null)
            {
                psd.Layers.Add(beginSection);
            }

            psd.Layers.Add(psd.MakeImageLayer(
                GenerateMarkText("by FreeMote, wdwxy12345@gmail.com ", width, 200), "FreeMote", 0, 0));
            return psd;
        }

        private Bitmap GenerateMarkText(string text, int width, int height, string fontName = "Arial", int fontSize = 20)
        {
            Font drawFont = new Font(fontName, fontSize, FontStyle.Bold);
            var size = PsbResHelper.MeasureString(text, drawFont);
            Bitmap bmp = new Bitmap((int) Math.Ceiling(size.Width + 1), (int) Math.Ceiling(size.Height + 1), PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmp);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            StringFormat sf = StringFormat.GenericTypographic;
            RectangleF rectBounds = new Rectangle(0, 0, (int) size.Width, (int) size.Height);
            g.FillRectangle(Brushes.Transparent, rectBounds);
            g.DrawString(text, drawFont, Brushes.Black, 1f, 1f, sf);
            g.Dispose();
            return bmp;
        }
    }

    interface ILayer
    {
        public GroupLayer Parent { get; set; }
        public int LayerId { get; }
        PsbDictionary Object { get; }
        public string Name { get; set; }

        void CreateLayers(PsdFile psd);
    }

    [DebuggerDisplay("{Name,nq} ({LayerId})")]
    class GroupLayer : ILayer
    {
        public bool Open { get; set; } = false;
        public GroupLayer Parent { get; set; }
        public int LayerId { get; set; }
        public PsbDictionary Object { get; set; }
        public List<ILayer> Children { get; private set; } = new();
        public string Name { get; set; }

        public void CreateLayers(PsdFile psd)
        {
            var beginLayer = psd.MakeSectionLayers(Name, out var endLayer, Open);
            beginLayer.Visible = Object.TryGetValue("visible", out var visible) && visible.GetInt() != 0;
            beginLayer.Opacity = (byte) (Object.TryGetValue("opacity", out var opacity) ? opacity.GetInt() : 255);
            if (LayerId is >= 0 and <= PsdShell.MaxLayerId)
            {
                var idLayer = new LayerId((uint) LayerId);
                beginLayer.AdditionalInfo.Add(idLayer);
            }

            psd.Layers.Add(beginLayer);
            foreach (var child in Children)
            {
                child.CreateLayers(psd);
            }

            psd.Layers.Add(endLayer);
        }
    }

    [DebuggerDisplay("{Name,nq} ({LayerId})")]
    class ImageLayer : ILayer
    {
        public GroupLayer Parent { get; set; }
        public int LayerId { get; set; }
        public PsbDictionary Object { get; set; }
        public string Name { get; set; }
        public ImageMetadata ImageMetadata { get; set; }

        public void CreateLayers(PsdFile psd)
        {
            var md = ImageMetadata;
            if (md == null)
            {
                var layerWidth = Object["width"].GetInt();
                var layerHeight = Object["height"].GetInt();
                var layerTop = Object["top"].GetInt();
                var layerLeft = Object["left"].GetInt();
                var emptyLayer = psd.MakeImageLayer(new Bitmap(layerWidth, layerHeight), Name, layerLeft, layerTop);
                emptyLayer.Visible = Object["visible"].GetInt() != 0;
                emptyLayer.Opacity = (byte) Object["opacity"].GetInt();
                if (LayerId is >= 0 and <= PsdShell.MaxLayerId)
                {
                    var idLayer = new LayerId((uint) LayerId);
                    emptyLayer.AdditionalInfo.Add(idLayer);
                }
                else if (LayerId == -1)
                {
                    emptyLayer.Name += $"{PsdShell.LayerIdSuffix}{LayerId}";
                }

                psd.Layers.Add(emptyLayer);
            }
            else
            {
                var layerTop = Object["top"].GetInt();
                var layerLeft = Object["left"].GetInt();
                var imageLayer = psd.MakeImageLayer(md.ToImage(), Name, layerLeft, layerTop);
                imageLayer.Visible = Object["visible"].GetInt() != 0;
                imageLayer.Opacity = (byte) Object["opacity"].GetInt();
                if (LayerId is >= 0 and <= PsdShell.MaxLayerId)
                {
                    var idLayer = new LayerId((uint) LayerId);
                    imageLayer.AdditionalInfo.Add(idLayer);
                }
                else if (LayerId == -1)
                {
                    imageLayer.Name += $"{PsdShell.LayerIdSuffix}{LayerId}";
                }

                psd.Layers.Add(imageLayer);
            }
        }
    }
}