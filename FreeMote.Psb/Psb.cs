﻿//Some PSB format code is based on psbfile by number201724. LICENSE: MIT
//#define DEBUG_OBJECT_WRITE //Enable if you want to check how much bytes each object costs.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using FreeMote.Plugins;
using FreeMote.Psb.Types;

// ReSharper disable InconsistentNaming

namespace FreeMote.Psb
{
    /// <summary>
    /// Packaged Struct Binary
    /// </summary>
    /// Photo Shop Big
    /// Pretty SB
    public partial class PSB
    {
        /// <summary>
        /// Header
        /// </summary>
        internal PsbHeader Header { get; set; }

        public Encoding Encoding { get; set; } = Encoding.UTF8;

        private PsbArray Charset;
        private PsbArray NamesData;
        private PsbArray NameIndexes;
        /// <summary>
        /// Names
        /// </summary>
        public List<string> Names { get; internal set; }

        private PsbArray StringOffsets;
        /// <summary>
        /// Strings
        /// </summary>
        public List<PsbString> Strings { get; set; }

        internal PsbArray ChunkOffsets;
        internal PsbArray ChunkLengths;
        /// <summary>
        /// Resource Chunk
        /// </summary>
        public List<PsbResource> Resources { get; internal set; }

        /// <summary>
        /// Extra Resource Chunk
        /// </summary>
        internal PsbArray ExtraChunkOffsets = null;
        internal PsbArray ExtraChunkLengths = null;

        /// <summary>
        /// Extra Resources
        /// </summary>
        public List<PsbResource> ExtraResources { get; internal set; }

        /// <summary>
        /// Objects (Entries)
        /// </summary>
        public PsbDictionary Objects
        {
            get => Root as PsbDictionary;
            set => Root = value;
        }

        public IPsbValue Root { get; set; }

        /// <summary>
        /// Type specific handler
        /// </summary>
        public IPsbType TypeHandler
        {
            get
            {
                if (TypeHandlers.ContainsKey(Type))
                {
                    return TypeHandlers[Type];
                }

                return new MotionType();
            }
        }

        /// <summary>
        /// Type
        /// </summary>
        public PsbType Type { get; set; } = PsbType.PSB;

        /// <summary>
        /// PSB Target Platform (Spec)
        /// </summary>
        public PsbSpec Platform
        {
            get
            {
                var spec = Objects?["spec"]?.ToString();
                if (string.IsNullOrEmpty(spec))
                {
                    return PsbSpec.none;
                }

                return Enum.TryParse(spec, out PsbSpec p) ? p : PsbSpec.other;
            }
            set
            {
                if (Objects != null)
                {
                    Objects["spec"] = value.ToString().ToPsbString();
                }
            }
        }

        internal string FilePath { get; private set; }

        public string TypeId
        {
            get
            {
                var id = Objects?["id"]?.ToString();
                if (string.IsNullOrEmpty(id))
                {
                    return "";
                }

                return id;
            }
            set
            {
                if (Objects == null)
                {
                    return;
                }

                Objects["id"] = value.ToPsbString();
            }
        }

        public PSB(ushort version = 3)
        {
            Header = new PsbHeader { Version = version };
        }

        public PSB(string path, Encoding encoding = null)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("File not exists.", path);
            }

            if (encoding != null)
            {
                Encoding = encoding;
            }

