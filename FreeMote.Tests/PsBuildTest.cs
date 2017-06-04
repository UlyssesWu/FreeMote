using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using FreeMote.PsBuild;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace FreeMote.Tests
{
    /// <summary>
    /// PsBuildTest 的摘要说明
    /// </summary>
    [TestClass]
    public class PsBuildTest
    {
        public PsBuildTest()
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
        public void TestDecompile()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var paths = Directory.GetFiles(resPath, "*pure.psb");
            var target = paths[0];
            var json = PsbDecompiler.Decompile(target);
            File.WriteAllText(target + ".json", json);
        }

        [TestMethod]
        public void TestCompile()
        {

        }

        [TestMethod]
        public void TestJsonNumbers()
        {
            List<float> floats = new List<float> { -0.00000001f, 1/3f, -0.000027f, 19.200079f, (float)Math.PI, float.MinValue};
            string json = JsonConvert.SerializeObject(floats);
            Console.WriteLine(json);
            var result = JsonConvert.DeserializeObject<List<float>>(json);
            for (int i = 0; i < result.Count; i++)
            {
                Assert.AreEqual(floats[i], result[i]);
            }

            List<double> doubles  = new List<double> { double.MinValue, double.MaxValue, 123456789.0, -0.00000001, 0.03, 0.4 };
            json = JsonConvert.SerializeObject(doubles);
            Console.WriteLine(json);
            var result2 = JsonConvert.DeserializeObject<List<double>>(json);
            for (int i = 0; i < result2.Count; i++)
            {
                Assert.AreEqual(doubles[i], result2[i]);
            }
        }
    }
}
