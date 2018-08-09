using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using FreeMote.Plugins;
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
        public void TestTlgNative()
        {
            if (!TlgNativePlugin.IsReady)
            {
                return;
            }
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            //var path = Path.Combine(resPath, "title-pimg");
            var path = Path.Combine(resPath, "title-pimg", "566.tlg");
            var bmp = TlgNativePlugin.LoadTlg(File.ReadAllBytes(path), out int ver);
            var width = bmp.Width;
            var height = bmp.Height;
            bmp.Save("tlg.png", ImageFormat.Png);

            path = Path.Combine(resPath, "emote_test.pure", "tex#000-texture.png");
            Bitmap bmp2 = new Bitmap(path);
            var bts = TlgNativePlugin.SaveTlg(bmp2);
            TlgImageConverter converter = new TlgImageConverter();
            using (var ms = new MemoryStream(bts))
            {
                using (var br = new BinaryReader(ms))
                {
                    var bmp3 = converter.Read(br);
                    bmp3.Save("tlg2.png", ImageFormat.Png);
                }
            }
        }

        [TestMethod]
        public void TestRGBA4444()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            //var path = Path.Combine(resPath, "emote_test.pure", "tex#000-texture.raw");
            var path = Path.Combine(resPath, "emote_test.pure", "tex#000-texture.png");
            var bts = RL.GetPixelBytesFromImageFile(path, PsbPixelFormat.WinRGBA4444);
            Assert.IsTrue(
                bts.SequenceEqual(
                    File.ReadAllBytes(
                        Path.Combine(resPath, "emote_test.pure", "tex#000-texture.raw"))));

            RL.ConvertToImageFile(bts, "rgba4444.png", 2048, 2048, PsbImageFormat.Png, PsbPixelFormat.WinRGBA4444);
        }

        [TestMethod]
        public void TestReadWriteZeroTrimString()
        {
            string ReadPeekAndAppend(BinaryReader br)
            {
                StringBuilder sb = new StringBuilder();
                while (br.PeekChar() != 0)
                {
                    sb.Append(br.ReadChar());
                }
                br.ReadByte(); //skip \0 - fail if end without \0
                return sb.ToString();
            }

            string ReadDetectAndSwallow(BinaryReader br)
            {
                var pos = br.BaseStream.Position;
                var length = 0;
                while (br.ReadByte() > 0)
                {
                    length++;
                }
                br.BaseStream.Position = pos;
                var str = Encoding.UTF8.GetString(br.ReadBytes(length));
                br.ReadByte(); //skip \0 - fail if end without \0
                return str;
            }

            //Generate super long string
            var theNatureOfHuman = "人类的本质是:";
            var bts = Encoding.UTF8.GetBytes(theNatureOfHuman);
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            using (var br = new BinaryReader(ms))
            {
                for (int i = 0; i < 100_0000; i++)
                {
                    bw.Write(bts);
                }
                bw.Write((byte)0);

                Console.WriteLine("Read 1 long string:");
                ms.Position = 0;
                Stopwatch sw = Stopwatch.StartNew();
                var r = ReadPeekAndAppend(br);
                sw.Stop();
                var time = sw.Elapsed;
                Console.WriteLine($"Time of {nameof(ReadPeekAndAppend)}: \t{time}");

                ms.Position = 0;
                sw.Restart();
                r = ReadDetectAndSwallow(br);
                sw.Stop();
                time = sw.Elapsed;
                Console.WriteLine($"Time of {nameof(ReadDetectAndSwallow)}: \t{time}");

                ms.SetLength(0); //Clear
                for (int i = 0; i < 100_0000; i++)
                {
                    bw.Write(bts);
                    bw.Write((byte)0);
                }
                
                Console.WriteLine("Read 100_0000 short strings:");
                ms.Position = 0;
                sw.Restart();
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    r = ReadPeekAndAppend(br);
                }
                sw.Stop();
                time = sw.Elapsed;
                Console.WriteLine($"Time of {nameof(ReadPeekAndAppend)}:\t{time}");

                ms.Position = 0;
                sw.Restart();
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    r = ReadDetectAndSwallow(br);
                }
                sw.Stop();
                time = sw.Elapsed;
                Console.WriteLine($"Time of {nameof(ReadDetectAndSwallow)}:\t{time}");

                ms.Position = 0;
                Console.WriteLine("PeekChar vs AppendChar:");

                sw.Restart();
                for (int i = 0; i < 100_0000; i++)
                {
                    br.PeekChar();
                }
                sw.Stop();
                time = sw.Elapsed;
                Console.WriteLine($"Time of PeekChar: \t{time}");

                StringBuilder sb = new StringBuilder();
                sw.Restart();
                for (int i = 0; i < 100_0000; i++)
                {
                    sb.Append('咕');
                }
                //r = sb.ToString();
                sw.Stop();
                time = sw.Elapsed;
                Console.WriteLine($"Time of Append: \t{time}");

                Console.WriteLine("Write 1 long string:");
                ms.SetLength(0);
                r = sb.ToString();

                sw.Restart();
                bw.Write(r.ToCharArray());
                bw.Write((byte) 0);
                sw.Stop();
                time = sw.Elapsed;
                Console.WriteLine($"Time of ToCharArray: \t{time}");

                sw.Restart();
                bw.Write(Encoding.UTF8.GetBytes(r));
                bw.Write((byte)0);
                sw.Stop();
                time = sw.Elapsed;
                Console.WriteLine($"Time of GetBytes: \t{time}");
            }

        }
    }
}