            FilePath = path;

#if DEBUG_OBJECT_WRITE
            _tw = new StreamWriter(path + ".debug");
#endif
            using (var fs = new FileStream(path, FileMode.Open))
            {
                try
                {
                    LoadFromStream(fs);
                }
                catch (PsbBadFormatException e)
                {
                    if (e.Reason == PsbBadFormatReason.Header || e.Reason == PsbBadFormatReason.Array)
                    {
                        fs.Seek(0, SeekOrigin.Begin);
                        LoadFromDullahan(fs);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        public PSB(Stream stream, bool tryDullahanLoading = true, Encoding encoding = null)
        {
            if (encoding != null)
            {
                Encoding = encoding;
            }
            try
            {
                LoadFromStream(stream);
            }
            catch (PsbBadFormatException e)
                when (tryDullahanLoading &&
                      (e.Reason == PsbBadFormatReason.Header || e.Reason == PsbBadFormatReason.Array))
            {
                stream.Seek(0, SeekOrigin.Begin);
                Header = new PsbHeader { Version = 3 };
                LoadFromDullahan(stream);
            }
        }

        /// <summary>
        /// Infer PSB Type
        /// </summary>
        /// <returns></returns>
        public PsbType InferType()
        {
            foreach (var handler in TypeHandlers)
            {
                if (handler.Value.IsThisType(this))
                {
                    Type = handler.Key;
                    return Type;
                }
            }

            foreach (var handler in FreeMount._.SpecialTypes)
            {
                if (handler.Value.IsThisType(this))
                {
                    TypeId = handler.Key;
                    Type = PsbType.PSB;
                    return PsbType.PSB;
                }
            }

            return PsbType.PSB;
        }

#if DEBUG_OBJECT_WRITE
        TextWriter _tw;
        private long _last = 0;
#endif

        internal void LoadFromStream(Stream stream)
        {
            var sig = new byte[4];
            stream.Read(sig, 0, 4);
            if (Encoding.ASCII.GetString(sig).ToUpperInvariant().StartsWith("MDF"))
            {
                stream.Seek(6, SeekOrigin.Current); //Original Length (4 bytes) | Compression Header (78 9C||DA)
                stream = ZlibCompress.DecompressToStream(stream);
            }
            else
            {
                stream.Seek(-4, SeekOrigin.Current);
            }

            BinaryReader sourceBr = new BinaryReader(stream, Encoding);
            BinaryReader br = sourceBr;

            //Load Header
            Header = PsbHeader.Load(br);
            if (Header.IsHeaderEncrypted)
            {
                throw new PsbBadFormatException(PsbBadFormatReason.Header);
            }

            //Switch MemoryMapped IO
            bool memoryPreload = Consts.InMemoryLoading && !(stream is MemoryStream);
            if (memoryPreload)
            {
                sourceBr.BaseStream.Position = 0;
                br = new BinaryReader(new MemoryStream(sourceBr.ReadBytes((int)Header.OffsetChunkData)), Encoding);
            }

            //Pre Load Strings
            br.BaseStream.Seek(Header.OffsetStrings, SeekOrigin.Begin);
            StringOffsets = new PsbArray(br.ReadByte() - (byte)PsbObjType.ArrayN1 + 1, br);
            Strings = new List<PsbString>();

            //Load Names
            if (Header.Version == 1)
            {
                br.BaseStream.Seek(Header.HeaderLength, SeekOrigin.Begin);
                NameIndexes = new PsbArray(br.ReadByte() - (byte) PsbObjType.ArrayN1 + 1, br);
                LoadKeys(br);
            }
            else
            {
                br.BaseStream.Seek(Header.OffsetNames, SeekOrigin.Begin);
                Charset = new PsbArray(br.ReadByte() - (byte) PsbObjType.ArrayN1 + 1, br);
                NamesData = new PsbArray(br.ReadByte() - (byte) PsbObjType.ArrayN1 + 1, br);
                NameIndexes = new PsbArray(br.ReadByte() - (byte) PsbObjType.ArrayN1 + 1, br);
                LoadNames();
            }
            
            //Pre Load Resources (Chunks)
            br.BaseStream.Seek(Header.OffsetChunkOffsets, SeekOrigin.Begin);
            ChunkOffsets = new PsbArray(br.ReadByte() - (byte)PsbObjType.ArrayN1 + 1, br);
            br.BaseStream.Seek(Header.OffsetChunkLengths, SeekOrigin.Begin);
            ChunkLengths = new PsbArray(br.ReadByte() - (byte)PsbObjType.ArrayN1 + 1, br);
            Resources = new List<PsbResource>(ChunkLengths.Value.Count);

            if (Header.Version >= 4)
            {
                //Pre Load Extra Resources (Chunks)
                br.BaseStream.Seek(Header.OffsetExtraChunkOffsets, SeekOrigin.Begin);
                ExtraChunkOffsets = new PsbArray(br.ReadByte() - (byte)PsbObjType.ArrayN1 + 1, br);
                br.BaseStream.Seek(Header.OffsetExtraChunkLengths, SeekOrigin.Begin);
                ExtraChunkLengths = new PsbArray(br.ReadByte() - (byte)PsbObjType.ArrayN1 + 1, br);
                ExtraResources = new List<PsbResource>(ExtraChunkLengths.Value.Count);
            }
            else
            {
                ExtraResources = new List<PsbResource>(0);
                ExtraChunkOffsets = new PsbArray();
                ExtraChunkLengths = new PsbArray();
            }

            //Load Entries
            br.BaseStream.Seek(Header.OffsetEntries, SeekOrigin.Begin);
            IPsbValue obj;

#if !DEBUG
            try
#endif
            {
                obj = Unpack(br);
                if (obj == null)
                {
                    throw new PsbBadFormatException(PsbBadFormatReason.Objects, "Can not parse objects");
                }

                Root = obj;
                //Objects = obj as PsbDictionary ??
                //          throw new PsbBadFormatException(PsbBadFormatReason.Objects,
                //              "Wrong offset when parsing objects");
            }
#if !DEBUG
            catch (Exception e)
            {
                Debug.WriteLine(e);
                throw;
            }
#endif
            if (memoryPreload)
            {
                br.Close();
                br.Dispose();
            }

            //FIXED: must load with source BR!
            //Load Unknown
            //if (Header.Version >= 4)
            //{
            //    try
            //    {
            //        LoadUnknown(sourceBr);
            //    }
            //    catch
            //    {
            //        // ignored
            //    }
            //}

            //Load Resource
            foreach (var res in Resources)
            {
                LoadResource(res, sourceBr);
            }

            if (Header.Version >= 4)
            {
                foreach (var res in ExtraResources)
                {
                    LoadExtraResource(res, sourceBr);
                }
            }

            AfterLoad();
        }

        /// <summary>
        /// Tasks after load: sort and type infer
        /// </summary>
        private void AfterLoad()
        {
            Strings.Sort((r1, r2) => (int)((r1.Index ?? int.MaxValue) - (r2.Index ?? int.MaxValue)));
            Resources.Sort((r1, r2) => (int)((r1.Index ?? int.MaxValue) - (r2.Index ?? int.MaxValue)));
            ExtraResources.Sort((r1, r2) => (int)((r1.Index ?? int.MaxValue) - (r2.Index ?? int.MaxValue)));
            InferType();
        }

        //private void LoadUnknown(BinaryReader br)
        //{
        //    br.BaseStream.Seek(Header.OffsetExtraChunkOffsets, SeekOrigin.Begin);
        //    ExtraChunkOffsets = new PsbArray(br.ReadByte() - (byte) PsbObjType.ArrayN1 + 1, br);
        //    br.BaseStream.Seek(Header.OffsetExtraChunkLengths, SeekOrigin.Begin);
        //    ExtraChunkLengths = new PsbArray(br.ReadByte() - (byte) PsbObjType.ArrayN1 + 1, br);
        //    if (ExtraChunkOffsets.Value.Count > 0)
        //    {
        //        UnknownData.Clear();
        //        for (var i = 0; i < ExtraChunkOffsets.Value.Count; i++)
        //        {
        //            var offset = ExtraChunkOffsets[i];
        //            var length = ExtraChunkLengths[i];
        //            br.BaseStream.Seek(Header.OffsetExtraChunkData + offset, SeekOrigin.Begin);
        //            UnknownData.Add(br.ReadBytes((int) length));
        //        }
        //    }
        //}

        /// <summary>
        /// Load names from Trie form
        /// </summary>
        private void LoadNames()
        {
            Names = new List<string>(NameIndexes.Value.Count);
            for (int i = 0; i < NameIndexes.Value.Count; i++)
            {
                var list = new List<byte>();
                var index = NameIndexes[i];
                var chr = NamesData[(int) index];
                while (chr != 0)
                {
                    var code = NamesData[(int) chr];
                    var d = Charset[(int) code];
                    var realChr = chr - d;
                    //Debug.Write(realChr.ToString("X2") + " ");
                    chr = code;
                    //REF: https://stackoverflow.com/questions/18587267/does-list-insert-have-any-performance-penalty
                    list.Add((byte) realChr);
                }

                //Debug.WriteLine("");
                list.Reverse();
                var str = Encoding.UTF8.GetString(list.ToArray()); //That's why we don't use StringBuilder here.
                Names.Add(str);
            }
        }

        /// <summary>
        /// <see cref="LoadNames"/> for PSBv1
        /// </summary>
        /// <param name="br"></param>
        private void LoadKeys(BinaryReader br)
        {
            Names = new List<string>(NameIndexes.Value.Count);
            for (int i = 0; i < NameIndexes.Value.Count; i++)
            {
                br.BaseStream.Seek(Header.OffsetNames + NameIndexes[i], SeekOrigin.Begin);
                var strValue = br.ReadStringZeroTrim(Encoding);
                Names.Add(strValue);
            }
        }

        /// <summary>
        /// Unpack PSB Value
        /// </summary>
        /// <param name="br"></param>
        /// <param name="lazyLoad">for zero-knowledge reading</param>
        /// <returns></returns>
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

            var type = (PsbObjType)typeByte;

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
                    return new PsbArray(typeByte - (byte)PsbObjType.ArrayN1 + 1, br);
                case PsbObjType.StringN1:
                case PsbObjType.StringN2:
                case PsbObjType.StringN3:
                case PsbObjType.StringN4:
                    var str = new PsbString(typeByte - (byte)PsbObjType.StringN1 + 1, br);
                    if (lazyLoad)
                    {
                        var foundStr = Strings.Find(s => s.Index != null && s.Index == str.Index);
                        if (foundStr == null)
                        {
                            Strings.Add(str);
                        }
                        else
                        {
                            str = foundStr;
                        }
                    }
                    else
                    {
                        LoadString(ref str, br);
                    }

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
                    var resList = isExtra ? ExtraResources : Resources;
                    var res =
                        new PsbResource(typeByte - (byte)(isExtra ? PsbObjType.ExtraChunkN1 : PsbObjType.ResourceN1) + 1, br)
                        { IsExtra = isExtra };
                    //LoadResource(ref res, br); //No longer load Resources here
                    var foundRes = resList.Find(r => r.Index == res.Index);
                    if (foundRes == null)
                    {
                        resList.Add(res);
                    }
                    else
                    {
                        res = foundRes;
                    }

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

        /// <summary>
        /// Load a dictionary, won't ensure stream Position unless use <paramref name="lazyLoad"/>
        /// </summary>
        /// <param name="br"></param>
        /// <param name="lazyLoad">whether to lift stream Position to dictionary end</param>
        /// <returns></returns>
        private PsbDictionary LoadObjects(BinaryReader br, bool lazyLoad = false)
        {
            var names = PsbArray.LoadIntoList(br.ReadByte() - (byte)PsbObjType.ArrayN1 + 1, br);
            var offsets = PsbArray.LoadIntoList(br.ReadByte() - (byte)PsbObjType.ArrayN1 + 1, br);
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
                if (nameIdx >= Names.Count)
                {
                    Logger.LogWarn($"[WARN] Bad PSB format: at position:{pos}, name index {nameIdx} >= Names count ({Names.Count}), skipping.");
                    continue;
                }
                var name = Names[nameIdx];
                IPsbValue obj = null;
                uint offset = 0;
                if (i < offsets.Count)
                {
                    offset = offsets[i];
                    br.BaseStream.Seek(pos + offset, SeekOrigin.Begin);
                    //br.BaseStream.Seek(offset, SeekOrigin.Current);
                    obj = Unpack(br, lazyLoad);
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

        /// <summary>
        /// Load a list, won't ensure stream Position unless use <paramref name="lazyLoad"/>
        /// </summary>
        /// <param name="br"></param>
        /// <param name="lazyLoad">whether to lift stream Position</param>
        /// <returns></returns>
        private PsbList LoadList(BinaryReader br, bool lazyLoad = false)
        {
            var offsets = PsbArray.LoadIntoList(br.ReadByte() - (byte)PsbObjType.ArrayN1 + 1, br);
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

        /// <summary>
        /// Load a resource content based on index, lift stream Position
        /// </summary>
        /// <param name="res"></param>
        /// <param name="br"></param>
        private void LoadResource(PsbResource res, BinaryReader br)
        {
            if (res.Index == null)
            {
                throw new IndexOutOfRangeException("Resource Index invalid");
            }

            var offset = ChunkOffsets[(int)res.Index];
            var length = ChunkLengths[(int)res.Index];
            br.BaseStream.Seek(Header.OffsetChunkData + offset, SeekOrigin.Begin);
            res.Data = br.ReadBytes((int)length);
        }

        /// <summary>
        /// Load an extra resource content based on index, lift stream Position
        /// </summary>
        /// <param name="res"></param>
        /// <param name="br"></param>
        private void LoadExtraResource(PsbResource res, BinaryReader br)
        {
            if (res.Index == null)
            {
                throw new IndexOutOfRangeException("Resource Index invalid");
            }

            var offset = ExtraChunkOffsets[(int)res.Index];
            var length = ExtraChunkLengths[(int)res.Index];
            br.BaseStream.Seek(Header.OffsetExtraChunkData + offset, SeekOrigin.Begin);
            res.Data = br.ReadBytes((int)length);
        }

        /// <summary>
        /// Load a string based on index, lift stream Position
        /// </summary>
        /// <param name="str"></param>
        /// <param name="br"></param>
        private void LoadString(ref PsbString str, BinaryReader br)
        {
            //var pos = br.BaseStream.Position;
            Debug.Assert(str.Index != null, "Index can not be null");
            var idx = str.Index.Value;
            PsbString refStr = Strings.Find(s => s.Index == idx);
            if (refStr != null && Consts.FastMode)
            {
                str = refStr;
                return;
            }

            br.BaseStream.Seek(Header.OffsetStringsData + StringOffsets[(int)idx], SeekOrigin.Begin);
            var strValue = br.ReadStringZeroTrim(Encoding);

            if (refStr != null && strValue == refStr.Value) //Strict value equal check
            {
                str = refStr;
                return;
            }

            if (refStr != null)
            {
                Debug.WriteLine($"{refStr} does not match {strValue}");
            }

            str.Value = strValue;
            Strings.Add(str); //FIXED
        }

        /// <summary>
        /// Fill fields based on <see cref="Objects"/>
        /// </summary>
        /// <param name="mergeString"></param>
        /// <param name="mergeRes">Whether to merge resources with exact same data. Be careful!</param>
        internal void Collect(bool mergeString = false, bool mergeRes = false)
        {
            //https://stackoverflow.com/questions/1427147/sortedlist-sorteddictionary-and-dictionary
            Resources = new List<PsbResource>();
            ExtraResources = new List<PsbResource>();
            var namesSet = new HashSet<string>(); //Keep names unique, HashSet is faster than List
            //Strings can be unique to save space, but can also be redundant for some reasons like translation.
            //We suggest users handle redundant strings before Merge, or in Json, or rewrite their own Merge. That's why PSB.Merge is not directly called in PSB.Build.
            var stringsDic = new Dictionary<string, PsbString>();
            var stringsIndexDic = new Dictionary<uint, PsbString>();
            uint strIdx = 0;
            TravelCollect(Objects);

            Names = new List<string>(namesSet);
            Names.Sort(string.CompareOrdinal); //FIXED: Compared by bytes
            Strings = new List<PsbString>(stringsDic.Values);

            uint NextIndex(Dictionary<uint, PsbString> dic, ref uint idx)
            {
                while (dic.ContainsKey(idx))
                {
                    idx = unchecked(idx + 1u);
                }

                return idx;
            }

            IPsbValue TravelCollect(IPsbValue obj)
            {
                switch (obj)
                {
                    case PsbResource r:
                        var resList = r.IsExtra ? ExtraResources : Resources;
                        if (!mergeRes)
                        {
                            //Still have to merge by index, otherwise you will have mismatched resource reference in objects!
                            if (r.Index != null)
                            {
                                var sameDataRes = resList.Find(res => res.Index != null && res.Index == r.Index);
                                if (sameDataRes == null)
                                {
                                    resList.Add(r);
                                }
                                else
                                {
                                    //r.Index = sameDataRes.Index; 
                                    return sameDataRes; //merge resource correctly!
                                }
                            }
                            else
                            {
                                resList.Add(r);
                            }
                        }
                        else
                        {
                            var sameDataRes = resList.Find(resource =>
                                resource.Data.ByteArrayEqual(r.Data) && resource.Index != null);
                            if (sameDataRes == null)
                            {
                                resList.Add(r);
                            }
                            else
                            {
                                //r.Index = sameDataRes.Index; //This is bad, because the Index will be discontinuous
                                return sameDataRes; //merge resource correctly!
                            }
                        }
                        break;
                    case PsbString s:
                        if (mergeString)
                        {
                            if (stringsDic.ContainsKey(s.Value))
                            {
                                return stringsDic[s.Value];
                            }
                            else //add new string
                            {
                                if (s.Index == null || stringsIndexDic.ContainsKey(s.Index.Value))
                                {
                                    var newIdx = NextIndex(stringsIndexDic, ref strIdx);
                                    s.Index = newIdx;
                                }

                                stringsIndexDic.Add(s.Index.Value, s);
                                stringsDic.Add(s.Value, s);
                            }
                        }
                        else
                        {
                            if (!stringsDic.ContainsKey(s.Value))
                            {
                                stringsDic.Add(s.Value, s); //Ensure value is unique
                                if (s.Index == null || stringsIndexDic.ContainsKey(s.Index.Value))
                                //However index can be null or conflict
                                {
                                    //at this time we assign a new index
                                    var newIdx = NextIndex(stringsIndexDic, ref strIdx);
                                    s.Index = newIdx;
                                }

                                stringsIndexDic.Add(s.Index.Value, s); //and record it for lookup
                                //Strings.Add(s);
                            }
                            else if (s.Index != stringsDic[s.Value].Index)
                            //if value is same but has different index, should let them point to same object
                            {
                                //s.Index = stringsDic[s.Value].Index; //set index
                                return stringsDic[s.Value];
                            }
                        }

                        break;
                    case PsbList c:
                        for (var i = 0; i < c.Count; i++)
                        {
                            var o = c[i];
                            var result = TravelCollect(o);
                            if (result != null)
                            {
                                c[i] = result;
                            }
                        }

                        break;
                    case PsbDictionary d:

                        foreach (var key in d.Keys.ToList())
                        {
                            if (!namesSet.Contains(key))
                            {
                                namesSet.Add(key);
                                //Does Name appears in String Table? No.
                            }

                            var result = TravelCollect(d[key]);
                            if (result != null)
                            {
                                d[key] = result;
                            }
                        }

                        break;
                }

                return null;
            }
        }

        ///// <summary>
        ///// Combine another PSB (for partial exported PSB). Not actually working
        ///// </summary>
        ///// <param name="other"></param>
        ///// <param name="method"></param>
        //[Obsolete]
        //internal void Combine(PSB other, PsbCombineMethod method = PsbCombineMethod.Default)
        //{
        //    switch (method)
        //    {

        //        case PsbCombineMethod.Objects:
        //            if (Objects["object"] is PsbDictionary dic && other.Objects["object"] is PsbDictionary otherDic)
        //            {
        //                dic.UnionWith(otherDic);
        //            }
        //            break;
        //        case PsbCombineMethod.Default:
        //        case PsbCombineMethod.All:
        //        default:
        //            Objects.UnionWith(other.Objects);
        //            break;
        //    }
        //}

        /// <summary>
        /// Update fields and indexes based on <see cref="Objects"/>
        /// </summary>
        public void Merge(bool mergeString = false)
        {
            Collect(mergeString);
            UpdateIndexes();

            //UniqueString(Objects);
            //[Obsolete]
            void UniqueString(IPsbValue obj)
            {
                switch (obj)
                {
                    case PsbResource _:
                        break;
                    case PsbString s:
                        if (Strings.Contains(s))
                        {
                            s.Index = (uint)Strings.IndexOf(s);
                        }
                        else
                        {
                            //Something is wrong
                            Strings.Add(s);
                            s.Index = (uint)Strings.IndexOf(s);
                        }

                        break;
                    case PsbList c:
                        foreach (var o in c)
                        {
                            UniqueString(o);
                        }

                        break;
                    case PsbDictionary d:
                        foreach (var pair in d)
                        {
                            UniqueString(pair.Value);
                        }

                        break;
                }
            }
        }

        internal void UpdateIndexes()
        {
            Strings.Sort((s1, s2) => (int)((s1.Index ?? int.MaxValue) - (s2.Index ?? int.MaxValue)));
            for (int i = 0; i < Strings.Count; i++)
            {
                Strings[i].Index = (uint)i;
            }

            Resources.Sort((s1, s2) => (int)((s1.Index ?? int.MaxValue) - (s2.Index ?? int.MaxValue)));
            for (int i = 0; i < Resources.Count; i++)
            {
                Resources[i].Index = (uint)i;
            }

            ExtraResources.Sort((s1, s2) => (int)((s1.Index ?? int.MaxValue) - (s2.Index ?? int.MaxValue)));
            for (int i = 0; i < ExtraResources.Count; i++)
            {
                ExtraResources[i].Index = (uint)i;
            }
        }

        /// <summary>
        /// Build PSB
        /// <para>Make sure you have called <see cref="Merge"/> or the output can be invalid.</para>
        /// </summary>
        /// <remarks>Why FreeMote do not call <see cref="Merge"/> by default? Because Merge is a strict string merge method which will merge any redundant string. If you have to keep two same strings from merged into one, you can write your own Merge method.</remarks>
        /// <returns>Binary</returns>
        public byte[] Build()
        {
            var ms = ToStream();
            var bts = ms.ToArray();
            ms.Dispose();
            return bts;
        }

        /// <summary>
        /// Build PSB to file
        /// <para>Make sure you have called <see cref="Merge"/> or the output can be invalid.</para>
        /// </summary>
        /// <param name="path"></param>
        public void BuildToFile(string path) => File.WriteAllBytes(path, Build());

        /// <summary>
        /// Build as <see cref="MemoryStream"/>, make sure you have called <see cref="Merge"/> before.
        /// </summary>
        /// <returns></returns>
        public MemoryStream ToStream()
        {
            /*
             * Header
             * --------------
             * Names (Trie for v2+; Strings-like for v1)
             * --------------
             * Entries
             * --------------
             * Strings
             * --------------
             * Resources (ExtraResources first for v4)
             * --------------
             */
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms, Encoding);
            bw.Pad((int)Header.GetHeaderLength());
            Header.HeaderLength = Header.GetHeaderLength();

            #region Compile Names

            if (Header.Version == 1)
            {
                using var keyMs = new MemoryStream();
                var offsets = new List<uint>(Names.Count);
                BinaryWriter nameBw = new BinaryWriter(keyMs, Encoding);
                //Collect Strings
                for (var i = 0; i < Names.Count; i++)
                {
                    var name = Names[i];
                    offsets.Add((uint) nameBw.BaseStream.Position);
                    nameBw.WriteStringZeroTrim(name);
                }

                nameBw.Flush();
                //Mark Offset Strings
                Header.HeaderLength = (uint) bw.BaseStream.Position;
                NameIndexes = new PsbArray(offsets);
                NameIndexes.WriteTo(bw);
                Header.OffsetNames = (uint) bw.BaseStream.Position;
                keyMs.WriteTo(bw.BaseStream);
            }
            else
            {
                //Compile Names
                PrefixTree.Build(Names, out var tNames, out var trie, out var tOffsets);
                //Mark Offset Names
                Header.OffsetNames = (uint) bw.BaseStream.Position;

                var offsetArray = new PsbArray(tOffsets);
                offsetArray.WriteTo(bw);
                var treeArray = new PsbArray(trie);
                treeArray.WriteTo(bw);
                var nameArray = new PsbArray(tNames);
                nameArray.WriteTo(bw);
            }

            #endregion

            #region Compile Entries

            Header.OffsetEntries = (uint)bw.BaseStream.Position;
            Pack(bw, Root);

            #endregion

            #region Compile Strings

            if (Strings.Count == 0)
            {
                Debug.WriteLine("Strings.Count == 0. Maybe forgot Merge() ?");
            }
            using (var strMs = new MemoryStream())
            {
                List<uint> offsets = new List<uint>(Strings.Count);
                BinaryWriter strBw = new BinaryWriter(strMs, Encoding);
                //Collect Strings
                for (var i = 0; i < Strings.Count; i++)
                {
                    var psbString = Strings[i];
                    offsets.Add((uint)strBw.BaseStream.Position);
                    strBw.WriteStringZeroTrim(psbString.Value);
                }

                strBw.Flush();
                //Mark Offset Strings
                Header.OffsetStrings = (uint)bw.BaseStream.Position;
                StringOffsets = new PsbArray(offsets);
                StringOffsets.WriteTo(bw);
                Header.OffsetStringsData = (uint)bw.BaseStream.Position;
                strMs.WriteTo(bw.BaseStream);
                //bw.Write(strMs.ToArray());
            }

            #endregion

            #region Compile Resources

            if (Header.Version >= 4)
            {
                using var resMs = new MemoryStream();
                List<uint> offsets = new List<uint>(ExtraResources.Count);
                List<uint> lengths = new List<uint>(ExtraResources.Count);

                BinaryWriter resBw = new BinaryWriter(resMs, Encoding);

                for (var i = 0; i < ExtraResources.Count; i++)
                {
                    var psbResource = ExtraResources[i];
                    offsets.Add((uint)resBw.BaseStream.Position);
                    if (psbResource.Data == null)
                    {
                        lengths.Add((uint)0);
                    }
                    else
                    {
                        lengths.Add((uint)psbResource.Data.Length);
                        resBw.Write(psbResource.Data);
                    }
                }

                resBw.Flush();
                Header.OffsetExtraChunkOffsets = (uint)bw.BaseStream.Position;
                ExtraChunkOffsets = new PsbArray(offsets);
                ExtraChunkOffsets.WriteTo(bw);
                Header.OffsetExtraChunkLengths = (uint)bw.BaseStream.Position;
                ExtraChunkLengths = new PsbArray(lengths);
                ExtraChunkLengths.WriteTo(bw);
                if (Consts.PsbDataStructureAlign)
                {
                    DataAlign(bw);
                }

                Header.OffsetExtraChunkData = (uint)bw.BaseStream.Position;
                resMs.WriteTo(bw.BaseStream);
                //bw.Write(resMs.ToArray());
            }

            using (var resMs = new MemoryStream())
            {
                List<uint> offsets = new List<uint>(Resources.Count);
                List<uint> lengths = new List<uint>(Resources.Count);

                BinaryWriter resBw = new BinaryWriter(resMs, Encoding);

                for (var i = 0; i < Resources.Count; i++)
                {
                    var psbResource = Resources[i];
                    offsets.Add((uint)resBw.BaseStream.Position);
                    if (psbResource.Data == null)
                    {
                        lengths.Add((uint)0);
                    }
                    else
                    {
                        lengths.Add((uint)psbResource.Data.Length);
                        resBw.Write(psbResource.Data);
                    }
                }

                resBw.Flush();
                Header.OffsetChunkOffsets = (uint)bw.BaseStream.Position;
                ChunkOffsets = new PsbArray(offsets);
                ChunkOffsets.WriteTo(bw);
                Header.OffsetChunkLengths = (uint)bw.BaseStream.Position;
                ChunkLengths = new PsbArray(lengths);
                ChunkLengths.WriteTo(bw);
                if (Consts.PsbDataStructureAlign)
                {
                    DataAlign(bw);
                }

                Header.OffsetChunkData = (uint)bw.BaseStream.Position;
                resMs.WriteTo(bw.BaseStream);
                //bw.Write(resMs.ToArray());
            }

            #endregion

            #region Compile Header

            bw.Seek(0, SeekOrigin.Begin);
            bw.Write(Header.ToBytes());

            #endregion

            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Perform (16) byte data align
        /// </summary>
        /// <param name="bw"></param>
        /// <param name="align">by default it should be 16</param>
        /// <returns>padded length</returns>
        private int DataAlign(BinaryWriter bw, int align = 16)
        {
            var len = (int)bw.BaseStream.Position % align;
            if (len != 0)
            {
                var pad = align - len;
                bw.Pad(pad);
                return pad;
            }

            return 0;
        }

        private void Pack(BinaryWriter bw, IPsbValue obj)
        {
            switch (obj)
            {
                case null: //
                    PsbNull.Null.WriteTo(bw);
                    return;
                case PsbNull pNull:
                    pNull.WriteTo(bw);
                    return;
                case PsbBool pBool:
                    pBool.WriteTo(bw);
                    return;
                case PsbNumber pNum:
                    pNum.WriteTo(bw);
                    return;
                case PsbArray pArr:
                    pArr.WriteTo(bw);
                    return;
                case PsbString pStr:
                    pStr.WriteTo(bw);
                    return;
                case PsbResource pRes:
                    if (pRes.Index == null || (pRes.IsExtra && pRes.Index >= ExtraResources.Count) || (!pRes.IsExtra && pRes.Index >= Resources.Count))
                    {
                        Debug.WriteLine($"Resource index: {pRes.Index} seems to be wrong!");
                    }
                    if (pRes.Data == null) //MARK: null resource will be eliminated!
                    {
                        PsbNull.Null.WriteTo(bw);
                    }
                    else
                    {
                        pRes.WriteTo(bw);
                    }

                    return;
                case PsbList pCol:
                    SaveCollection(bw, pCol);
                    return;
                case PsbDictionary pDic:
                    SaveObjects(bw, pDic);
                    return;
                default:
                    return;
            }
        }

        private void SaveObjects(BinaryWriter bw, PsbDictionary pDic)
        {
            bw.Write((byte)pDic.Type);
            var namesList = new List<uint>(pDic.Count);
            var indexList = new List<uint>(pDic.Count);
            using (var ms = Consts.MsManager.GetStream())
            {
                BinaryWriter mbw = new BinaryWriter(ms, Encoding);
                if (Consts.PsbObjectOrderByKey)
                {
                    foreach (var pair in pDic.OrderBy(p => p.Key, StringComparer.Ordinal))
                    {
                        //var index = Names.BinarySearch(pair.Key); //Sadly, we may not use it for performance
                        //var index = Names.FindIndex(s => s == pair.Key);
                        var index = Names.IndexOf(pair.Key);
                        if (index < 0)
                        {
                            throw new IndexOutOfRangeException($"Can not find Name [{pair.Key}] in Name Table");
                        }

                        namesList.Add((uint)index);
                        indexList.Add((uint)mbw.BaseStream.Position);
                        Pack(mbw, pair.Value);
                    }
                }
                else
                {
                    foreach (var pair in pDic)
                    {
                        //var index = Names.BinarySearch(pair.Key); //Sadly, we may not use it for performance
                        //var index = Names.FindIndex(s => s == pair.Key);
                        var index = Names.IndexOf(pair.Key);
                        if (index < 0)
                        {
                            throw new IndexOutOfRangeException($"Can not find Name [{pair.Key}] in Name Table");
                        }

                        namesList.Add((uint)index);
                        indexList.Add((uint)mbw.BaseStream.Position);
                        Pack(mbw, pair.Value);
                    }
                }


                mbw.Flush();
                new PsbArray(namesList).WriteTo(bw);
                new PsbArray(indexList).WriteTo(bw);
                ms.WriteTo(bw.BaseStream);
                //bw.Write(ms.ToArray());
            }
        }

        /// <summary>
        /// Save a List
        /// </summary>
        /// <param name="bw"></param>
        /// <param name="pCol"></param>
        private void SaveCollection(BinaryWriter bw, PsbList pCol)
        {
            bw.Write((byte)pCol.Type);
            var indexList = new List<uint>(pCol.Count);
            using (var ms = Consts.MsManager.GetStream())
            {
                BinaryWriter mbw = new BinaryWriter(ms, Encoding);

                foreach (var obj in pCol)
                {
                    indexList.Add((uint)mbw.BaseStream.Position);
                    Pack(mbw, obj);
                }

                mbw.Flush();
                new PsbArray(indexList).WriteTo(bw);
                ms.WriteTo(bw.BaseStream);
                //bw.Write(ms.ToArray());
            }
        }

        /// <summary>
        /// Export all resources
        /// </summary>
        /// <param name="path"></param>
        public void SaveRawResources(string path)
        {
            for (int i = 0; i < Resources.Count; i++)
            {
                File.WriteAllBytes(
                    Path.Combine(path, Resources[i].Index == null ? $"##{i}.bin" : $"{Resources[i].Index}.bin"),
                    Resources[i].Data);
            }

            for (int i = 0; i < ExtraResources.Count; i++)
            {
                File.WriteAllBytes(
                    Path.Combine(path, ExtraResources[i].Index == null ? $"#@{i}.bin" : $"@{ExtraResources[i].Index}.bin"),
                    ExtraResources[i].Data);
            }
        }

        /// <summary>
        /// Try skip header and load. May (not) work on any PSB only if body is not encrypted
        /// <para>Can not use <see cref="Consts.InMemoryLoading"/> so it will be slow.</para>
        /// <remarks>DuRaRaRa!!</remarks>
        /// </summary>
        /// <param name="path"></param>
        /// <param name="detectSize"></param>
        public static PSB DullahanLoad(string path, int detectSize = 1024)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("File not exists.", path);
            }

            using (var fs = new FileStream(path, FileMode.Open))
            {
                var psb = new PSB();
                psb.LoadFromDullahan(fs, detectSize);
                return psb;
            }
        }

        /// <summary>
        /// Try skip header and load
        /// <para>May (not) work on any PSB only if body is not encrypted</para>
        /// <remarks>DuRaRaRa!!</remarks>
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="detectSize"></param>
        public static PSB DullahanLoad(Stream stream, int detectSize = 1024)
        {
            var psb = new PSB();
            psb.LoadFromDullahan(stream, detectSize);
            return psb;
        }

        private void LoadFromDullahan(Stream stream, int detectSize = 1024, int? namePosition = null)
        {
            //var ctx = FreeMount.CreateContext();
            //string currentType = null;
            //var ms = ctx.OpenFromShell(stream, ref currentType);
            //if (ms != null)
            //{
            //    var oldStream = stream;
            //    stream = ms;
            //    oldStream.Dispose();
            //}

            byte[] wNumbers = { 1, 0, 0, 0 };
            byte[] nNumbers = { 1, 0 };
            var possibleHeader = new byte[detectSize];
            stream.Read(possibleHeader, 0, detectSize);
            var namePos = -1;
            var startPos = 0;
            if (namePosition != null)
            {
                namePos = namePosition.Value;
                startPos = 0;
            }
            else
            {
                for (var i = 0; i < possibleHeader.Length - wNumbers.Length - 3; i++)
                {
                    //find 0x0E / 0x0D
                    if (possibleHeader[i] == (int)PsbObjType.ArrayN2 || possibleHeader[i] == (int)PsbObjType.ArrayN1)
                    {
                        var offset1 = possibleHeader[i] - 0xB;
                        if (possibleHeader[i + offset1] == 0x0E)
                        {
                            if (possibleHeader.Skip(i + 4).Take(wNumbers.Length).SequenceEqual(wNumbers))
                            {
                                namePos = i;
                                break;
                            }
                        }
                        else if (possibleHeader[i + offset1] == 0x0D)
                        {
                            if (possibleHeader.Skip(i + offset1 + 1).Take(nNumbers.Length).SequenceEqual(nNumbers))
                            {
                                namePos = i;
                                break;
                            }
                        }
                    }
                    else if (possibleHeader[i] == 'P')
                    {
                        if (possibleHeader[i + 1] == 'S' && possibleHeader[i + 2] == 'B')
                        {
                            startPos = i;
                        }
                    }
                }
            }


            if (namePos < 0)
            {
                throw new PsbBadFormatException(PsbBadFormatReason.Body,
                    "Can not find Names segment, Dullahan load failed");
            }

            if (namePos - startPos == PsbHeader.GetHeaderLength(4))
            {
                Header.Version = 4;
            }
            else
            {
                Header.Version = 3;
            }

            var br = new BinaryReader(stream, Encoding);
            Strings = new List<PsbString>();
            Resources = new List<PsbResource>();
            ExtraResources = new List<PsbResource>();

            //Load Names
            var offsetMaybeNamesOrOffsetKeyIndex = (uint) namePos;
            br.BaseStream.Seek(offsetMaybeNamesOrOffsetKeyIndex, SeekOrigin.Begin);
            var offsetMaybeCharsetsOrIndexes = new PsbArray(br.ReadByte() - (byte) PsbObjType.ArrayN1 + 1, br);
            var namesDetector = new PsbArrayDetector(br);
            if (namesDetector.IsArray) //PSBv2+
            {
                Header.OffsetNames = offsetMaybeNamesOrOffsetKeyIndex;
                Charset = offsetMaybeCharsetsOrIndexes;
                NamesData = namesDetector.ToPsbArray(br);
                NameIndexes = new PsbArray(br.ReadByte() - (byte) PsbObjType.ArrayN1 + 1, br);
                LoadNames();
            }
            else //PSBv1
            {
                Header.Version = 1;
                Header.HeaderLength = offsetMaybeNamesOrOffsetKeyIndex; // OffsetKeyIndex
                Header.OffsetNames = (uint)namesDetector.Position;
                NameIndexes = offsetMaybeCharsetsOrIndexes;
                LoadKeys(br);
            }

            //Load Entries
            while (br.PeekChar() != (int)PsbObjType.Objects)
            {
                br.ReadByte();
            }

            Header.OffsetEntries = (uint)br.BaseStream.Position;
            IPsbValue obj = Unpack(br, true);

            Objects = obj as PsbDictionary ?? throw new PsbBadFormatException(PsbBadFormatReason.Objects, "Can not parse objects");

            //Load Strings
            while (!PsbArrayDetector.IsPsbArrayType((byte)br.PeekChar()))
            {
                br.ReadByte();
            }

            Header.OffsetStrings = (uint)br.BaseStream.Position;
            StringOffsets = new PsbArray(br.ReadByte() - (byte)PsbObjType.ArrayN1 + 1, br);
            Header.OffsetStringsData = (uint)br.BaseStream.Position;
            Strings.Sort((s1, s2) => (int)((s1.Index ?? int.MaxValue) - (s2.Index ?? int.MaxValue)));

            if (StringOffsets.Value.Count > 0 && Consts.InMemoryLoading)
            {
                uint strsEndPos = StringOffsets.Value.Max();
                br.BaseStream.Seek(strsEndPos, SeekOrigin.Current);
                br.ReadStringZeroTrim(Encoding);
                strsEndPos = (uint)br.BaseStream.Position;
                var strsLength = strsEndPos - Header.OffsetStringsData;
                br.BaseStream.Seek(-strsLength, SeekOrigin.Current);

                using (var strMs = new MemoryStream(br.ReadBytes((int)strsLength)))
                using (var strBr = new BinaryReader(strMs, Encoding))
                {
                    for (var i = 0; i < Strings.Count; i++)
                    {
                        var str = Strings[i];
                        if (str.Index == null)
                        {
                            continue;
                        }

                        strBr.BaseStream.Seek(StringOffsets[(int)str.Index], SeekOrigin.Begin);
                        var strValue = strBr.ReadStringZeroTrim(Encoding);
                        str.Value = strValue;
                    }
                }
            }
            else
            {
                for (var i = 0; i < Strings.Count; i++)
                {
                    var str = Strings[i];
                    if (str.Index == null)
                    {
                        continue;
                    }

                    br.BaseStream.Seek(Header.OffsetStringsData + StringOffsets[(int)str.Index], SeekOrigin.Begin);
                    var strValue = br.ReadStringZeroTrim(Encoding);
                    str.Value = strValue;
                }
            }


            //Load Resources
            while (br.PeekChar() != (int)PsbObjType.ArrayN1 && br.PeekChar() != (int)PsbObjType.ArrayN2)
            {
                br.ReadByte();
            }
            //currently found a PsbArray, could be ExtraResource or Resource
            var pos1 = (uint)br.BaseStream.Position;
            var array1 = new PsbArray(br.ReadByte() - (byte)PsbObjType.ArrayN1 + 1, br);
            var pos2 = (uint)br.BaseStream.Position;
            var array2 = new PsbArray(br.ReadByte() - (byte)PsbObjType.ArrayN1 + 1, br);
            var arriveEnd = br.BaseStream.Position == br.BaseStream.Length;
            //var peek = br.PeekChar();
            if (!arriveEnd &&
                (Header.Version >= 4 ||
                 br.PeekChar() == (int)PsbObjType.ArrayN1 ||
                 br.PeekChar() == (int)PsbObjType.ArrayN2)
            )
            {
                Header.Version = 4;
                Header.HeaderLength = PsbHeader.GetHeaderLength(Header.Version);
                Header.OffsetExtraChunkOffsets = pos1;
                Header.OffsetExtraChunkLengths = pos2;
                ExtraChunkOffsets = array1;
                ExtraChunkLengths = array2;

                //There is extra data. Detect Extra Data (I hate padding)
                if (array1.Value.Count > 0 && array2.Value.Count > 0)
                {
                    var currentPos = br.BaseStream.Position;
                    //var shouldBeLength = ExtraChunkOffsets.Value.Max() + ExtraChunkLengths.Value.Max();
                    var lengthOfMaxOffsetItem = ExtraChunkLengths[ExtraChunkOffsets.Value.IndexOf(ExtraChunkOffsets.Value.Max())];
                    var shouldBeLength = ExtraChunkOffsets.Value.Max() + lengthOfMaxOffsetItem;
                    br.BaseStream.Position = currentPos + shouldBeLength;
                    var detectionArea = br.ReadBytes(detectSize);
                    var detected = false;
                    for (int i = 0; i < detectSize; i++)
                    {
                        br.BaseStream.Position = currentPos + shouldBeLength + i;
                        if (PsbArrayDetector.IsPsbArrayType(detectionArea[i]))
                        {
                            var dummyOffsets = new PsbArrayDetector(br);
                            if (dummyOffsets.IsArray) //found real Resource Offset Array
                            {
                                br.BaseStream.Position = dummyOffsets.Position + dummyOffsets.Size;
                                var dummyLengths = new PsbArrayDetector(br);
                                if (dummyLengths.IsArray) //found real Resource Length Array, confirmed!
                                {
                                    Header.OffsetExtraChunkData = (uint)dummyOffsets.Position - shouldBeLength;
                                    Header.OffsetChunkOffsets = (uint)dummyOffsets.Position;
                                    Header.OffsetChunkLengths = (uint)dummyLengths.Position;
                                    ChunkOffsets = dummyOffsets.ToPsbArray(br);
                                    ChunkLengths = dummyLengths.ToPsbArray(br);
                                    detected = true;
                                    break;
                                }
                            }

                            br.BaseStream.Position = dummyOffsets.Position;
                        }
                    }

                    if (!detected)
                    {
                        throw new PsbBadFormatException(PsbBadFormatReason.Body, "Can not find ExtraChunk");
                    }

                    br.BaseStream.Position = Header.OffsetExtraChunkData;
                    //since this Data position is confirmed to be correct, it should be ok to load all extra resources
                    for (int i = 0; i < ExtraResources.Count; i++)
                    {
                        LoadExtraResource(ExtraResources[i], br);
                    }
                }
                else
                {
                    Header.OffsetExtraChunkData = (uint)br.BaseStream.Position;
                    while (!PsbArrayDetector.IsPsbArrayType((byte)br.PeekChar()))
                    {
                        br.ReadByte();
                    }

                    //var pos3 = br.BaseStream.Position;
                    ChunkOffsets = new PsbArray(br.ReadByte() - (byte)PsbObjType.ArrayN1 + 1, br);
                    Header.OffsetChunkLengths = (uint)br.BaseStream.Position;
                    ChunkLengths = new PsbArray(br.ReadByte() - (byte)PsbObjType.ArrayN1 + 1, br); //got it
                }
            }
            else //resource chunk
            {
                Header.OffsetChunkOffsets = pos1;
                ChunkOffsets = array1;
                Header.OffsetChunkLengths = pos2;
                ChunkLengths = array2;
            }

            Header.OffsetChunkData = (uint)br.BaseStream.Position;

            if (Resources.Count > 0)
            {
                Resources.Sort((r1, r2) => (int)((r1.Index ?? int.MaxValue) - (r2.Index ?? int.MaxValue)));

                #region Dullahan Resource Inference : Infer by align

                //Failed on some no align PSB

                //WARN: Didn't test very much, if your texture looks strange, FIX THIS
                //If this is wrong, try to align by EOF
                //var currentPos = (uint)br.BaseStream.Position;
                //var padding = 16 - currentPos % 16;
                //br.ReadBytes((int)padding);
                //if (padding < 16)
                //{
                //    if (br.ReadBytes(16).All(b => b == 0))
                //    {
                //        padding += 16;
                //    }
                //}

                #endregion

                #region Dullahan Resource Inference : Infer by EOF

                //This method works on all known PSB

                var currentPos = br.BaseStream.Position;
                var remainLength = br.BaseStream.Length - currentPos;
                var lengthOfMaxOffsetItem = ChunkLengths[ChunkOffsets.Value.IndexOf(ChunkOffsets.Value.Max())];
                var shouldBeLength = ChunkOffsets.Value.Max() + lengthOfMaxOffsetItem;
                var padding = Math.Max((remainLength - shouldBeLength), 0);

                #endregion

                Header.OffsetChunkData = (uint)(currentPos + padding);
                foreach (var res in Resources)
                {
                    LoadResource(res, br);
                }
            }

            AfterLoad();
        }
    }
}