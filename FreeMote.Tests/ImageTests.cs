using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace FreeMote.Tests
{
    [TestClass]
    public class ImageTests
    {
        [TestMethod]
        public void Dxt1SwFormatNameRoundTrips()
        {
            Assert.AreEqual(PsbPixelFormat.DXT1_SW, "DXT1_SW".ToPsbPixelFormat(PsbSpec.ps4));
            Assert.AreEqual("DXT1_SW", PsbPixelFormat.DXT1_SW.ToStringForPsb());
        }

        [TestMethod]
        public void Dxt1SwUsesPs4BlockTiling()
        {
            const int width = 32;
            const int height = 32;

            using (var source = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(source))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.FillRectangle(Brushes.Red, 0, 0, 16, 16);
                    graphics.FillRectangle(Brushes.Lime, 16, 0, 16, 16);
                    graphics.FillRectangle(Brushes.Blue, 0, 16, 16, 16);
                    graphics.FillRectangle(Brushes.White, 16, 16, 16, 8);
                }

                var linearDxt1 = DxtUtil.Dxt1Encode(source);
                var expected = PostProcessing.TileTextureV2(linearDxt1, width / 4, height / 4, 8);
                var actual = RL.GetPixelBytesFromImage(source, PsbPixelFormat.DXT1_SW);

                CollectionAssert.AreEqual(expected, actual);

                using (var decoded = RL.ConvertToImage(actual, width, height, PsbPixelFormat.DXT1_SW))
                {
                    AssertColorClose(Color.Red, decoded.GetPixel(8, 8));
                    AssertColorClose(Color.Lime, decoded.GetPixel(24, 8));
                    AssertColorClose(Color.Blue, decoded.GetPixel(8, 24));
                    AssertColorClose(Color.White, decoded.GetPixel(24, 20));
                    Assert.AreEqual(0, decoded.GetPixel(24, 28).A);
                }
            }
        }

        private static void AssertColorClose(Color expected, Color actual)
        {
            const int tolerance = 8;
            Assert.IsTrue(Math.Abs(expected.R - actual.R) <= tolerance &&
                          Math.Abs(expected.G - actual.G) <= tolerance &&
                          Math.Abs(expected.B - actual.B) <= tolerance,
                $"Expected {expected}, but got {actual}.");
        }
    }
}
