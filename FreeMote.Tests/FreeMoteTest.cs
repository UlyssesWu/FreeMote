using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FreeMote.Tests
{
    /// <summary>
    /// FreeMoteTest 的摘要说明
    /// </summary>
    [TestClass]
    public class FreeMoteTest
    {
        public FreeMoteTest()
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

    }
}
