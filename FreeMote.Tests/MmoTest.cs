using System;
using System.IO;
using FreeMote.Psb;
using FreeMote.PsBuild;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FreeMote.Tests
{
    [TestClass]
    public class MmoTest
    {
        public MmoTest()
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
        public void TestPackMmo()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "template39.json");
            var path2 = Path.Combine(resPath, "template39-krkr.json");
            var psb = PsbCompiler.LoadPsbFromJsonFile(path);
            //var psb2 = PsbCompiler.LoadPsbFromJsonFile(path2);
            //psb.Objects["objectChildren"] = psb2.Objects["object"];
            //var collection = (PsbCollection)psb.Objects["objectChildren"];
            //collection.RemoveAt(0);
            psb.Objects["metaformat"] = PsbNull.Null;
            psb.Merge();
            psb.SaveAsMdfFile("temp.mmo");
        }

        [TestMethod]
        public void TestLoadMmo()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "mmo", "template39.mmo");
            var psb = new PSB(path);
            var content = (PsbDictionary)psb.Objects.FindByPath("objectChildren/[0]/children/[0]/layerChildren/[0]/frameList/[0]/content");
            foreach (var kv in content)
            {
                var k = kv.Key;
                var v = kv.Value;
            }
        }

        [TestMethod]
        public void TestMmoGraft()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "template39.json");
            var path2 = Path.Combine(resPath, "template39-krkr.json");
            var mmo = PsbCompiler.LoadPsbFromJsonFile(path);
            var psb = PsbCompiler.LoadPsbFromJsonFile(path2);
            var psbMmo = MmoBuilder.Build(psb);
            mmo.Objects["objectChildren"] = psbMmo.Objects["objectChildren"];

            mmo.Merge();
            mmo.SaveAsMdfFile(Path.Combine(resPath, "mmo", "temp.mmo"));

        }
    }
}
