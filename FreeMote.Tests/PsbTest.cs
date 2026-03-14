using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using FreeMote.Plugins;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FreeMote.Psb;
using System.Runtime.Remoting.Messaging;


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
        public void TestPsbV1()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "psb-v1.psb");
            PSB psb = new PSB(path);
            psb.Header.Version = 1;
            var pathV1 = Path.ChangeExtension(path, "v1.psb");
            psb.BuildToFile(pathV1);
            PSB reload = new PSB(pathV1);
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

            //var mdfShell = new MdfShell();
            //var ms = mdfShell.ToPsb(File.OpenRead(path), ctx.Context);
            //HINT: brute get info-psb key is nearly impossible, don't waste your time on it and just find the key by yourself.
            //You have been warned for (times): 3
            var ms = PsbExtension.EncodeMdf(File.OpenRead(path), "38757621acf82scenario_info.psb.m", 131, true);
            File.WriteAllBytes(path + ".raw", ms.ToArray());
        }

        [TestMethod]
        public void TestMdf()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "e-mote38_free.mmo");
            //var path = Path.Combine(resPath, "e-mote38_free.mmo-repack.mmo");
            var path2 = Path.Combine(resPath, "emote38-pure.mmo");

            MPack.CompressPsbToMdfStream(File.OpenRead(path2)).CopyTo(File.Create(path + "-repack.mmo"));

            using (var mdfStream = File.OpenRead(path))
            {
                using (var psbStream = MPack.MdfDecompressToStream(mdfStream))
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
            var painter = new EmtPainter(psb);
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
            var painter = new EmtPainter(psb);
            var bmp = painter.Draw(4096, 4096);
            bmp.Save("RenderWin.png", ImageFormat.Png);
        }

        [TestMethod]
        public void TestDrawCommon()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "akira_guide-pure.psb");
            var psb = new PSB(path);
            var painter = new EmtPainter(psb);
            var bmp = painter.Draw(2048, 2048);
            bmp.Save("RenderCommon.png", ImageFormat.Png);
        }

        [TestMethod]
        public void TestDrawKrkrMtn()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\Res");
            var path = Path.Combine(resPath, "sd001.mtn");
            var psb = new PSB(path);
            var painter = new MtnPainter(psb);
            var bmps = painter.DrawAll();
            foreach (var bmp in bmps)
            {
                bmp.Image?.Save($"{bmp.Name}.png", ImageFormat.Png);
            }
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
            var results = PsbExtension.ArchiveInfo_GetAllPossibleFileNames("scenario/ca01_06.txt.scn.m", ".psb.m");
            foreach (var result in results)
            {
                Console.WriteLine(result);
            }

            Console.WriteLine();
            results = PsbExtension.ArchiveInfo_GetAllPossibleFileNames("up05_10_03.txt", ".scn.m");
            foreach (var result in results)
            {
                Console.WriteLine(result);
            }
        }

        [TestMethod]
        public void ObjectStat()
        {
            var resPath = Path.Combine(Environment.CurrentDirectory, @"..\..\..\Res");
            var path = Path.Combine(resPath, "test_kazuma.txt.scn.m");

            PsbFile f = new PsbFile(path);
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            br.BaseStream.Seek(f.Header.OffsetEntries, SeekOrigin.Begin);

            Dictionary<PsbObjType, List<IPsbValue>> objects = new Dictionary<PsbObjType, List<IPsbValue>>();
            Unpack(br, false);
        }

        private IPsbValue Unpack(BinaryReader br, bool lazyLoad = false)
        {
#if DEBUG_OBJECT_WRITE
            var pos = br.BaseStream.Position;
            _tw.WriteLine($"{(_last == 0 ? 0 : pos - _last)}");
#endif

            var typeByte = br.ReadByte();
            //There is no need to check this, and it's slow
            //if (!Enum.IsDefined(typeof(PsbObjType), typeByte))
            //{
            //    return null;
            //    //throw new ArgumentOutOfRangeException($"0x{type:X2} is not a known type.");
            //}

            var type = (PsbObjType) typeByte;

#if DEBUG_OBJECT_WRITE
            _tw.Write($"{type}\t{pos}\t");
            _tw.Flush();
            _last = pos;
#endif

            switch (type)
            {
                case PsbObjType.None:
                    return null;
                case PsbObjType.Null:
                    return PsbNull.Null;
                case PsbObjType.False:
                case PsbObjType.True:
                    return new PsbBool(type == PsbObjType.True);
                case PsbObjType.NumberN0:
                    return PsbNumber.Zero; //PsbNumber is not comparable!
                case PsbObjType.NumberN1:
                case PsbObjType.NumberN2:
                case PsbObjType.NumberN3:
                case PsbObjType.NumberN4:
                case PsbObjType.NumberN5:
                case PsbObjType.NumberN6:
                case PsbObjType.NumberN7:
                case PsbObjType.NumberN8:
                case PsbObjType.Float0:
                case PsbObjType.Float:
                case PsbObjType.Double:
                    return new PsbNumber(type, br);
                case PsbObjType.ArrayN1:
                case PsbObjType.ArrayN2:
                case PsbObjType.ArrayN3:
                case PsbObjType.ArrayN4:
                case PsbObjType.ArrayN5:
                case PsbObjType.ArrayN6:
                case PsbObjType.ArrayN7:
                case PsbObjType.ArrayN8:
                    return new PsbArray(typeByte - (byte) PsbObjType.ArrayN1 + 1, br);
                case PsbObjType.StringN1:
                case PsbObjType.StringN2:
                case PsbObjType.StringN3:
                case PsbObjType.StringN4:
                    var str = new PsbString(typeByte - (byte) PsbObjType.StringN1 + 1, br);
                    return str;
                case PsbObjType.ResourceN1:
                case PsbObjType.ResourceN2:
                case PsbObjType.ResourceN3:
                case PsbObjType.ResourceN4:
                case PsbObjType.ExtraChunkN1:
                case PsbObjType.ExtraChunkN2:
                case PsbObjType.ExtraChunkN3:
                case PsbObjType.ExtraChunkN4:
                    bool isExtra = type >= PsbObjType.ExtraChunkN1;
                    var res =
                        new PsbResource(typeByte - (byte) (isExtra ? PsbObjType.ExtraChunkN1 : PsbObjType.ResourceN1) + 1, br)
                        { IsExtra = isExtra };
                   return res;
                case PsbObjType.List:
                    return LoadList(br, lazyLoad);
                case PsbObjType.Objects:
                    return LoadObjects(br, lazyLoad);
                //Compiler used
                case PsbObjType.Integer:
                case PsbObjType.String:
                case PsbObjType.Resource:
                case PsbObjType.Decimal:
                case PsbObjType.Array:
                case PsbObjType.Boolean:
                case PsbObjType.BTree:
                    Debug.WriteLine("FreeMote won't need these for compile.");
                    break;
                default:
                    Debug.WriteLine($"Found unknown type {type}. Please provide the PSB for research.");
                    return null;
            }

            return null;
        }

        private PsbList LoadList(BinaryReader br, bool lazyLoad = false)
        {
            var offsets = PsbArray.LoadIntoList(br.ReadByte() - (byte) PsbObjType.ArrayN1 + 1, br);
            var duplicates = offsets.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();

            var pos = br.BaseStream.Position;
            PsbList list = new PsbList(offsets.Count);
            uint? maxOffset = null;
            var endPos = pos;
            if (lazyLoad && offsets.Count > 0)
            {
                maxOffset = offsets.Max();
            }

            for (int i = 0; i < offsets.Count; i++)
            {
                var offset = offsets[i];
                br.BaseStream.Seek(pos + offset, SeekOrigin.Begin);
                var obj = Unpack(br, lazyLoad);
                if (obj != null)
                {
                    if (duplicates.Contains(offset))
                    {
                        Console.WriteLine($"Reuse {obj.Type} ({offset}) in PsbList");
                    }
                    if (obj is IPsbChild c)
                    {
                        c.Parent = list;
                    }

                    if (obj is IPsbSingleton s)
                    {
                        s.Parents.Add(list);
                    }

                    list.Add(obj);
                }

                if (lazyLoad && offset == maxOffset)
                {
                    endPos = br.BaseStream.Position;
                }
            }

            if (lazyLoad)
            {
                br.BaseStream.Position = endPos;
            }

            return list;
        }

        private PsbDictionary LoadObjects(BinaryReader br, bool lazyLoad = false)
        {
            var names = PsbArray.LoadIntoList(br.ReadByte() - (byte) PsbObjType.ArrayN1 + 1, br);
            var offsets = PsbArray.LoadIntoList(br.ReadByte() - (byte) PsbObjType.ArrayN1 + 1, br);
            //find elements which appears more than once in offsets
            var duplicates = offsets.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();

            var pos = br.BaseStream.Position;
            PsbDictionary dictionary = new PsbDictionary(names.Count);
            uint? maxOffset = null;
            var endPos = pos;
            if (lazyLoad && offsets.Count > 0)
            {
                maxOffset = offsets.Max();
            }

            for (int i = 0; i < names.Count; i++)
            {
                //br.BaseStream.Seek(pos, SeekOrigin.Begin);
                var nameIdx = (int) names[i];
                var name = nameIdx.ToString();
                IPsbValue obj = null;
                uint offset = 0;
                if (i < offsets.Count)
                {
                    offset = offsets[i];
                    br.BaseStream.Seek(pos + offset, SeekOrigin.Begin);
                    //br.BaseStream.Seek(offset, SeekOrigin.Current);
                    obj = Unpack(br, lazyLoad);
                    if (duplicates.Contains(offset))
                    {
                        Console.WriteLine($"Reuse {obj.Type} ({offset}) in PsbDic");
                    }
                }
                else
                {
                    Logger.LogWarn($"[WARN] Bad PSB format: at position:{pos}, offset index {i} >= offsets count ({offsets.Count}), skipping.");
                }

                if (obj != null)
                {
                    if (obj is IPsbChild c)
                    {
                        c.Parent = dictionary;
                    }

                    if (obj is IPsbSingleton s)
                    {
                        s.Parents.Add(dictionary);
                    }

                    dictionary.Add(name, obj);
                }

                if (lazyLoad && offset == maxOffset)
                {
                    endPos = br.BaseStream.Position;
                }
            }

            if (lazyLoad)
            {
                br.BaseStream.Position = endPos;
            }

            return dictionary;
        }
    }
}