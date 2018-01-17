using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FreeMote.Tests
{
    [TestClass]
    public class FreeMoteTest
    {
        public FreeMoteTest()
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

        #region 附加测试特性
        //
        // 编写测试时，可以使用以下附加特性: 
        //
        // 在运行类中的第一个测试之前使用 ClassInitialize 运行代码
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // 在类中的所有测试都已运行之后使用 ClassCleanup 运行代码
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // 在运行每个测试之前，使用 TestInitialize 来运行代码
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // 在每个测试运行完之后，使用 TestCleanup 来运行代码
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void TestEncode()
        {
            //Debug.WriteLine(Environment.CurrentDirectory);
            uint targetKey = 504890837; //give your model key
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            foreach (var file in Directory.EnumerateFiles(resPath))
            {
                if (!file.ToLowerInvariant().EndsWith(".psb"))
                {
                    continue;
                }
                var fileName = Path.GetFileNameWithoutExtension(file).Split(new[] { '-' }, 2); //rename your file as key-name.psb
                if (fileName.Length < 2)
                {
                    continue;
                }
                var key = UInt32.Parse(fileName[0]);
                if (key != targetKey)
                {
                    continue;
                }
                PsbFile psb = new PsbFile(file);
                psb.EncodeToFile(targetKey, file + ".pure", EncodeMode.Encrypt, EncodePosition.Auto);
            }
        }

        [TestMethod]
        public void TestChecksum()
        {
            byte[] arr1 =
                {0x2C, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0xBD, 0x8C, 0x08, 0x00, 0x19, 0x31, 0x08, 0x00,
                0xD1, 0xAF, 0x08, 0x00, 0x69, 0x0F, 0x08, 0x00, 0xF4, 0xAF, 0x08, 0x00, 0x66, 0x94, 0x00, 0x05
                };
            byte[] arr2 = { 0x1F, 0x0F, 0x08, 0x00, 0xD6, 0xAF, 0x08, 0x00, 0xC0, 0x82, 0x20, 0x00 };
            Adler32 adler32 = new Adler32();
            adler32.Update(arr1);
            adler32.Update(arr2);
            Debug.WriteLine(adler32.Checksum.ToString("X8"));
            Assert.AreEqual(0xC02709D3, adler32.Checksum);

            arr1 = new byte[]
               {0x2C, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0xBD, 0x8C, 0x08, 0x00, 0x19, 0x31, 0x08, 0x00,
                0xD1, 0xAF, 0x08, 0x00, 0x69, 0x0F, 0x08, 0x00, 0xF4, 0xAF, 0x08, 0x00, 0x66, 0x94, 0x00, 0x00
                };
            arr2 = new byte[]
            { 0xAB, 0x0F, 0x08, 0x00, 0xD6, 0xAF, 0x08, 0x00, 0xC0, 0x82, 0x20, 0x00 };
            adler32.Reset();
            adler32.Update(arr1);
            adler32.Update(arr2);
            Debug.WriteLine(adler32.Checksum.ToString("X8"));
        }

        [TestMethod]
        public void TestDxt5Uncompress()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var rawDxt = Path.Combine(resPath, "D愛子a_春服-pure", "0.raw");
            var rawBytes = File.ReadAllBytes(rawDxt);
            RL.ConvertToImageFile(rawBytes, rawDxt + "-convert.png", 4096, 4096, PsbImageFormat.Png, PsbPixelFormat.DXT5);
        }

        [TestMethod]
        public void TestDxt5Compress()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var rawPng = Path.Combine(resPath, "D愛子a_春服-pure", "0.png");
            Bitmap bitmap = new Bitmap(rawPng);
            var bc3Bytes = DxtUtil.Dxt5Encode(bitmap);
            RL.ConvertToImageFile(bc3Bytes, rawPng + "-convert.png", 4096, 4096, PsbImageFormat.Png, PsbPixelFormat.DXT5);
        }

        [TestMethod]
        public void TestRlUncompress()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");

            var path = Path.Combine(resPath, "澄怜a_裸.psb-pure", "84.bin"); //輪郭00
            RL.UncompressToImageFile(File.ReadAllBytes(path), path + ".png", 570, 426);
            path = Path.Combine(resPath, "澄怜a_裸.psb-pure", "89.bin"); //胸00
            RL.UncompressToImageFile(File.ReadAllBytes(path), path + ".png", 395, 411);
        }

        [TestMethod]
        public void TestRlCompress()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            string path;
            byte[] bytes;
            path = Path.Combine(resPath, "澄怜a_裸.psb-pure", "84.bin"); //輪郭00
            RL.UncompressToImageFile(File.ReadAllBytes(path), path + ".png", 570, 426);
            bytes = RL.CompressImageFile(path + ".png");
            File.WriteAllBytes(path + ".rl", bytes);
            RL.UncompressToImageFile(File.ReadAllBytes(path + ".rl"), path + ".rl.png", 570, 426);
            Assert.IsTrue(bytes.SequenceEqual(File.ReadAllBytes(path)));

            path = Path.Combine(resPath, "澄怜a_裸.psb-pure", "89.bin"); //胸00
            RL.UncompressToImageFile(File.ReadAllBytes(path), path + ".png", 395, 411);
            bytes = RL.CompressImageFile(path + ".png");
            File.WriteAllBytes(path + ".rl", bytes);
            RL.UncompressToImageFile(File.ReadAllBytes(path + ".rl"), path + ".rl.png", 395, 411);
            Assert.IsTrue(bytes.SequenceEqual(File.ReadAllBytes(path)));
        }

        [TestMethod]
        public void TestRlDirectCompress()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "emote_test2-pure", "tex-texture.png");
            var bytes = RL.CompressImageFile(path, PsbPixelFormat.CommonRGBA8);
            File.WriteAllBytes(path + ".rl", bytes);
        }

        [TestMethod]
        public void TestTlgDecode()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            //var path = Path.Combine(resPath, "title-pimg");
            var path = Path.Combine(resPath, "conf");
            TlgImageConverter image = new TlgImageConverter();
            foreach (var tlg in Directory.EnumerateFiles(path, "*.tlg"))
            {
                using (var stream = File.OpenRead(tlg))
                {
                    var br = new BinaryReader(stream);
                    var img = image.Read(br);
                    img.Save($"{tlg}.png", ImageFormat.Png);
                }
            }
        }

        [TestMethod]
        public void TestRGBA4444()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            //var path = Path.Combine(resPath, "emote_test.pure", "tex#000-texture.raw");
            var path = Path.Combine(resPath, "emote_test.pure", "tex#000-texture.png");
            var bts = RL.GetPixelBytesFromImageFile(path, PsbPixelFormat.RGBA4444);
            Assert.IsTrue(
                bts.SequenceEqual(
                    File.ReadAllBytes(
                        Path.Combine(resPath, "emote_test.pure", "tex#000-texture.raw"))));
                
            RL.ConvertToImageFile(bts, "rgba4444.png", 2048, 2048, PsbImageFormat.Png, PsbPixelFormat.RGBA4444);
        }
    }
}
