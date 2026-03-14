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
    /// See: https://en.wikipedia.org/wiki/Trie  https://zhuanlan.zhihu.com/p/143975546
    /// See Also: (by cyanic) https://gitlab.com/modmyclassic/sega-mega-drive-mini/marchive-batch-tool/-/blob/master/psb_v4.md
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
            //public uint FirstChar = 0;
            public uint FirstChar => MinChar;

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
        /// <summary>
        /// store the parent node ID for each node, index = nodeId
        /// </summary>
        private readonly List<uint> _tree = new List<uint>();
        /// <summary>
        /// store the offset for each node, index = nodeId
        /// </summary>
        private readonly List<uint> _offsets = new List<uint>();
        private TrieNode _root;
        private readonly bool _enableOptimization = false;
        
        // Optimized allocator state (occupancy bitmap driven placement)
        private List<bool> _occupancy;
        private uint _firstVacant;
        

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

        public PrefixTree(bool optimize = false)
        {
            _enableOptimization = optimize;
        }

        public PrefixTree(List<string> input, bool optimize = false)
        {
            Values = input;
            _enableOptimization = optimize;
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
                prev = GetNode(prev, (char) c);
            }

            GetNode(prev, (char)0, true); //terminal node
        }

        public static PrefixTree Build(List<string> namesList, bool optimize, out List<uint> names, out List<uint> tree, out List<uint> offsets)
        {
            //namesList.Sort((s1, s2) => s1.Length - s2.Length);
            var trie = new PrefixTree(namesList, optimize);
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
                    var prevValue = _offsets[(int)currentValue];
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
            // Process children in sorted order
            foreach (var child in node.Children.OrderBy(c => c.Key))
            {
                child.Value.Parent = node;
                MakeTree(child.Value);
            }

            foreach (var child in node.Children.OrderBy(c => c.Key))
            {
                MakeOffset(child.Value);
            }

            foreach (var child in node.Children.OrderBy(c => c.Key))
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

        /// <summary>
        /// Find the optimal position for allocating child nodes.
        /// A free index range is found that will accommodate the indexes of each child node.
        /// The position returned is BeginPosition, which will be used to calculate node IDs for children.
        /// </summary>
        /// <param name="node">The parent node</param>
        /// <param name="minChar">Minimum child character value</param>
        /// <param name="maxChar">Maximum child character value</param>
        /// <returns>The optimal starting position</returns>
        private int FindOptimalPosition(TrieNode node, char minChar, char maxChar)
        {
            var childChars = node.Children.Keys.OrderBy(c => c).ToArray();
            var rangeSize = maxChar - minChar + 1;
            
            // Start searching from a reasonable position
            var candidatePos = Math.Max(1, _tree.Count);
            
            // Ensure candidatePos is valid (must be greater than max/min char for the calculation to work)
            if (candidatePos <= maxChar || candidatePos <= minChar)
            {
                candidatePos = Math.Max(maxChar, minChar) + 1;
            }
            
            // Try to find a position that minimizes waste
            int bestPos = candidatePos;
            int bestWaste = int.MaxValue;
            
            // Search for a position where all child slots are free
            for (int searchAttempts = 0; searchAttempts < 100; searchAttempts++)
            {
                bool allFree = true;
                int wastedSlots = 0;
                
                // Check if all positions in the range [candidatePos, candidatePos + rangeSize - 1] are available
                // But we only care about positions for actual children
                for (int i = 0; i < childChars.Length; i++)
                {
                    char c = childChars[i];
                    int offset = c - minChar;
                    int targetPos = candidatePos + offset;
                    
                    if (targetPos < _tree.Count && _tree[targetPos] != 0)
                    {
                        allFree = false;
                        break;
                    }
                }
                
                if (allFree)
                {
                    // Count wasted slots (positions between min and max that aren't used)
                    wastedSlots = rangeSize - childChars.Length;
                    
                    if (wastedSlots < bestWaste)
                    {
                        bestPos = candidatePos;
                        bestWaste = wastedSlots;
                        
                        // If we found a perfect fit (no waste), use it immediately
                        if (wastedSlots == 0)
                        {
                            return bestPos;
                        }
                    }
                    
                    // Found a valid position, but keep searching for a better one
                    if (searchAttempts > 20)  // After some attempts, accept good enough
                    {
                        return bestPos;
                    }
                }
                
                candidatePos++;
                
                // Make sure candidatePos stays valid
                if (candidatePos <= maxChar || candidatePos <= minChar)
                {
                    candidatePos = Math.Max(maxChar, minChar) + 1;
                }
            }
            
            return bestPos;
        }

        private void MakeOffset(TrieNode node)
        {
            // For tail nodes (string terminators), store the string index
            if (node.Char == (char)0)
            {
                int index = FindIndex(this[node]);
                _offsets.Set(node.Id, (uint)index);
                return;
            }

            if (node.Children.Count == 0)
            {
                return;
            }

            char max = (char)Math.Max(node.MaxChar, node.MinChar);
            char min = (char)Math.Min(node.MaxChar, node.MinChar);
            int count = max - min;
            int pos;
            
            if (_enableOptimization)
            {
                // Optimized mode: find the best position that fits all children
                // This implements the PSB v4 spec requirement to find a free index range
                pos = FindOptimalPosition(node, min, max);
            }
            else
            {
                // Original algorithm: allocate at the end
                pos = _tree.Count;
                
                // Ensure position is valid
                if (pos <= max || pos <= min)
                {
                    _tree.Set(max, 0u);
                    pos = _tree.Count;
                }
            }
            
            int endPos = pos + count;
            uint offset = (uint)pos - min;
            
            // Expand tree to accommodate the end position
            _tree.Set(endPos, 0u);
            
            // Set the base value for this node
            _offsets.Set(node.Id, offset);

            // Store position info for MakeTree to use
            node.BeginPosition = pos;
            node.EndPosition = endPos;
        }

        private void MakeTree(TrieNode node)
        {
            int nodeId = 0;
            uint offset = 0;

            if (node.Parent.Id == 0) // first level under root
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

            if (_enableOptimization)
            {
                BuildOptimized();
            }
            else
            {
                _offsets.Add(1);
                MakeBranch(Root);
                MakeLink();
            }
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

        internal static (TrieNode Root, Dictionary<string, uint> Results) LoadNodes(List<uint> names, List<uint> trees, List<uint> offsets)
        {
            // 创建根节点
            TrieNode root = new TrieNode { Id = 0 };

            // 用于保存已创建的节点，键为节点的 Id，值为 TrieNode 实例
            Dictionary<int, TrieNode> nodeDict = new Dictionary<int, TrieNode>();
            nodeDict[0] = root;

            // 遍历树结构，重建节点
            for (int i = 1; i < trees.Count; i++)
            {
                uint parentId = trees[i]; // 获取父节点的 Id
                if (!nodeDict.ContainsKey((int) parentId))
                {
                    // 如果父节点不存在，创建父节点
                    nodeDict[(int) parentId] = new TrieNode { Id = (int) parentId };
                }

                TrieNode parentNode = nodeDict[(int) parentId];

                uint offset = 0;
                if (offsets.Count > (int) parentId)
                {
                    offset = offsets[(int) parentId];
                }

                // 计算当前字符
                char c = (char) (i - offset);

                if (!nodeDict.TryGetValue(i, out var currentNode))
                {
                    currentNode = new TrieNode();
                    nodeDict[i] = currentNode;
                }

                currentNode.Id = i;
                currentNode.Char = c;
                currentNode.Parent = parentNode;

                // 将当前节点添加到父节点的子节点中
                parentNode.Children[c] = currentNode;
            }

            // 处理终结节点，构建 Results
            var results = new Dictionary<string, uint>();
            foreach (var nameIndex in names)
            {
                TrieNode node = nodeDict[(int) nameIndex];
                var charList = new List<byte>();

                // 回溯获取完整字符串
                while (node.Parent != null)
                {
                    charList.Add((byte) node.Char);
                    node = node.Parent;
                }

                charList.Reverse();
                var str = Encoding.UTF8.GetString(charList.ToArray()).TrimEnd('\0');

                // 添加到结果中
                results[str] = nameIndex;
            }

            return (root, results);
        }

        #region Optimized Build (bitmap-based range fitting)

        private void BuildOptimized()
        {
            _tree.Clear();
            _offsets.Clear();
            _names.Clear();
            Results.Clear();

            // Root always occupies index 0; base (offsets[0]) must be >= 1
            _offsets.Set(0, 1u);

            // Initialize used map: mark 0 as used, next free = 1
            _occupancy = new List<bool> { true };
            _firstVacant = 1;

            ProcessOptimized(Root);
            MakeLink();
        }

        private void ProcessOptimized(TrieNode curr)
        {
            // Order children by character (PSB spec)
            var children = curr.Children.Values.OrderBy(x => x.Char).ToArray();
            if (children.Length == 0)
            {
                return;
            }

            uint minChildValue = children[0].Char;
            // Compute relative offsets from min child value (use pooled array to avoid allocations)
            int childCount = children.Length;
            var relativeOffsets = System.Buffers.ArrayPool<uint>.Shared.Rent(childCount);
            for (int i = 0; i < childCount; i++)
            {
                relativeOffsets[i] = (uint) (children[i].Char - minChildValue);
            }

            // Per spec, ensure base >= 1; therefore the first-level child index must be >= char + 1
            var minSlot = Math.Max(_firstVacant, minChildValue + 1u);
            var minChildIndex = ProbeFreeWindow(minSlot, relativeOffsets, childCount, out var needExtending, false);
            if (needExtending)
            {
                ExpandOccupancy(minChildIndex + relativeOffsets[childCount - 1]);
            }

            // Assign child indexes and parent links
            for (int i = 0; i < childCount; i++)
            {
                var child = children[i];
                var index = minChildIndex + relativeOffsets[i];
                child.Id = (int) index;
                _tree.Set((int) index, (uint) curr.Id); // check[index] = parentId
                MarkOccupied((int) index);

                // Tail (terminator) node: store key index in base[tail]
                if (child.Char == (char) 0)
                {
                    int keyIdx = FindIndex(this[child]);
                    if (keyIdx >= 0)
                    {
                        _offsets.Set(child.Id, (uint) keyIdx);
                        Results[this[child]] = (uint) child.Id;
                    }
                }
            }

            // Set base (offset) for current node
            var baseValue = minChildIndex - minChildValue;
            _offsets.Set(curr.Id, baseValue);

            // Return pooled buffer
            System.Buffers.ArrayPool<uint>.Shared.Return(relativeOffsets, false);

            // For compatibility/debug only
            curr.BeginPosition = (int) minChildIndex;
            curr.EndPosition = (int) (minChildIndex + (children[children.Length - 1].Char - (char) minChildValue + 1));

            AdvanceFirstVacant();

            // DFS: process non-terminating children
            foreach (var child in children)
            {
                if (child.Char != (char) 0)
                {
                    ProcessOptimized(child);
                }
            }
        }

        private void AdvanceFirstVacant()
        {
            while (_firstVacant < _occupancy.Count && _occupancy[(int) _firstVacant])
            {
                _firstVacant++;
            }
        }

        private uint ProbeFreeWindow(uint minSlot, uint[] range, int rangeCount, out bool needExtending, bool hardConstraint)
        {
            needExtending = false;
            for (uint i = minSlot; i < (uint) _occupancy.Count; i++)
            {
                bool found = true;
                for (int k = 0; k < rangeCount; k++)
                {
                    var target = i + range[k];
                    if (target < (uint) _occupancy.Count)
                    {
                        if (_occupancy[(int) target])
                        {
                            found = false;
                            if (hardConstraint) throw new Exception("Range not free for hard constraint");
                            break;
                        }
                    }
                    else
                    {
                        needExtending = true;
                        break;
                    }
                }

                if (found)
                {
                    return i;
                }
            }

            needExtending = true;
            return Math.Max((uint) _occupancy.Count, minSlot);
        }

        private void ExpandOccupancy(uint targetIndex)
        {
            while (_occupancy.Count <= targetIndex)
            {
                _occupancy.Add(false);
            }
        }

        private void MarkOccupied(int index)
        {
            if (index >= _occupancy.Count)
            {
                ExpandOccupancy((uint) index);
            }
            _occupancy[index] = true;
        }

        #endregion
    }
}
