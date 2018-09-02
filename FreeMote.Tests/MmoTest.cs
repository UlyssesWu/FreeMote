using System;
using System.Collections.Generic;
using System.Drawing;
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
            MmoBuilder.DebugMode = true;
            var psbMmo = MmoBuilder.Build(psb);
            //mmo.Objects["objectChildren"] = psbMmo.Objects["objectChildren"];
            var data = (PsbDictionary) mmo.Objects["metaformat"].Children("data");
            var data2 = (PsbDictionary) psbMmo.Objects["metaformat"].Children("data");
            data["bustControlDefinitionList"] = data2["bustControlDefinitionList"];
            mmo.Merge();
            mmo.SaveAsMdfFile(Path.Combine(resPath, "mmo", "temp.mmo"));

        }

        [TestMethod]
        public void TestMmoGraft2()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "template39.json");
            var path2 = Path.Combine(resPath, "template39-krkr.json");
            //var path = Path.Combine(resPath, "e-mote38基本テンプレート(正面zバイナリ専用)_free.json");
            //var path2 = Path.Combine(resPath, "e-mote3.0ショコラパジャマa中-krkr.json");
            //var path2 = Path.Combine(resPath, "mmo", "e-mote38基本テンプレート(正面zバイナリ専用)-krkr.json");
            var mmo = PsbCompiler.LoadPsbFromJsonFile(path);
            var psb = PsbCompiler.LoadPsbFromJsonFile(path2);
            var psbMmo = MmoBuilder.Build(psb);
            psbMmo.Objects["metaformat"] = mmo.Objects["metaformat"];
            psbMmo.Merge();
            File.WriteAllBytes(Path.Combine(resPath, "mmo", "crash-temp.mmo"), psbMmo.Build());
        }

        [TestMethod]
        public void TestBuildMmo()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "e-mote3.0ショコラパジャマa中-krkr.json");
            //var path = Path.Combine(resPath, "template39-krkr.json");
            var psb = PsbCompiler.LoadPsbFromJsonFile(path);
            MmoBuilder.DebugMode = true;
            var psbMmo = MmoBuilder.Build(psb);
            psbMmo.Merge();
            File.WriteAllBytes(Path.Combine(resPath, "mmo", "NekoCrash.mmo"), psbMmo.Build());
        }

        [TestMethod]
        public void TestCompareMmo()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res\mmo");
            var path = Path.Combine(resPath, "template39.json");
            var path2 = Path.Combine(resPath, "temp.mmo");

            var mmo1 = PsbCompiler.LoadPsbFromJsonFile(path);
            var allpart1 = FindPart((PsbCollection)mmo1.Objects["objectChildren"], "body_parts");
            var mmo2 = new PSB(path2);
            var allpart2 = FindPart((PsbCollection)mmo2.Objects["objectChildren"], "body_parts");

            //var p1 = mmo1.Objects.FindByPath(
            //    "/objectChildren/[3]/children/[1]/layerChildren/[0]/children/[0]/frameList/[0]/content/coord");
            //var pp = ((IPsbChild) p1).Parent.Parent.Parent.Parent["label"];
            PsbDictionary FindPart(PsbCollection col, string label)
            {
                foreach (var c in col)
                {
                    if (c is PsbDictionary d)
                    {
                        if (d["label"] is PsbString s && s.Value == label)
                        {
                            return d;
                        }
                    }
                }

                return null;
            }
            PsBuildTest.CompareValue(allpart1, allpart2);
        }
    }
}
