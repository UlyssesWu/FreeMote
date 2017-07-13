using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            //
            //TODO:  在此处添加构造函数逻辑
            //
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
        public void TestRlUncompress()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");

            var path = Path.Combine(resPath, "澄怜a_裸.psb-pure", "84.bin"); //輪郭00
            RlCompress.UncompressToImageFile(File.ReadAllBytes(path), path + ".png", 570, 426);
            path = Path.Combine(resPath, "澄怜a_裸.psb-pure", "89.bin"); //胸00
            RlCompress.UncompressToImageFile(File.ReadAllBytes(path), path + ".png", 395, 411);
        }

        [TestMethod]
        public void TestRlCompress()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            string path;
            byte[] bytes;
            path = Path.Combine(resPath, "澄怜a_裸.psb-pure", "84.bin"); //輪郭00
            RlCompress.UncompressToImageFile(File.ReadAllBytes(path), path + ".png", 570, 426);
            bytes = RlCompress.CompressImageFile(path + ".png");
            File.WriteAllBytes(path + ".rl", bytes);
            RlCompress.UncompressToImageFile(File.ReadAllBytes(path + ".rl"), path + ".rl.png", 570, 426);
            Assert.IsTrue(bytes.SequenceEqual(File.ReadAllBytes(path)));

            path = Path.Combine(resPath, "澄怜a_裸.psb-pure", "89.bin"); //胸00
            RlCompress.UncompressToImageFile(File.ReadAllBytes(path), path + ".png", 395, 411);
            bytes = RlCompress.CompressImageFile(path + ".png");
            File.WriteAllBytes(path + ".rl", bytes);
            RlCompress.UncompressToImageFile(File.ReadAllBytes(path + ".rl"), path + ".rl.png", 395, 411);
            Assert.IsTrue(bytes.SequenceEqual(File.ReadAllBytes(path)));
        }
    }
}
