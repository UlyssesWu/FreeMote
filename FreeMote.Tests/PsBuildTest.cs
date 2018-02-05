using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using FreeMote.Psb;
using FreeMote.Psb.Textures;
using FreeMote.PsBuild;
using FreeMote.PsBuild.Converters;
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
            var path = Path.Combine(resPath, "澄怜a_裸.psb-pure.psb.json");
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
            var tree = BTree.Build(psb.Names, out List<uint> oNames, out List<uint> oTrees, out List<uint> oOffsets);
            var list = BTree.Load(oNames, oTrees, oOffsets);
            Assert.IsTrue(psb.Names.SequenceEqual(list));
        }

        [TestMethod]
        public void TestJsonNumbers()
        {
            List<float> floats = new List<float> { -0.00000001f, 1 / 3f, -0.000027f, 19.200079f, (float)Math.PI, float.MinValue };
            string json = JsonConvert.SerializeObject(floats);
            Console.WriteLine(json);
            var result = JsonConvert.DeserializeObject<List<float>>(json);
            for (int i = 0; i < result.Count; i++)
            {
                Assert.AreEqual(floats[i], result[i]);
            }

            List<double> doubles = new List<double> { double.MinValue, double.MaxValue, 123456789.0, -0.00000001, 0.03, 0.4 };
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
            var resKrkr = psbKrkr.CollectResources(false);
            var resWin = psbWin.CollectResources(false);
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
            Dictionary<string,Image> imgs = new Dictionary<string, Image>();
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
            var obj = (PsbDictionary)psb.Objects.FindByPath(targetPath);
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
            RL.ConvertToImageFile(psb.Resources.First().Data, "tex-in-psb.png", 4096,4096, PsbImageFormat.Png, PsbPixelFormat.WinRGBA8); 
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
        public void TestCompileMenuPsb()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");

            var path = Path.Combine(resPath, "title.psb.json");
            PsbCompiler.CompileToFile(path, path + ".psbuild.psb", null, 2);
        }

        [TestMethod]
        public void TestCompareDecompile()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");

            var pccPsb = new PSB(Path.Combine(resPath, "e-mote38_win-pure.psb"));
            //var pccPsb = new PSB(Path.Combine(resPath, "ca01_l_body_1.psz.psb-pure.psb"));
            //var pccPsb = new PSB(Path.Combine(resPath, "ca01.psb"));

            //var psbuildPsb = new PSB(Path.Combine(resPath, "ca01_l_body_1.psz.psb-pure.psb.json.psbuild.psb"));
            var psbuildPsb = new PSB(Path.Combine(resPath, "e-mote38_KRKR-pure.psb.json.psbuild.psb"));

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

            bool CompareValue(IPsbValue p1, IPsbValue p2)
            {
                //if (p1.Type != p2.Type && !(p1 is PsbString))
                //{
                //    Console.WriteLine($"Strict Type diff: {p1}({p1.Type}) vs {p2}({p2.Type})");
                //}
                if (p1.GetType() != p2.GetType())
                {
                    Console.WriteLine($"Type diff: {p1} vs {p2}");
                    return false;
                }
                switch (p1)
                {
                    case PsbResource r1:
                        var r2 = (PsbResource)p2;
                        if (r1.Data.SequenceEqual(r2.Data))
                        {
                            return true;
                        }
                        Console.WriteLine($"Res Diff: {r1} vs {r2}");
                        return false;
                    case PsbNull _:
                        return true;
                    case PsbNumber n1:
                        var n2 = (PsbNumber)p2;
                        if (n1.Type != n2.Type)
                        {
                            Console.WriteLine($"Wrong Number Type: {n1}({n1.Type}) vs {n2}({n2.Type})");
                            return false;
                        }
                        switch (n1.NumberType)
                        {
                            case PsbNumberType.Int:
                                if ((int)n1 != (int)n2)
                                {
                                    Console.WriteLine($"{n1} != {n2}");
                                    return false;
                                }
                                break;
                            case PsbNumberType.Float:
                                if (Math.Abs((float)n1 - (float)n2) > float.Epsilon)
                                {
                                    Console.WriteLine($"{n1} != {n2}");
                                    return false;
                                }
                                break;
                            case PsbNumberType.Double:
                                if (Math.Abs((double)n1 - (double)n2) > double.Epsilon)
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
                        var s2 = (PsbString)p2;
                        if (s1.Value == s2.Value)
                        {
                            return true;
                        }
                        Console.WriteLine($"{s1} != {s2}");
                        return false;
                    case PsbBool b1:
                        var b2 = (PsbBool)p2;
                        if (b1.Value == b2.Value)
                        {
                            return true;
                        }
                        Console.WriteLine($"{b1} != {b2}");
                        return false;
                    case PsbArray a1:
                        var a2 = (PsbArray)p2;
                        if (a1.Value.SequenceEqual(a2.Value))
                        {
                            return true;
                        }
                        Console.WriteLine($"{a1} != {a2}");
                        return false;
                    case PsbCollection c1:
                        var c2 = (PsbCollection)p2;
                        for (var i = 0; i < c1.Count; i++)
                        {
                            if (CompareValue(c1[i], c2[i]))
                            {
                                continue;
                            }
                            //Console.WriteLine($"{c1.Value[i]} != {c2.Value[i]}");
                        }
                        return true;
                    case PsbDictionary d1:
                        var d2 = (PsbDictionary)p2;
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
}
