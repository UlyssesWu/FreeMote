using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using FreeMote.Psb.Textures;

namespace FreeMote.Psb
{
    /// <summary>
    /// EMT PSB Painter
    /// </summary>
    public class EmtPainter
    {
        public string GroupMark { get; set; } = "■";
        public PSB Source { get; set; }
        public List<ImageMetadata> Resources { get; private set; } = new List<ImageMetadata>();

        private StaticMotionPainterCore _renderer;
        private List<StaticDrawableResource> _drawResources = new List<StaticDrawableResource>();

        public EmtPainter(PSB psb)
        {
            Source = psb;
            UpdateResource();
        }

        /// <summary>
        /// Gather resources for painting
        /// </summary>
        public void UpdateResource()
        {
            if (Source.InferType() != PsbType.Motion)
            {
                throw new FormatException("EmtPainter only works for Motion PSB models.");
            }

            var resources = Source.Platform == PsbSpec.krkr ? Source.CollectResources<ImageMetadata>().ToList() : Source.CollectSplitResources();
            _renderer = new StaticMotionPainterCore(Source, resources);
            var basePart = GetBasePart();
            var baseMotion = GetBaseMotion();
            _drawResources = string.IsNullOrEmpty(baseMotion)
                ? new List<StaticDrawableResource>()
                : _renderer.CollectResourcesAtDefaultTime(basePart, baseMotion);

            if (_drawResources.Count == 0)
            {
                _drawResources = _renderer.CollectAllMotionsAtDefaultTime(basePart);
            }

            Resources = _drawResources.Select(res => res.ToImageMetadata()).OrderBy(res => res.ZIndex).ToList();
        }

        public (int Width, int Height, float OffsetX, float OffsetY) TryGetCanvasSize()
        {
            return _renderer.GetCanvasSize(_drawResources);
        }

        /// <summary>
        /// Render the model to an image
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public Bitmap Draw(int width, int height)
        {
            var bmp = _renderer.DrawResources(_drawResources, width, height);
            if (bmp == null)
            {
                Logger.LogError("Nothing is visible to draw!");
            }

            return bmp;
        }

        private string GetBasePart()
        {
            var basePart = "all_parts";
            try
            {
                if (Source.Objects["metadata"] is PsbNull)
                {
                    var basePart2 = (Source.Objects["object"] as PsbDictionary)?.Keys.FirstOrDefault();
                    if (!string.IsNullOrEmpty(basePart2))
                    {
                        basePart = basePart2;
                    }
                }
                else if (Source.Objects["metadata"].Children("base").Children("chara") is PsbString chara && !string.IsNullOrEmpty(chara.Value))
                {
                    basePart = chara;
                }
            }
            catch
            {
                //ignore
            }

            return basePart;
        }

        private string GetBaseMotion()
        {
            try
            {
                if (Source.Objects["metadata"].Children("base").Children("motion") is PsbString motion && !string.IsNullOrEmpty(motion.Value))
                {
                    return motion;
                }
            }
            catch
            {
                //ignore
            }

            return null;
        }
    }
}
