using FreeMote.Psb.Textures;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace FreeMote.Psb
{
    /// <summary>
    /// Motion PSB Painter
    /// </summary>
    public class MtnPainter
    {
        public PSB Source { get; set; }

        private readonly StaticMotionPainterCore _renderer;

        public string BaseMotion { get; private set; }

        public MtnPainter(PSB psb)
        {
            Source = psb;
            var resources = Source.Platform == PsbSpec.krkr ? Source.CollectResources<ImageMetadata>().ToList() : Source.CollectSplitResources();
            _renderer = new StaticMotionPainterCore(Source, resources);
            var motionName = (Source.Objects["object"] as PsbDictionary)?.Keys.FirstOrDefault();

            if (string.IsNullOrEmpty(motionName))
            {
                throw new Exception("cannot find base motion object");
            }

            BaseMotion = motionName;
        }

        /// <summary>
        /// Draw all sub-motions with auto size
        /// </summary>
        /// <returns></returns>
        public IEnumerable<(string Name, Bitmap Image)> DrawAll()
        {
            var subMotions = GetSubMotionNames();
            foreach (var subMotion in subMotions)
            {
                var img = Draw(subMotion);
                if (img == null)
                {
                    continue;
                }
                yield return (subMotion, img);
            }
        }

        /// <summary>
        /// Draw a sub-motion, set both <paramref name="width"/> and <paramref name="height"/> to 0 to use auto size.
        /// A representative frame is selected automatically to avoid rendering empty startup frames.
        /// </summary>
        /// <param name="subMotion"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public Bitmap Draw(string subMotion, int width = 0, int height = 0)
        {
            return _renderer.DrawMotion(BaseMotion, subMotion, width, height);
        }

        /// <summary>
        /// Draw a sub-motion at a specific timeline time.
        /// </summary>
        /// <param name="subMotion"></param>
        /// <param name="time"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public Bitmap DrawAt(string subMotion, float time, int width = 0, int height = 0)
        {
            return _renderer.DrawMotionAt(BaseMotion, subMotion, time, width, height);
        }

        public List<string> GetSubMotionNames()
        {
            return _renderer.GetMotionNames(BaseMotion);
        }
    }
}
