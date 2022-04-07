using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace FreeMote
{
    /// <summary>
    /// Trie (prefix tree, 字典树/前缀树) for string, not optimized
    /// </summary>
    /// Rewrited from psbtools\pcc\psb_cc_btree by number201724(number201724@me.com).
    public class CrudeTrie
    {
        /// <summary>
        /// Node
        /// </summary>
        [DebuggerDisplay("{" + nameof(Char) + "}")]
        internal class TrieNode
        {
            public int Id = -1;
            public char Char = (char)0;
            public TrieNode Parent;
            public List<TrieNode> Childs = new List<TrieNode>();

            public int BeginPosition = 0;
            public int EndPosition = 0;
            public uint FirstChar = 0;

            public char MinChar
            {
                get
                {
                    if (Childs.Count <= 0)
                    {
                        return (char)0;
                    }
                    return Childs.Min(node => node.Char);
                }
            }
            public char MaxChar
            {
                get
                {
                    if (Childs.Count <= 0)
                    {
                        return (char)0;
                    }
                    return Childs.Max(node => node.Char);
                }
            }
        }

        private List<uint> _names = new List<uint>();
        private List<uint> _tree = new List<uint>();
        private List<uint> _offsets = new List<uint>();
        private TrieNode _root;

        internal TrieNode Root
        {
            get => _root ??= new TrieNode();
            set => _root = value;
        }

        internal List<string> Values = new List<string>();

        public Dictionary<string, uint> Results { get; } = new Dictionary<string, uint>();

        public CrudeTrie()
        { }

        public CrudeTrie(List<string> input)
        {
            Values = input;
            Build();
        }

        public int Insert(string value)
        {
            Values.Add(value);
            InsertTree(value);
            return Values.FindLastIndex(s => s == value);
        }

        internal void InsertTree(string value)
        {
            TrieNode prev = Root;
            foreach (var c in Encoding.UTF8.GetBytes(value)) //WTF! We have to take unicode chars apart
            {
                prev = GetNode(prev, (char)c);
            }
            GetNode(prev, (char)0, true);
        }

        public static CrudeTrie Build(List<string> namesList, out List<uint> names, out List<uint> tree, out List<uint> offsets)
        {
            var crudeTrie = new CrudeTrie(namesList);
            names = crudeTrie._names;
            tree = crudeTrie._tree;
            offsets = crudeTrie._offsets;
            return crudeTrie;
        }

        private TrieNode GetNode(TrieNode node, char c, bool isEnd = false)
        {
            TrieNode result = node.Childs.FirstOrDefault(child => child.Char == c);
            if (result != null)
            {
                return result;
            }

            result = new TrieNode
            {
                Char = c,
                Parent = node
            };
            node.Childs.Add(result);
            return result;
        }

        public int FindIndex(string name) => Values.FindIndex(s => s == name);

        internal string this[TrieNode node]
        {
            get
            {
                var list = new List<byte>();
                var prev = node.Parent;

                while (prev.Parent != null)
                {
                    list.Add((byte)prev.Char);
                    prev = prev.Parent;
                }

                list.Reverse();
                return Encoding.UTF8.GetString(list.ToArray());
            }
        }


        public string this[int tid]
        {
            get
            {
                var list = new List<byte>();
                var index = _names[tid];
                var chr = _tree[(int)index];
                while (chr != 0)
                {
                    var code = _tree[(int)chr];
                    var d = _tree[(int)code];
                    var realChr = chr - d;
                    //Debug.Write(realChr.ToString("X2") + " ");
                    chr = code;

                    //REF: https://stackoverflow.com/questions/18587267/does-list-insert-have-any-performance-penalty
                    list.Add((byte)realChr);
                }
                //Debug.WriteLine("");
                list.Reverse();
                return Encoding.UTF8.GetString(list.ToArray()); //That's why we don't use StringBuilder here.
            }
        }


        private void MakeBranch(TrieNode node)
        {
            foreach (var child in node.Childs)
            {
                MakeTree(child);
            }

            foreach (var child in node.Childs)
            {
                MakeOffset(child);
            }

            foreach (var child in node.Childs)
            {
                MakeBranch(child);
            }

            foreach (var child in node.Childs)
            {
                if (child.Char == 0)
                {
                    Results[this[child]] = (uint)child.Id;
                }
            }
        }

        private void MakeOffset(TrieNode node)
        {
            var max = Math.Max(node.MaxChar, node.MinChar);
            var min = Math.Min(node.MaxChar, node.MinChar);
            var count = max - min;
            var pos = _tree.Count;
            if (pos <= max || pos <= min)
            {
                _tree.Set(max, 0u);
                pos = _tree.Count;
            }
            int endPos = pos + count;
            uint offset = (uint)pos - min;
            _tree.Set(endPos, 0u);
            _offsets.Set(node.Id, offset);

            if (node.Char == (char)0) //end point link to names table index
            {
                int index = FindIndex(this[node]);
                _offsets.Set(node.Id, (uint)index);
                return;
            }

            node.BeginPosition = pos;
            node.EndPosition = endPos;
            node.FirstChar = min;
        }

        private void MakeTree(TrieNode node)
        {
            int nodeId = 0;
            uint offset = 0;

            if (node.Parent.FirstChar == 0) //Check if it's first char
            {
                //tree['a' + 0x1] = 0;
                //offsets[0] = 0x1;

                nodeId = node.Char;
                offset = _offsets[node.Parent.Id];
                nodeId += (int)offset;
            }
            else
            {
                //[current char] - [range char min] + start_pos
                nodeId = (int)(node.Char - node.Parent.FirstChar + node.Parent.BeginPosition);
            }

            //set parent value
            _tree.Set(nodeId, (uint)node.Parent.Id);
            //set node id
            node.Id = nodeId;
        }

        private void MakeLink()
        {
            _names.Clear();
            foreach (var value in Values)
            {
                _names.Add(Results[value]);
            }
        }

        private void Build()
        {
            Root = new TrieNode {Id = 0};
            foreach (var value in Values)
            {
                InsertTree(value);
            }
            _offsets.Add(1);
            MakeBranch(Root);
            MakeLink();
        }

        /// <summary>
        /// Load a Trie
        /// </summary>
        public static List<string> Load(List<uint> names, List<uint> trees, List<uint> offsets)
        {
            var results = new List<string>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                var list = new List<byte>();
                var index = names[i];
                var chr = trees[(int)index];
                while (chr != 0)
                {
                    var code = trees[(int)chr];
                    var d = offsets[(int)code];
                    var realChr = chr - d;
                    chr = code;
                    //REF: https://stackoverflow.com/questions/18587267/does-list-insert-have-any-performance-penalty
                    list.Add((byte)realChr);
                }
                //Debug.WriteLine("");
                list.Reverse();
                var str = Encoding.UTF8.GetString(list.ToArray()); //That's why we don't use StringBuilder here.
                results.Add(str);
            }
            return results;
        }

    }
}
