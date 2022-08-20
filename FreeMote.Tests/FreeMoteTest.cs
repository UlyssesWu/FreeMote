using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using BCnEncoder.Shared.ImageFiles;
using FreeMote.FastLz;
using FreeMote.Plugins.Audio;
using FreeMote.Plugins.Images;
using FreeMote.Plugins.Shells;
using FreeMote.Psb;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VGAudio.Containers.Dsp;
using VGAudio.Containers.Wave;


namespace FreeMote.Tests
{
    [TestClass]
    public class FreeMoteTest
    {
        public FreeMoteTest()
        {
        }

        public TestContext TestContext { get; set; }

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
        public void TestBTree()
        {
            var list = new List<string> {"aabc", "deff", "acebdf"};
            PrefixTree prefixTree = new PrefixTree(list);
            PrefixTree.Build(list, out var names, out var tree, out var offsets);

        }

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

                var fileName =
                    Path.GetFileNameWithoutExtension(file).Split(new[] {'-'}, 2); //rename your file as key-name.psb
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
        public void TestDecode()
        {
            //uint targetKey = 3803466536; //give your model key
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var psbPath = Path.Combine(resPath, "C2_EA.psp");

            PspShell shell = new PspShell();
            var stream = File.OpenRead(psbPath);
            if (shell.IsInShell(stream, new Dictionary<string, object>()))
            {
                var ms = shell.ToPsb(stream, null);
                //PSB psb = new PSB(ms);
                //psb.Merge();
                //File.WriteAllBytes(psbPath + "-pure.psb", psb.Build());
            }
        }

        [TestMethod]
        public void TestChecksum()
        {
            byte[] arr1 =
            {
                0x2C, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0xBD, 0x8C, 0x08, 0x00, 0x19, 0x31, 0x08, 0x00,
                0xD1, 0xAF, 0x08, 0x00, 0x69, 0x0F, 0x08, 0x00, 0xF4, 0xAF, 0x08, 0x00, 0x66, 0x94, 0x00, 0x05
            };
            byte[] arr2 = {0x1F, 0x0F, 0x08, 0x00, 0xD6, 0xAF, 0x08, 0x00, 0xC0, 0x82, 0x20, 0x00};
            Adler32 adler32 = new Adler32();
            adler32.Update(arr1);
            adler32.Update(arr2);
            Debug.WriteLine(adler32.Checksum.ToString("X8"));
            Assert.AreEqual(0xC02709D3, adler32.Checksum);

            arr1 = new byte[]
            {
                0x2C, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0xBD, 0x8C, 0x08, 0x00, 0x19, 0x31, 0x08, 0x00,
                0xD1, 0xAF, 0x08, 0x00, 0x69, 0x0F, 0x08, 0x00, 0xF4, 0xAF, 0x08, 0x00, 0x66, 0x94, 0x00, 0x00
            };
            arr2 = new byte[]
                {0xAB, 0x0F, 0x08, 0x00, 0xD6, 0xAF, 0x08, 0x00, 0xC0, 0x82, 0x20, 0x00};
            adler32.Reset();
            adler32.Update(arr1);
            adler32.Update(arr2);
            Debug.WriteLine(adler32.Checksum.ToString("X8"));
        }

        [TestMethod]
        public void TestA8()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var imgPath = Path.Combine(resPath, "textfont24", "[0]-[0].png");
            var bts = RL.GetPixelBytesFromImageFile(imgPath, PsbPixelFormat.A8);
            RL.ConvertToImageFile(bts, imgPath + "output.png", 2048, 2048, PsbImageFormat.png, PsbPixelFormat.A8);
        }

        [TestMethod]
        public void TestL8()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var imgPath = Path.Combine(resPath, "map00.psb.m", "0.raw");
            
