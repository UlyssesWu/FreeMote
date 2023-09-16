using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FreeMote.Plugins;
using FreeMote.Plugins.Shells;
using FreeMote.Psb;
using FreeMote.Psb.Textures;
using FreeMote.PsBuild;
using FreeMote.PsBuild.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FreeMote.Tests
{
    [TestClass]
    public class PsBuildTest
    {
        public PsBuildTest()
        {
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
        public void TestDecompile2()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var psbPath = Path.Combine(resPath, "ako.pure.psb");
            var json = PsbDecompiler.Decompile(psbPath);
        }

        [TestMethod]
        public void TestInplaceReplace()
        {
            FreeMount.Init();
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            //var path = Path.Combine(resPath, "dx_ふかみ_駅員服.psb");
            var path = Path.Combine(resPath, "dx_ふかみ_駅員服.lz4.psb");
            var jsonPath = Path.Combine(resPath, "dx_ふかみ_駅員服.json");
            PsbCompiler.InplaceReplaceToFile(path, jsonPath);
        }


        [TestMethod]
        public void TestDirectCompile()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "emote_test2-pure.psb");
            PSB psb = new PSB(path);
            psb.Header.Version = 3;
            psb.UpdateIndexes();
            File.WriteAllBytes(path + ".build.psb", psb.Build());
        }

        [TestMethod]
        public void TestJsonContext()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "dx_ふかみ_駅員服.resx.json");
            var obj = JsonConvert.DeserializeObject<PsbResourceJson>(File.ReadAllText(path));
            //var flattenArrays = obj.Context[Consts.Context_FlattenArray];
            //Console.WriteLine();
        }

        [TestMethod]
        public void TestCompileKrkr()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            //var path = Path.Combine(resPath, "澄怜a_裸.psb-pure.psb.json");
            var path = Path.Combine(resPath, "e-mote38_KRKR-pure.psb.json");
            PsbCompiler.CompileToFile(path, path + ".psbuild.psb", null, 4, null, PsbSpec.win);
        }

        [TestMethod]
        public void TestCompileWin()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            //var path = Path.Combine(resPath, "D愛子a_春服-pure.psb.json");
            var path = Path.Combine(resPath, "dx_れいなh1a1.psb.json");
            PsbCompiler.CompileToFile(path, path + ".psbuild.psb", null, 4, null, PsbSpec.win);
        }

        [TestMethod]
        public void TestCompileCommon()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "emote396-a8l8.pure.json");
            var path2 = Path.Combine(resPath, "emote396-a8l8.pure.psb");
            var psb = PsbCompiler.LoadPsbFromJsonFile(path);
            var psb2 = new PSB(path2);
            //File.WriteAllBytes("396.psb", psb.Build());
            PsbCompiler.CompileToFile(path, path + ".psbuild.psb", null, 4, null, PsbSpec.win);
        }

        [TestMethod]
        public void TestCompileEms()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            //var path = Path.Combine(resPath, "akira_guide-pure.psb.json");
            var path = Path.Combine(resPath, "emote_test2-pure.psb.json");
            var psb = PsbCompiler.LoadPsbFromJsonFile(path);
            psb.Platform = PsbSpec.ems;
            psb.Merge();
            File.WriteAllBytes(path + ".build.psb", psb.Build());
            //PsbCompiler.CompileToFile(path, path + ".psbuild.psb", null, 3, null, PsbSpec.ems);
        }

        [TestMethod]
        public void TestBTree()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "澄怜a_裸.psb-pure.psb");
            PSB psb = new PSB(path);
            var tree = PrefixTree.Build(psb.Names, out List<uint> oNames, out List<uint> oTrees, out List<uint> oOffsets);
            var list = PrefixTree.Load(oNames, oTrees, oOffsets);
            Assert.IsTrue(psb.Names.SequenceEqual(list));
        }

        [TestMethod]
        public void TestJsonNumbers()
        {
            string s = @"
[1.000000, 1.0000001, 1.001, 1.0, 1, 1.51, 233.333328,233.33333333333337]";
            var psbList = JsonConvert.DeserializeObject<PsbList>(s, new PsbJsonConverter());
            foreach (var psbValue in psbList)
            {
                var v = psbValue;
                var vt = psbValue.Type;
            }

            //var f = (111111111123L).ToString("X16");
            //var jj = JsonConvert.DeserializeObject("[0xDEADBEEF]");
            List<float> floats = new List<float>
                {-0.00000001f, 1 / 3f, -0.000027f, 19.200079f, (float) Math.PI, float.MinValue};
            string json = JsonConvert.SerializeObject(floats);
            Console.WriteLine(json);
            var result = JsonConvert.DeserializeObject<List<float>>(json);
            for (int i = 0; i < result.Count; i++)
            {
                Assert.AreEqual(floats[i], result[i]);
            }

            List<double> doubles = new List<double>
                {double.MinValue, double.MaxValue, 123456789.0, -0.00000001, 0.03, 0.4};
            json = JsonConvert.SerializeObject(doubles);
            Console.WriteLine(json);
            var result2 = JsonConvert.DeserializeObject<List<double>>(json);
            for (int i = 0; i < result2.Count; i++)
            {
                Assert.AreEqual(doubles[i], result2[i]);
            }
        }

        [TestMethod]
        public void TestGraft()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");

            //var path = Path.Combine(resPath, "澄怜a_裸.psb-pure.psb.json");
            var path = Path.Combine(resPath, "e-mote38_KRKR-pure.psb.json");
            var path2 = Path.Combine(resPath, "e-mote38_win-pure.psb.json");
            PSB psbKrkr = PsbCompiler.LoadPsbFromJsonFile(path);
            PSB psbWin = PsbCompiler.LoadPsbFromJsonFile(path2);
            psbWin.SwitchSpec(PsbSpec.krkr);
            //var metadata = (PsbDictionary)psbWin.Objects["metadata"];
            //metadata["attrcomp"] = psbKrkr.Objects["metadata"].Children("attrcomp");
            psbWin.Merge();

            ////Graft
            var resKrkr = psbKrkr.CollectResources<ImageMetadata>(false);
            var resWin = psbWin.CollectResources<ImageMetadata>(false);
            var headWin = resWin.FirstOrDefault(r => r.Height == 186 && r.Width == 122);
            var headKrkr = resKrkr.FirstOrDefault(r => r.Height == 186 && r.Width == 122);
            if (headWin != null && headKrkr != null)
            {
                headWin.Resource.Data = headKrkr.Resource.Data;
            }

            //foreach (var resourceMetadata in resWin)
            //{
            //    var sameRes = resKrkr.FirstOrDefault(r => r.Height == resourceMetadata.Height && r.Width == resourceMetadata.Width);
            //    if (sameRes != null)
            //    {
            //        Console.WriteLine($"{sameRes} {sameRes.Width}x{sameRes.Height} found.");
            //        resourceMetadata.Resource.Data = sameRes.Resource.Data;
            //    }
            //}
            psbWin.Merge();
            File.WriteAllBytes("emote_win2krkr.psb", psbWin.Build());
            //File.WriteAllText("emote_krkr2win.json", PsbDecompiler.Decompile(psb2));
        }

        [TestMethod]
        public void TestFindByPath()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");

            var path = Path.Combine(resPath, "e-mote38_win-pure.psb.json");
            PSB psb = PsbCompiler.LoadPsbFromJsonFile(path);

            var obj = psb.Objects.FindByPath("/object/all_parts/motion/タイムライン構造/bounds");
            var type = obj.Type;

            var objs = psb.Objects.FindAllByPath("/object/*/motion/*");

            foreach (var psbValue in objs)
            {
                if (psbValue is PsbDictionary dic)
                {
                    var s = dic.GetName();
                    Console.WriteLine(s);
                }
                else
                {
                    Console.WriteLine($"Not a PsbObject: {psbValue}");
                }
            }
        }

        [TestMethod]
        public void TestSplitTexture()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");

            //var path = Path.Combine(resPath, "dx_e-mote3.0ショコラパジャマa-pure.psb.json");
            //var path = Path.Combine(resPath, "ca01_l_body_1.psz.psb-pure.psb.json");
            var path = Path.Combine(resPath, "e-mote38_win-pure.psb.json");
            //var path = Path.Combine(resPath, "akira_guide-pure.psb.json");
            //PSB psb = PsbCompiler.LoadPsbFromJsonFile(path);
            PSB psb = new PSB("emote_krkr2win.psb");
            psb.SplitTextureToFiles("texs");
        }

        [TestMethod]
        public void TestPackTexture()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");

            var path = Path.Combine(resPath, "澄怜a_裸.psb-pure");
            var savePath = Path.Combine(path, "packed");
            Dictionary<string, Image> imgs = new Dictionary<string, Image>();
            foreach (var file in Directory.EnumerateFiles(path, "*.png", SearchOption.AllDirectories))
            {
                imgs.Add(file, Image.FromFile(file));
            }

            TexturePacker packer = new TexturePacker
            {
                FitHeuristic = BestFitHeuristic.MaxOneAxis,
            };
            packer.Process(imgs, 4096, 5, false);
            if (Directory.Exists(savePath))
            {
                Directory.Delete(savePath, true);
            }

            Directory.CreateDirectory(savePath);
            packer.SaveAtlasses(Path.Combine(savePath, "tex.txt"));
        }

        [TestMethod]
        public void TestPathTravel()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");

            var path = Path.Combine(resPath, "e-mote38_win-pure.psb.json");
            PSB psb = PsbCompiler.LoadPsbFromJsonFile(path);

            var targetPath = "/object/all_parts/motion/タイムライン構造/bounds";
            var obj = (PsbDictionary) psb.Objects.FindByPath(targetPath);
            var objPath = obj.Path;
            Assert.AreEqual(targetPath, objPath);
        }

        [TestMethod]
        public void TestConvertWin2Krkr()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");

            //var path = Path.Combine(resPath, "e-mote38_win-pure.psb.json");
            var path = Path.Combine(resPath, "e-mote38_win-pure.psb");
            //var path = Path.Combine(resPath, "dx_e-mote3.0ショコラパジャマa-pure.psb.json");
            //PSB psb = PsbCompiler.LoadPsbFromJsonFile(path);
            PSB psb = new PSB(path);
            psb.SwitchSpec(PsbSpec.krkr);
            //Common2KrkrConverter converter = new Common2KrkrConverter();
            //converter.Convert(psb);
            psb.Merge();
            File.WriteAllBytes("emote_test_front.psb", psb.Build());
            File.WriteAllText("emote_test_front.json", PsbDecompiler.Decompile(psb));
            psb.SwitchSpec(PsbSpec.win);
            psb.Merge();
            File.WriteAllBytes("emote_2x.psb", psb.Build());
        }

        [TestMethod]
        public void TestConvertCommon2Krkr()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");

            var path = Path.Combine(resPath, "akira_guide-pure.psb.json");
            PSB psb = PsbCompiler.LoadPsbFromJsonFile(path);

            Common2KrkrConverter converter = new Common2KrkrConverter();
            converter.Convert(psb);
            psb.Merge();
            File.WriteAllBytes("emote_test_front.psb", psb.Build());
            File.WriteAllText("emote_test_front.json", PsbDecompiler.Decompile(psb));
        }

        [TestMethod]
        public void TestConvertKrkr2Win()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");

            //var path = Path.Combine(resPath, "澄怜a_裸.psb-pure.psb");
            var path = Path.Combine(resPath, "澄怜a_裸.psb-pure.psb.json");
            //var path = Path.Combine(resPath, "e-mote38_KRKR-pure.psb.json");
            //var path = Path.Combine(resPath, "e-mote38_KRKR-pure.psb");
            PSB psb = PsbCompiler.LoadPsbFromJsonFile(path);
            //PSB psb = new PSB(path);
            psb.SwitchSpec(PsbSpec.win);
            psb.Merge();
            File.WriteAllBytes("emote_krkr2win.psb", psb.Build());
            File.WriteAllText("emote_krkr2win.json", PsbDecompiler.Decompile(psb));
            RL.ConvertToImageFile(psb.Resources.First().Data, "tex-in-psb.png", 4096, 4096,
                PsbImageFormat.png, PsbPixelFormat.LeRGBA8);
        }

        [TestMethod]
        public void TestConvertKrkr2Win2()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "ちえ_横_おしゃれc1.pure.psb");
            PSB psb = new PSB(path);
            psb.SwitchSpec(PsbSpec.win);
            psb.Merge();
            File.WriteAllBytes("emote_krkr2win.psb", psb.Build());
        }

        [TestMethod]
        public void TestConvertCommon2Win()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");

            var path = Path.Combine(resPath, "akira_guide-pure.psb");
            //PSB psb = PsbCompiler.LoadPsbFromJsonFile(path);
            PSB psb = new PSB(path);
            psb.SwitchSpec(PsbSpec.win);
            psb.Merge();
            File.WriteAllBytes("emote_common2win.psb", psb.Build());
        }

        [TestMethod]
        public void TestConvertWin2Common()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");

            var path = Path.Combine(resPath, "ca01_l_body_1.psz.psb-pure.psb");
            //PSB psb = PsbCompiler.LoadPsbFromJsonFile(path);
            PSB psb = new PSB(path);
            psb.SwitchSpec(PsbSpec.common);
            psb.Merge();
            File.WriteAllBytes("emote_win2common.psb", psb.Build());
        }

        [TestMethod]
        public void TestConvertWin2Ems()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");

            var path = Path.Combine(resPath, "ca01_s_body_2.psz.psb-pure.psb");
            //PSB psb = PsbCompiler.LoadPsbFromJsonFile(path);
            PSB psb = new PSB(path);
            psb.SwitchSpec(PsbSpec.ems);
            psb.Merge();
            File.WriteAllBytes("emote_win2ems.psb", psb.Build());
        }

        [TestMethod]
        public void TestDecompileMenuPsb()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "title.pimg");
            var json = PsbDecompiler.Decompile(path, out var psb);
        }

        [TestMethod]
        public void TestDecompileDullahan()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "dx_オリヴィ_制服.psb");
            //var path = Path.Combine(resPath, "ap_bup_yu02_頭部.psb");
            //var path = Path.Combine(resPath, "dx_真闇_裸_impure.psb");
            var json = PsbDecompiler.Decompile(path, out var psb);
        }

        [TestMethod]
        public void TestCompileMenuPsb()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");

            var path = Path.Combine(resPath, "title.psb.json");
            PsbCompiler.CompileToFile(path, path + ".psbuild.psb", null, 2);
        }

        [TestMethod]
        public void TestMmioAndDullhanContent()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "00_pro02.txt.scn");
            var path2 = Path.Combine(resPath, "00_pro02.txt.scn.json");
            //var path = Path.Combine(resPath, "akira_guide.psb");
            //var path2 = Path.Combine(resPath, "akira_guide.psb.json");
            //var psb = new PSB(path);
            var psb = PSB.DullahanLoad(path);
            var r = PsbDecompiler.Decompile(psb).Equals(File.ReadAllText(path2));
        }

        [TestMethod]
        public void TestNaN()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            //var path = Path.Combine(resPath, "dx_れいなh1a1.psb");
            //var psb = new PSB(path);
            var path = Path.Combine(resPath, "dx_れいなh1a1.psb.json");
            var psb = PsbCompiler.LoadPsbFromJsonFile(path);
            var o = psb.Objects.FindByPath(
                    "/object/head_parts/motion/頭部変形基礎/layer/[0]/children/[3]/children/[0]/children/[0]/children/[0]/children/[0]/children/[0]/children/[0]/children/[0]/children/[0]/children/[1]/children/[0]/children/[0]/children/[0]/children/[0]/frameList/[0]/content/coord")
                as PsbList;
            var num = o[0] as PsbNumber;
            var val = num.IntValue;
            var valNaN = num.FloatValue;
        }

        [TestMethod]
        public void TestInline()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "2001010400_00_mid.pure.psb");
            var texPath = Path.Combine(resPath, "2001010400_00_mid_tex000.png");
            PSB psb = new PSB(path);
            psb.Link(new List<string> {texPath}, order: PsbLinkOrderBy.Order);
            psb.Merge();
            File.WriteAllBytes("inline.psb", psb.Build());
            PSB p2 = new PSB("inline.psb");
        }

        [TestMethod]
        public void TestPsz()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "ca01_l_body_1.psz");
            PszShell pszShell = new PszShell();
            var context = new Dictionary<string, object>();
            var oriStream = File.OpenRead(path);
            var psbStream = pszShell.ToPsb(oriStream, context);
            var config = context[Consts.Context_PsbZlibFastCompress];
            //context[ZlibCompress.PsbZlibCompressConfig] = (byte) 0x9C;
            var pszStream = pszShell.ToShell(psbStream, context) as MemoryStream;
            File.WriteAllBytes("test.psz", pszStream.ToArray());
        }

        [TestMethod]
        public void TestLz4()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "dx_真闇_裸_lz4_linked.psb");
            Lz4Shell shell = new Lz4Shell();
            var context = new Dictionary<string, object>();
            var oriStream = File.OpenRead(path);
            var psbStream = shell.ToPsb(oriStream, context);
            var psb = new PSB(psbStream);
            var pszStream = shell.ToShell(psb.ToStream(), context) as MemoryStream;
            File.WriteAllBytes("test.lz4", pszStream.ToArray());
        }

        [TestMethod]
        public void TestPsd()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "dx_れいなh1a1.freemote-krkr-pure.win.psb");
            PsdShell shell = new PsdShell();
            var stream = File.OpenRead(path);
            var inShell = shell.IsInShell(stream);
            File.WriteAllBytes("test.psd", shell.ToShell(stream).ToArray());
        }


        [TestMethod]
        public void TestLoadResxJson()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "image_info.psb.m.json");
            var resx = PsbResourceJson.LoadByPsbJsonPath(path);
            Console.WriteLine(resx.Context[Consts.Context_ArchiveItemFileNames]);
            //JObject j = (JObject)resx.Context[Consts.Context_ArchiveItemFileNames];
            //foreach (var kv in j)
            //{
            //    var val = kv.Value.Value<string>();
            //    if (string.IsNullOrEmpty(val))
            //    {
            //        Console.WriteLine("<null>");
            //    }
            //    else
            //    {
            //        Console.WriteLine(val);
            //    }
            //}
        }

        [TestMethod]
        public void TestCompareDecompile()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");

            var pccPsb = new PSB(Path.Combine(resPath, "c01c.txt.scn"));
            //var pccPsb = new PSB(Path.Combine(resPath, "ca01_l_body_1.psz.psb-pure.psb"));
            //var pccPsb = new PSB(Path.Combine(resPath, "ca01.psb"));

            //var psbuildPsb = new PSB(Path.Combine(resPath, "ca01_l_body_1.psz.psb-pure.psb.json.psbuild.psb"));
            //var psbuildPsb = new PSB(Path.Combine(resPath, "dx_れいなh1a1.psb.json-pure.psb"));
            var psbuildPsb = PsbCompiler.LoadPsbFromJsonFile(Path.Combine(resPath, "c01c.txt.json"));
            //foreach (var s in psbuildPsb.Strings)
            //{
            //    var pccStr = pccPsb.Strings.Find(ss => ss.Value == s.Value);
            //    if (pccStr != null)
            //    {
            //        s.Index = pccStr.Index;
            //    }
            //    else
            //    {
            //        Console.WriteLine($"Can not find: {s}");
            //    }
            //}

            //psbuildPsb.UpdateIndexes();
            //File.WriteAllBytes(Path.Combine(resPath, "ca01_build.psb"), psbuildPsb.Build());

            CompareValue(pccPsb.Objects, psbuildPsb.Objects);
            //Console.WriteLine("============");
            //CompareValue(psbuildPsb.Objects, pccPsb.Objects);
        }

        [TestMethod]
        public void TestGraft2()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var pathGood = Path.Combine(resPath, "goodstr.freemote.psb");
            var pathBad = Path.Combine(resPath, "goodStr.psb");

            var psbGood = new PSB(pathGood);
            var psbBad = new PSB(pathBad);

            dynamic texGood = (PsbDictionary) psbGood.Objects["source"].Children("tex");
            dynamic texBad = (PsbDictionary) psbBad.Objects["source"].Children("tex#000");
            var badIcon = texBad["icon"];

            PsbDictionary newIcon = new PsbDictionary();
            foreach (var part in texGood["icon"])
            {
                var content = part.Value;
                var bi = ((PsbDictionary) badIcon).FirstOrDefault(i =>
                    ((PsbNumber) i.Value.Children("width")).AsInt == content["width"].AsInt &&
                    ((PsbNumber) i.Value.Children("height")).AsInt == content["height"].AsInt &&
                    ((PsbNumber) i.Value.Children("originX")).AsFloat == content["originX"].AsFloat &&
                    ((PsbNumber) i.Value.Children("originY")).AsFloat == content["originY"].AsFloat);

                if (bi.Key != null)
                {
                    newIcon[bi.Key] = content;
                }
            }

            texGood["icon"] = newIcon;

            dynamic badSource = psbBad.Objects["source"];
            badSource["tex#000"] = texGood;

            psbBad.Merge();
            psbBad.BuildToFile("graft.psb");
        }

        [TestMethod]
        public void TestWin2Krkr2Win()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var pathGood = Path.Combine(resPath, "goodstr.freemote.psb");
            var psb = new PSB(pathGood);
            psb.SwitchSpec(PsbSpec.krkr, PsbSpec.krkr.DefaultPixelFormat());
            psb.Merge();
            psb.SwitchSpec(PsbSpec.win, PsbSpec.win.DefaultPixelFormat());
            psb.Merge();
            psb.BuildToFile("convert2.psb");
        }

        [TestMethod]
        public void TestTrie()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            //var path = Path.Combine(resPath, "e-mote38基本テンプレート(正面zバイナリ専用)_free.json");
            var path = Path.Combine(resPath, "test_kazuma.txt.scn.m.json");
            var psb = PsbCompiler.LoadPsbFromJsonFile(path);
            var names = psb.Names;
            var s1 = psb.ToStream().Length;
            Console.WriteLine(s1);

            names.Sort((s1, s2) => s1.Length - s2.Length);
            var s2 = psb.ToStream().Length;
            Console.WriteLine(s2);
        }

        [TestMethod]
        public void TestExtractArchive()
        {
            FreeMount.Init();
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "patch_info.psb.m");
            PsbDecompiler.ExtractArchive(path, "523aad2de7132", FreeMountContext.CreateForArchive(), null, false, false, true);
        }

        [TestMethod]
        public void TestPackArchive()
        {
            FreeMount.Init();
            //Consts.ForceCompressionLevel = CompressionLevel.Fastest;
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "patch_info.psb.m.json");
            //PsbCompiler.PackArchive(path, "523aad2de7132", true, false);
            PsbCompiler.PackArchive(path, "523aad2de7132", false, false);

            PsbDecompiler.ExtractArchive("patch_info.psb.m", "523aad2de7132", FreeMountContext.CreateForArchive(), null, false, false, true);
        }

        public static bool CompareValue(IPsbValue p1, IPsbValue p2)
        {
            //if (p1.Type != p2.Type && !(p1 is PsbString))
            //{
            //    Console.WriteLine($"Strict Type diff: {p1}({p1.Type}) vs {p2}({p2.Type})");
            //}
            if (p1 != null && p2 == null)
            {
                Console.WriteLine($"p1 == {p1.ToString()} && p2 == null");
                return false;
            }

            if (p1 == null && p2 != null)
            {
                Console.WriteLine($"p1 == null && p2 == {p2.ToString()}");
                return false;
            }

            if (p1 == null && p2 == null)
            {
                return true;
            }

            if (p1.GetType() != p2.GetType())
            {
                Console.WriteLine($"Type diff: {p1}({p1.GetType()}) vs {p2}({p2.GetType()})");
                return false;
            }

            switch (p1)
            {
                case PsbResource r1:
                    var r2 = (PsbResource) p2;
                    if (r1.Data.SequenceEqual(r2.Data))
                    {
                        return true;
                    }

                    Console.WriteLine($"Res Diff: {r1} vs {r2}");
                    return false;
                case PsbNull _:
                    return true;
                case PsbNumber n1:
                    var n2 = (PsbNumber) p2;
                    if (n1.Type != n2.Type)
                    {
                        Console.WriteLine($"Wrong Number Type: {n1}({n1.Type}) vs {n2}({n2.Type})");
                        return false;
                    }

                    switch (n1.NumberType)
                    {
                        case PsbNumberType.Int:
                            if ((int) n1 != (int) n2)
                            {
                                Console.WriteLine($"{n1} != {n2}");
                                return false;
                            }

                            break;
                        case PsbNumberType.Float:
                            if (Math.Abs((float) n1 - (float) n2) > float.Epsilon)
                            {
                                Console.WriteLine($"{n1} != {n2}");
                                return false;
                            }

                            break;
                        case PsbNumberType.Double:
                            if (Math.Abs((double) n1 - (double) n2) > double.Epsilon)
                            {
                                Console.WriteLine($"{n1} != {n2}");
                                return false;
                            }

                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    return true;
                case PsbString s1:
                    var s2 = (PsbString) p2;
                    if (s1.Value == s2.Value)
                    {
                        return true;
                    }

                    Console.WriteLine($"{s1} != {s2}");
                    return false;
                case PsbBool b1:
                    var b2 = (PsbBool) p2;
                    if (b1.Value == b2.Value)
                    {
                        return true;
                    }

                    Console.WriteLine($"{b1} != {b2}");
                    return false;
                case PsbArray a1:
                    var a2 = (PsbArray) p2;
                    if (a1.Value.SequenceEqual(a2.Value))
                    {
                        return true;
                    }

                    Console.WriteLine($"{a1} != {a2}");
                    return false;
                case PsbList c1:
                    var c2 = (PsbList) p2;
                    for (var i = 0; i < c1.Count; i++)
                    {
                        if (CompareValue(c1[i], c2[i]))
                        {
                            continue;
                        }

                        Console.WriteLine(c1.Path);
                        //Console.WriteLine($"{c1.Value[i]} != {c2.Value[i]}");
                    }

                    return true;
                case PsbDictionary d1:
                    var d2 = (PsbDictionary) p2;
                    foreach (var pair1 in d1)
                    {
                        if (!d2.ContainsKey(pair1.Key))
                        {
                            Console.WriteLine($"Missing {pair1.Key}");
                        }
                        else
                        {
                            CompareValue(pair1.Value, d2[pair1.Key]);
                        }
                    }

                    return true;
            }

            return true;
        }
    }
}