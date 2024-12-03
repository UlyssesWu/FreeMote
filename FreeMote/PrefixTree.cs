using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace FreeMote
{
    /// <summary>
    /// Trie (prefix tree, 字典树/前缀树) for string
    /// </summary>
    /// Rewrited from psbtools\pcc\psb_cc_btree by number201724(number201724@me.com).
    /// SEE: https://en.wikipedia.org/wiki/Trie  https://zhuanlan.zhihu.com/p/143975546
    public class PrefixTree
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
            public readonly Dictionary<char, TrieNode> Children = new();

            public int BeginPosition = 0;
            public int EndPosition = 0;
            public uint FirstChar = 0;

            public char MinChar
            {
                get
                {
                    if (Children.Count <= 0)
                    {
                        return (char)0;
                    }
                    return Children.Keys.Min();
                }
            }
            public char MaxChar
            {
                get
                {
                    if (Children.Count <= 0)
                    {
                        return (char)0;
                    }
                    return Children.Keys.Max();
                }
            }
        }

        /// <summary>
        /// store the end node ID for each string, count = string count
        /// </summary>
        private readonly List<uint> _names = new List<uint>();
        private readonly List<uint> _tree = new List<uint>();
        private readonly List<uint> _offsets = new List<uint>();
        private TrieNode _root;

        internal TrieNode Root
        {
            get => _root ??= new TrieNode();
            set => _root = value;
        }

        internal List<string> Values = new List<string>();

        /// <summary>
        /// string, index
        /// </summary>
        public Dictionary<string, uint> Results { get; } = new();

        public PrefixTree()
        { }

        public PrefixTree(List<string> input)
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

        /// <summary>
        /// Insert a string to Trie
        /// </summary>
        /// <param name="value"></param>
        internal void InsertTree(string value)
        {
            TrieNode prev = Root;
            foreach (var c in Encoding.UTF8.GetBytes(value)) //WTF! We have to take unicode chars apart
            {
                prev = GetNode(prev, (char)c);
            }
            GetNode(prev, (char)0, true); //终结节点
        }

        public static PrefixTree Build(List<string> namesList, out List<uint> names, out List<uint> tree, out List<uint> offsets)
        {
            //namesList.Sort((s1, s2) => s1.Length - s2.Length);
            var trie = new PrefixTree(namesList);
            names = trie._names;
            tree = trie._tree;
            offsets = trie._offsets;
            return trie;
        }

        private TrieNode GetNode(TrieNode node, char c, bool isEnd = false)
        {
            if (node.Children.ContainsKey(c))
            {
                return node.Children[c];
            }

            var result = new TrieNode
            {
                Char = c,
                Parent = node
            };
            node.Children.Add(c, result);
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
                var lastValue = _tree[(int)index];
                while (lastValue != 0)
                {
                    var currentValue = _tree[(int)lastValue];
                    var prevValue = _tree[(int)currentValue];
                    var realChr = lastValue - prevValue;
                    //Debug.Write(realChr.ToString("X2") + " ");
                    lastValue = currentValue;

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
            foreach (var child in node.Children)
            {
                MakeTree(child.Value);
            }

            foreach (var child in node.Children)
            {
                MakeOffset(child.Value);
            }

            foreach (var child in node.Children)
            {
                MakeBranch(child.Value);
            }

            foreach (var child in node.Children)
            {
                if (child.Key == 0)
                {
                    Results[this[child.Value]] = (uint)child.Value.Id;
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