            var img = RL.ConvertToImage(File.ReadAllBytes(imgPath), 1024, 1024, PsbPixelFormat.L8);
            img.Save("l8.png", ImageFormat.Png);
            //RL.ConvertToImageFile(bts, imgPath + "output.png", 544, 960, PsbImageFormat.png, PsbPixelFormat.L8_SW);
        }

        [TestMethod]
        public void TestPal()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            //var imgPath = Path.Combine(resPath, "akatsuki.no.goei.image.samples", "ev_aya07.psb", "ev_aya07_half-5.png");
            var imgPath = Path.Combine(resPath, "textfont20_2.psb", "[0]-[0].png");

            var img = BitmapHelper.LoadBitmap(File.ReadAllBytes(imgPath));
            var locked = img.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.ReadOnly, img.PixelFormat);
            Debug.WriteLine($"{locked.Stride},{locked.Width},{locked.Height},{locked.PixelFormat}");
            //RL.ConvertToImageFile(bts, imgPath + "output.png", 544, 960, PsbImageFormat.png, PsbPixelFormat.L8_SW);
        }


        [TestMethod]
        public void TestRGBA8SW()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var imgPath = Path.Combine(resPath, "pm_ev001b.psb", "0.bin");
            RL.ConvertToImageFile(File.ReadAllBytes(imgPath), imgPath + "output.png", 1024, 32, PsbImageFormat.png, PsbPixelFormat.BeRGBA8_SW);
        }

        [TestMethod]
        public void TestCI8()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var palPath = Path.Combine(resPath, "pm_title.psb", "2.bin");
            var imgPath = Path.Combine(resPath, "pm_title.psb", "11.bin");

            var bmp = RL.ConvertToImageWithPalette(File.ReadAllBytes(imgPath), File.ReadAllBytes(palPath), 1024,
                512, PsbPixelFormat.CI8_SW);
            bmp.Save("ci8_10.png", ImageFormat.Png);
        }

        [TestMethod]
        public void TestCI8_Pal()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var palPath = Path.Combine(resPath, "config.psb", "2.bin");
            var imgPath = Path.Combine(resPath, "config.psb", "11.bin");

            var bmp = RL.ConvertToImageWithPalette(File.ReadAllBytes(imgPath), File.ReadAllBytes(palPath), 1024,
                512, PsbPixelFormat.CI8_SW);
            bmp.Save("ci8_10.png", ImageFormat.Png);
        }

        [TestMethod]
        public void TestFastLz()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "raw.mfl");
            var input = File.OpenRead(path);
            var outStream = PsbExtension.EncodeMdf(input, "Rj9Pegoh4e_viewer.psb.m", 97, false);
            var buffer = outStream.ToArray();
            var output = FastLzNative.Decompress(buffer, 13504);
            File.WriteAllBytes("decode-raw.mfl", output);
        }

        [TestMethod]
        public void TestDxt5Decompress()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var rawDxt = Path.Combine(resPath, "D愛子a_春服-pure", "0.raw");
            var rawBytes = File.ReadAllBytes(rawDxt);
            RL.ConvertToImageFile(rawBytes, rawDxt + "-convert.png", 4096, 4096,
                PsbImageFormat.png, PsbPixelFormat.DXT5);
        }

        [TestMethod]
        public void TestDxt5Compress()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var rawPng = Path.Combine(resPath, "D愛子a_春服-pure", "0.png");
            Bitmap bitmap = new Bitmap(rawPng);
            var bc3Bytes = DxtUtil.Dxt5Encode(bitmap);
            RL.ConvertToImageFile(bc3Bytes, rawPng + "-convert.png", 4096, 4096,
                PsbImageFormat.png, PsbPixelFormat.DXT5);
        }

        [TestMethod]
        public void TestRlDecompress()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "澄怜a_裸.psb-pure", "84.bin"); //輪郭00
            RL.DecompressToImageFile(File.ReadAllBytes(path), path + ".png", 426, 570);
            path = Path.Combine(resPath, "澄怜a_裸.psb-pure", "89.bin"); //胸00
            RL.DecompressToImageFile(File.ReadAllBytes(path), path + ".png", 411, 395);
        }

        [TestMethod]
        public void TestRlCompress()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            string path;
            byte[] bytes;
            path = Path.Combine(resPath, "澄怜a_裸.psb-pure", "84.bin"); //輪郭00
            RL.DecompressToImageFile(File.ReadAllBytes(path), path + ".png", 426, 570);
            bytes = RL.CompressImageFile(path + ".png");
            File.WriteAllBytes(path + ".rl", bytes);
            RL.DecompressToImageFile(File.ReadAllBytes(path + ".rl"), path + ".rl.png", 426, 570);
            Assert.IsTrue(bytes.SequenceEqual(File.ReadAllBytes(path)));

            path = Path.Combine(resPath, "澄怜a_裸.psb-pure", "89.bin"); //胸00
            RL.DecompressToImageFile(File.ReadAllBytes(path), path + ".png", 411, 395);
            bytes = RL.CompressImageFile(path + ".png");
            File.WriteAllBytes(path + ".rl", bytes);
            RL.DecompressToImageFile(File.ReadAllBytes(path + ".rl"), path + ".rl.png", 411, 395);
            Assert.IsTrue(bytes.SequenceEqual(File.ReadAllBytes(path)));
        }
        
        [TestMethod]
        public void TestRlDirectCompress()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "emote_test2-pure", "tex-texture.png");
            var bytes = RL.CompressImageFile(path, PsbPixelFormat.BeRGBA8);
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
        public void TestVagDecode()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "pm_a01_01_eru0000.psb.pure", "pm_a01_01_eru0000.vag");
            var vag = new VagFile(path);
            vag.Load();
            vag.WriteToWavFile("test.wav");
        }

        [TestMethod]
        public void TestDspDecode()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "[nx][adpcm]syssearc", "1.raw");
            DspReader reader = new DspReader();
            var data = reader.Read(File.ReadAllBytes(path));
            using MemoryStream oms = new MemoryStream();
            WaveWriter writer = new WaveWriter();
            writer.WriteToStream(data, oms, new WaveConfiguration { Codec = WaveCodec.Pcm16Bit }); //only 16Bit supported
            File.WriteAllBytes(path + ".wav", oms.ToArray());
        }

        [TestMethod]
        public void TestRGBA4444()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            //var path = Path.Combine(resPath, "emote_test.pure", "tex#000-texture.raw");
            var path = Path.Combine(resPath, "emote_test.pure", "tex#000-texture.png");
            var bts = RL.GetPixelBytesFromImageFile(path, PsbPixelFormat.LeRGBA4444);
            Assert.IsTrue(
                bts.SequenceEqual(
                    File.ReadAllBytes(
                        Path.Combine(resPath, "emote_test.pure", "tex#000-texture.raw"))));

            RL.ConvertToImageFile(bts, "rgba4444.png", 2048, 2048, PsbImageFormat.png, PsbPixelFormat.LeRGBA4444);
        }

        [TestMethod]
        public void TestSwizzle()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "textfont14.psb", "4.bin");
            var pixels = File.ReadAllBytes(path);
            var cleanPixels = PostProcessing.UnswizzleTexturePSP(pixels, 512, 512, PixelFormat.Format4bppIndexed);
            var swizzledPixels = PostProcessing.SwizzleTexturePSP(cleanPixels, 512, 512, PixelFormat.Format4bppIndexed);

            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i] != swizzledPixels[i])
                {
                    Debug.WriteLine($"[{i}] {pixels[i]} vs {swizzledPixels[i]}");
                }
            }
            Assert.IsTrue(pixels.SequenceEqual(swizzledPixels.Take(pixels.Length)));
        }

        [TestMethod]
        public void TestFlip()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "base_00b33f69d51e22005fd754f79e494968", "2.bin");
            var pixels = File.ReadAllBytes(path);
            var p1 = PostProcessing.UnswizzleTexture(pixels, 1024, 256, PixelFormat.Format32bppArgb);
            var p2 = PostProcessing.FlipPs3Texture(p1, 1024, 256, PixelFormat.Format32bppArgb);
            var p3 = PostProcessing.FlipPs3Texture(p2, 1024, 256, PixelFormat.Format32bppArgb);
            var p4 = PostProcessing.SwizzleTexture(p3, 1024, 256, PixelFormat.Format32bppArgb);

            Assert.IsTrue(pixels.SequenceEqual(p4.Take(pixels.Length)));
        }

        [TestMethod]
        public void TestBc7()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "ac_lifegame.psb", "0.bin");
            var buffer = File.ReadAllBytes(path);

            var decoder = new BcDecoder();

            var width = 4096;
            var height = 3056;
            var bufferSize = decoder.GetBlockSize(CompressionFormat.Bc7) * width * height;
            Debug.WriteLine($"size: expect: {bufferSize} ; actual: {buffer.Length}");

            var pixels = decoder.DecodeRaw(buffer, width, height, CompressionFormat.Bc7);
            var pixelBytes = MemoryMarshal.Cast<ColorRgba32, byte>(pixels);
            RL.ConvertToImageFile(pixelBytes.ToArray(), "bc7-be.png", width, height, PsbImageFormat.png, PsbPixelFormat.BeRGBA8);
        }

        [TestMethod]
        public void TestBc7Plugin()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "ac_lifegame.psb", "0.bin");
            var buffer = File.ReadAllBytes(path);

            Bc7Formatter bc7Formatter = new Bc7Formatter();
            var bitmap = bc7Formatter.ToBitmap(buffer, 4096, 3056, PsbSpec.nx);
            bitmap.Save("bc7-plugin.png");

            var buffer2 = bc7Formatter.ToBytes(bitmap, PsbSpec.nx);
            var bitmap2 = bc7Formatter.ToBitmap(buffer2, 4096, 3056, PsbSpec.nx);
            bitmap2.Save("bc7-plugin2.png");
        }

        //[TestMethod]
        //public void TestGraphics()
        //{
        //    //No, it won't work
        //    var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
        //    var path1 = Path.Combine(resPath, "textfont20_2.psb", "[0]-[0].png");
        //    var path2 = Path.Combine(resPath, "textfont20_2.psb", "[1]-[1].png");
        //    Bitmap bmp1 = BitmapHelper.LoadBitmap(File.ReadAllBytes(path1));
        //    Bitmap bmp2 = BitmapHelper.LoadBitmap(File.ReadAllBytes(path2));
        //    Bitmap bmpCanvas = new Bitmap(1024, 512, bmp1.PixelFormat);

        //    bmpCanvas.Palette = bmp1.Palette;
        //    Graphics g = Graphics.FromImage(bmpCanvas);
        //    g.DrawImage(bmp2, new Point(512, 0));
        //    g.Save();
        //    bmpCanvas.Save("4bpp.png");
        //}

        [TestMethod]
        public void TestPath()
        {
            var path = PathNetCore.GetRelativePath(@"C:\\abc\def\", @"C:\\abc\def\image/tex.png"); //"image\tex.png"
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

                bw.Write((byte) 0);

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
                    bw.Write((byte) 0);
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
                bw.Write((byte) 0);
                sw.Stop();
                time = sw.Elapsed;
                Console.WriteLine($"Time of GetBytes: \t{time}");
            }
        }
    }
}