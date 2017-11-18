using System;
using System.Collections.Generic;
using System.Drawing;
using FreeMote.Psb;

namespace FreeMote.PsBuild.Converters
{
    class Krkr2CommonConverter : ISpecConverter
    {
        public Krkr2CommonConverter(bool isWin = false)
        {
            IsWin = isWin;
        }

        public SpecConvertOption ConvertOption { get; set; } = SpecConvertOption.Default;

        public PsbPixelFormat TargetPixelFormat { get; set; } = PsbPixelFormat.WinRGBA8;
        public bool UseRL { get; set; } = false;
        public IList<PsbSpec> FromSpec { get; } = new List<PsbSpec>{PsbSpec.win, PsbSpec.krkr};
        public IList<PsbSpec> ToSpec { get; } = new List<PsbSpec> {PsbSpec.krkr};
        public bool IsWin { get; set; }

        public int? SideLength { get; set; } = null;


        public void Convert(PSB psb)
        {
            if (!FromSpec.Contains(psb.Platform))
            {
                throw new FormatException("Can not convert Spec for this PSB");
            }

            psb.Platform = IsWin ? PsbSpec.win : PsbSpec.common;
        }

        private void Remove(PSB psb)
        {
            
        }

        private void Add(PSB psb)
        {
            
        }

        private void TranslateResource(PSB psb)
        {
            Dictionary<string, Bitmap> iconInfos = new Dictionary<string, Bitmap>();
            var source = (PsbDictionary)psb.Objects["source"];
            foreach (var tex in source)
            {
                var texName = tex.Key;
                var icons = (PsbDictionary)((PsbDictionary) tex.Value)["icon"];
                foreach (var icon in icons)
                {
                    var iconName = icon.Key;
                    var info = (PsbDictionary) icon.Value;
                    var width = (int)(PsbNumber)info["width"];
                    var height = (int)(PsbNumber)info["height"];
                    var res = (PsbResource) info["pixel"];
                    var bmp = info["compress"]?.ToString().ToUpperInvariant() == "RL"
                        ? RL.UncompressToImage(res.Data, height, width, psb.Platform.DefaultPixelFormat())
                        : RL.ConvertToImage(res.Data, height, width, psb.Platform.DefaultPixelFormat());
                    bmp.Tag = iconName;
                }
            }
        }

        private void Travel(IPsbCollection collection)
        {
            
        }
    }
}
