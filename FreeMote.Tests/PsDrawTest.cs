using System;
using System.Drawing.Imaging;
using System.IO;
using FreeMote.Psb;
using FreeMote.PsDraw;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FreeMote.Tests
{
    /// <summary>
    /// PsDrawTest
    /// </summary>
    [TestClass]
    public class PsDrawTest
    {
        public PsDrawTest()
        {
        }

        private TestContext testContextInstance;
        
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }
        
        [TestMethod]
        public void TestDrawKrkr()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "澄怜a_裸-pure.psb");
            var psb = new PSB(path);
            var painter = new PsbPainter(psb);
            var bmp = painter.Draw(4096, 4096);
            bmp.Save("RenderKrkr.png", ImageFormat.Png);
        }
    }
}
