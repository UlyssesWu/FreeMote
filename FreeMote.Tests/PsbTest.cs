using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using FreeMote.Plugins;
using FreeMote.Plugins.Shells;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FreeMote.Psb;


namespace FreeMote.Tests
{
    /// <summary>
    /// PsbTest 的摘要说明
    /// </summary>
    [TestClass]
    public class PsbTest
    {
        public PsbTest()
        {
        }

        /// <summary>
        ///获取或设置测试上下文，该上下文提供
        ///有关当前测试运行及其功能的信息。
        ///</summary>
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
        public void TestPsbLoad()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var paths = Directory.GetFiles(resPath, "*pure.psb");
            using (FileStream fs = new FileStream(paths[0], FileMode.Open))
            {
                PSB psb = new PSB(fs);
            }
        }

        [TestMethod]
        public void TestPsbV4()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "dx_ふかみ_駅員服.psb");
            Consts.PsbObjectOrderByKey = false;
            Consts.FastMode = false;
            //var path = Path.Combine(resPath, "東北ずん子e-mote_ver39(max2048).psb");
            PSB psb = new PSB(path);
            psb.BuildToFile("regenerated.psb");
        }

        [TestMethod]
        public void TestPsbEncoding()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "staffroll_script.txt.scn");

            //Consts.PsbEncoding = Encoding.GetEncoding("SHIFT-JIS");
            PSB psb = new PSB();
            psb.LoadFromStream(File.OpenRead(path));
            foreach (var psbString in psb.Strings)
            {
                var str = psbString;
                var str2 = Encoding.GetEncoding("SHIFT-JIS").GetString(Encoding.UTF8.GetBytes(str.Value));
            }
        }

        [TestMethod]
        public void TestPsbLoadV4()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var paths = Directory.GetFiles(resPath, "*v4.psb");
            using (FileStream fs = new FileStream(paths[0], FileMode.Open))
            {
                PSB psb = new PSB(fs);
            }
        }

        [TestMethod]
        public void TestPsbLoadKrkr()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var paths = Directory.GetFiles(resPath, "澄怜a_裸.psb-pure.psb");
            using (FileStream fs = new FileStream(paths[0], FileMode.Open))
            {
                PSB psb = new PSB(fs);
            }
        }

        [TestMethod]
        public void TestPsbNumbers()
        {
            var p1 = new PsbNumber(2.4f);
            using (var ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);
                BinaryReader br = new BinaryReader(ms);
                p1.WriteTo(bw);
                var bts = ms.ToArray();
                ms.Seek(0, SeekOrigin.Begin);
                var p2 = new PsbNumber((PsbObjType) br.ReadByte(), br);
                Assert.AreEqual(p1.IntValue, p2.IntValue);
            }
        }

        [TestMethod]
        public void TestPsbArrays()
        {
            var a1 = new PsbArray(new List<uint> {4, 3, 9, 6});
            using (var ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);
                BinaryReader br = new BinaryReader(ms);
                a1.WriteTo(bw);
                var bts = ms.ToArray();
                ms.Seek(0, SeekOrigin.Begin);
                br.ReadByte();
                var a2 = new PsbArray((int) 1, br);
                Assert.AreEqual(a1[0], a2[0]);
            }
        }

        [TestMethod]
        public void TestPsbStrings()
        {
            var se1 = PsbString.Empty;
            var se2 = PsbString.Empty;
            se1.Index = 114514;
            var se2i = se2.Index;
            return;

            var p1 = new PsbString("PSB");
            var p2 = new PsbString("PSB", index: 1);
            var p3 = new PsbString("MDF", 1);
            var p4 = new PsbString("MDF");
            var s1 = "PSB";
            var r = p1 == p2;
            r = p2 == p3;
            r = p3 == p4;
            r = p1 == s1;
            r = s1 == p1;
            r = p3 == s1;
            p2.Index = null;
            r = p2 == p3;
            p2.Index = 1;
            var dic = new Dictionary<PsbString, string>();
            dic.Add(p3, "mdf");
            r = dic.ContainsKey(p4);
            r = dic.ContainsKey(p2);
            r = dic.ContainsKey(p1);
            dic.Add(p1, "psb");
            r = dic.ContainsKey(p4);
            r = dic.ContainsKey(p2);
            r = dic.ContainsKey(p1);
        }

        [TestMethod]
        public void TestInfoPsb()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "scenario_info.psb.m");

            var ctx = FreeMount.CreateContext();
            ctx.Context[Consts.Context_MdfKey] = "38757621acf82scenario_info.psb.m";
            ctx.Context[Consts.Context_MdfKeyLength] = 131;

            var mdfShell = new MdfShell();
            //var ms = mdfShell.ToPsb(File.OpenRead(path), ctx.Context);
            //HINT: brute get info-psb key is nearly impossible, don't waste your time on it and just find the key by yourself
            var ms = mdfShell.EncodeMdf(File.OpenRead(path), "38757621acf82scenario_info.psb.m", 131);
            File.WriteAllBytes(path + ".raw", ms.ToArray());
        }

        [TestMethod]
        public void TestMdf()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "e-mote38_free.mmo");
            //var path = Path.Combine(resPath, "e-mote38_free.mmo-repack.mmo");
            var path2 = Path.Combine(resPath, "emote38-pure.mmo");

            MdfFile.CompressPsbToMdfStream(File.OpenRead(path2)).CopyTo(File.Create(path + "-repack.mmo"));

            using (var mdfStream = File.OpenRead(path))
            {
                using (var psbStream = MdfFile.DecompressToPsbStream(mdfStream))
                {
                    //using (var pureStream = new MemoryStream((int)psbStream.Length))
                    {
                        //PsbFile.Encode(key, EncodeMode.Decrypt, EncodePosition.Auto, psbStream, pureStream);
                        PSB psb = new PSB(psbStream);
                        psb.SaveAsMdfFile(path + "-build.mmo");
                    }
                }
            }
        }

        [TestMethod]
        public void TestPlugins()
        {
            Debug.WriteLine(FreeMount.CurrentPath);
            FreeMount.Init();
            var resource = new ImageMetadata
            {
                Compress = PsbCompressType.Tlg,
                Name = "test.tlg",
                Resource = new PsbResource()
            };
            resource.SetData(new Bitmap(100,100));
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

        [TestMethod]
        public void TestDrawWin()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "emote_logo_d5-pure.psb");
            //var path = Path.Combine(resPath, "vanilla-pure.psb");
            var psb = new PSB(path);
            var painter = new PsbPainter(psb);
            var bmp = painter.Draw(4096, 4096);
            bmp.Save("RenderWin.png", ImageFormat.Png);
        }

        [TestMethod]
        public void TestDrawCommon()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "akira_guide-pure.psb");
            var psb = new PSB(path);
            var painter = new PsbPainter(psb);
            var bmp = painter.Draw(2048, 2048);
            bmp.Save("RenderCommon.png", ImageFormat.Png);
        }

        [TestMethod]
        public void TestDullahanPsb()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            //var path = Path.Combine(resPath, "e-mote38_KRKR-pure.psb");
            //var path = Path.Combine(resPath, "HD愛子a_春服.psb");
            //var path2 = Path.Combine(resPath, "HD愛子a_春服-pure.psb");
            //var path = Path.Combine(resPath, "akira_guide.psb");
            //var path2 = Path.Combine(resPath, "akira_guide-pure.psb");
            //var path = Path.Combine(resPath, "dx_真闇_裸_impure.psb");
            //var path2 = Path.Combine(resPath, "dx_真闇_裸-pure.psb");
            //var path = Path.Combine(resPath, "emote396-win.psb");
            //var path2 = Path.Combine(resPath, "emote396-win.pure.psb");
            var path = Path.Combine(resPath, "ca01_l_body_1.psz.psb");
            var path2 = Path.Combine(resPath, "ca01_l_body_1-pure.psb");

            var psb = PSB.DullahanLoad(new FileStream(path, FileMode.Open), 64);
            var p2 = new PsbFile(path2);

            var offset1 = psb.Header.OffsetChunkData;
            var offset2 = p2.Header.OffsetChunkData;
            Assert.AreEqual(offset1, offset2);

            var obj = psb.Objects.First();
            //if (psb.Platform == PsbSpec.krkr)
            //{
            //    psb.SwitchSpec(PsbSpec.win);
            //}
            //psb.Merge();
            //var r = psb.Resources[0].Data.Length;
            //File.WriteAllBytes("Dullahan.psb", psb.Build());
        }

        [TestMethod]
        public void TestDullahanScn()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "00_pro02.txt.scn");
            //var path = Path.Combine(resPath, "エリナ－その６_β（邂逅）.ks.scn");
            //var rPsb = new PSB(path);
            var fs = new FileStream(path, FileMode.Open);
            Stopwatch sw = Stopwatch.StartNew();
            //var psb = PSB.DullahanLoad(fs, 64);
            var psb = new PSB(fs);
            sw.Stop();
            var time = sw.Elapsed;
            //return;
            Consts.InMemoryLoading = false;
            fs.Position = 0;
            sw.Restart();
            //psb = PSB.DullahanLoad(fs, 64);
            psb = new PSB(fs);
            sw.Stop();
            time = sw.Elapsed;
        }

        [TestMethod]
        public void TestArchiveInfoGetAllPossibleFileNames()
        {
            var results = PsbExtension.ArchiveInfoGetAllPossibleFileNames("scenario/ca01_06.txt.scn.m", ".psb.m");
            foreach (var result in results)
            {
                Console.WriteLine(result);
            }
        }
    }
}