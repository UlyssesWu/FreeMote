using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using FreeMote.Psb.Types;
using FastBitmapLib;

namespace FreeMote.Psb
{
    public class SprPainter
    {
        public PSB SprData { get; set; }
        public string BasePath { get; set; }
        public bool JsonMode { get; set; } = true;

        public int TileWidth { get; set; } = 32;
        public int TileHeight { get; set; } = 32;

        public int IdWidth { get; set; } = 3;

        private readonly Dictionary<int, Bitmap> _textures = new Dictionary<int, Bitmap>();

        //palette: each single line is a palette

        public SprPainter(PSB sprData, string basePath)
        {
            SprData = sprData;
            BasePath = basePath;
        }

        private void Collect()
        {
            if (JsonMode)
            {
                //read images from BasePath\__ddd.psb.m\*.png
                var dirs = Directory.GetDirectories(BasePath, "*.psb.m", SearchOption.TopDirectoryOnly);
                foreach (var dir in dirs)
                {
                    var png = Path.Combine(dir, "0.png");
                    if (File.Exists(png))
                    {
                        var dirName = Path.GetFileName(dir);
                        try
                        {
                            if(int.TryParse(dirName.Substring(dirName.IndexOf('.') - IdWidth, IdWidth), out int number))
                            {
                                _textures.Add(number, new Bitmap(png));
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarn($"Error processing {dirName}: {ex.Message}");
                        }
                    }
                }
            }
            else
            {
                Logger.LogError("JsonMode is the only supported mode for now.");
                // var files = Directory.GetFiles(BasePath, "*.psb*", SearchOption.AllDirectories);
                // foreach (var file in files)
                // {
                //     var fileName = Path.GetFileName(file);
                //     var extension = Path.GetExtension(file).ToLowerInvariant();

                //     if(extension == ".m" || extension == ".psb")
                //     {
                //         try{
                //             if(int.TryParse(fileName.Substring(2, fileName.IndexOf('.') - 2), out int number))
                //             {
                //                 PSB psb = new PSB(file);
                //                 //TODO:
                //             }
                //         }
                //         catch (Exception ex)
                //         {
                //             Logger.LogWarn($"Error processing {fileName}: {ex.Message}");
                //         }
                //     }

                // }
            }
        }

        public Dictionary<string, Bitmap> Draw()
        {
            var result = new Dictionary<string, Bitmap>();
            if (!JsonMode)
            {
                Logger.LogError("JsonMode is the only supported mode for now.");
                return result;
            }

            Collect();

            var sprDataList = SprData.Objects["spr_data"] as PsbList;
            for (int i = 0; i < sprDataList.Count; i++)
            {
                var sprImage = sprDataList[i] as PsbDictionary;
                var org = sprImage.Children("org") as PsbList;
                var width = org[0].GetInt();
                var height = org[1].GetInt();
                var bitmap = new Bitmap(width, height);
                var tiles = new List<SprTile>();
                PsbList data = sprImage.Children("data") as PsbList;
                foreach (var tileData in data)
                {
                    var tileDataList = tileData as PsbList;
                    var tile = new SprTile
                    {
                        TexId = (ushort) tileDataList[0].GetInt(),
                        Id = (ushort) tileDataList[1].GetInt(),
                        X = tileDataList[2].GetInt(),
                        Y = tileDataList[3].GetInt()
                    };
                    tiles.Add(tile);
                }

                using (var f = bitmap.FastLock())
                {
                    foreach (var tile in tiles)
                    {
                        var texId = tile.TexId;
                        if (!_textures.ContainsKey(texId))
                        {
                            Logger.LogError($"Tex missing: {texId} required by: {tile}");
                            continue;
                        }
                        var tex = _textures[texId];

                        var texWidth = tex.Width;
                        var texHeight = tex.Height; //useless, the ID is only related to width
                        var tilePerLine = texWidth / TileWidth;

                        // 0 1 2 3 ... 31
                        // ...
                        // 31*32 31*32+1 31*32+2 ... 31*32+31
                        //calculate source rect from tile.Id
                        var sourceRect = new Rectangle(
                            (tile.Id % tilePerLine) * TileWidth,
                            (tile.Id / tilePerLine) * TileHeight,
                            TileWidth,
                            TileHeight
                        );
                        var tileX = tile.X;
                        var tileY = tile.Y;

                        f.CopyRegion(tex, sourceRect, new Rectangle(tileX * TileWidth, tileY * TileHeight, TileWidth, TileHeight));
                    }

                    result.Add(i.ToString(), bitmap);
                }
            }

            return result;
        }
    }
}
