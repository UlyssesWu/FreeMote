using System;
using System.IO;
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

        private TestContext testContextInstance;

        /// <summary>
        ///获取或设置测试上下文，该上下文提供
        ///有关当前测试运行及其功能的信息。
        ///</summary>
        public TestContext TestContext
        {
            get => testContextInstance;
            set => testContextInstance = value;
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
            var p1 = new PsbNumber(225);
            using (var ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);
                BinaryReader br = new BinaryReader(ms);
                p1.WriteTo(bw);
                ms.Seek(0, SeekOrigin.Begin);
                var p2 = new PsbNumber((PsbObjType)br.ReadByte(), br);
                Assert.AreEqual(p1.IntValue, p2.IntValue);
            }
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
                using (var psbStream = MdfFile.UncompressToPsbStream(mdfStream))
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
    }
}
