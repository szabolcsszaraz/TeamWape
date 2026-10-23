using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization
{
    /// <summary>
    /// Performs multiple text-pattern matching.
    /// </summary>
    class AhoCorasickSearch
    {
        class TrieNode
        {
            public readonly IDictionary<char, TrieNode> Children = new Dictionary<char, TrieNode>();
            public TrieNode Fail;
            public readonly List<string> Output = new();
        }

        /// <summary>
        /// Describes a single result of <see cref="AhoCorasickSearch.Search"/>.
        /// </summary>
        public readonly struct Match : IEquatable<Match>
        {
            readonly int m_HashCode;

            /// <summary>
            /// The index in the source string where the <see cref="Pattern"/> has been found.
            /// </summary>
            public readonly int Index;

            /// <summary>
            /// The pattern found.
            /// </summary>
            public readonly SubString Pattern;

            /// <summary>
            /// Initializes a new instance of the <see cref="Match"/> type.
            /// </summary>
            /// <param name="index">
            /// The index in the source string where the <see cref="Pattern"/> has been found.
            /// </param>
            /// <param name="pattern">
            /// The pattern found.
            /// </param>
            public Match(int index, SubString pattern)
            {
                Index = index;
                Pattern = pattern;
                m_HashCode = HashCode.Combine(Index, Pattern);
            }

            /// <summary>
            /// Gets the bounds of the <see cref="Pattern"/> within the source string.
            /// </summary>
            public Range Offsets => new Range(Index, Index + Pattern.Length);

            /// <summary>
            /// Deconstructs this instance.
            /// </summary>
            /// <param name="offsets">
            /// The bounds of the <see cref="Pattern"/> within the source string.
            /// </param>
            /// <param name="pattern">
            /// The pattern found.
            /// </param>
            public void Deconstruct(out Range offsets, out SubString pattern)
            {
                offsets = Offsets;
                pattern = Pattern;
            }

            /// <inheritdoc cref="IEquatable{T}.Equals(T)" />
            public bool Equals(Match other) => Index == other.Index && Pattern == other.Pattern;

            /// <inheritdoc />
            public override bool Equals(object obj) => obj is Match other && Equals(other);

            /// <inheritdoc />
            public override int GetHashCode() => m_HashCode;
        }

        static TrieNode BuildTrie(IEnumerable<string> patterns)
        {
            var root = new TrieNode();

            foreach (var pattern in patterns)
            {
                var node = root;
                foreach (var c in pattern)
                {
                    var found = node.Children.TryGetValue(c, out var child);
                    if (!found)
                    {
                        child = new TrieNode();
                        node.Children.Add(c, child);
                    }
                    node = child;
                }
                node.Output.Add(pattern);
            }

            return root;
        }

        static void BuildFailureLinks(TrieNode root)
        {
            var nodes = new Queue<TrieNode>();
            foreach (var child in root.Children.Values)
            {
                child.Fail = root;
                nodes.Enqueue(child);
            }

            while (nodes.TryDequeue(out var node))
            {
                foreach (var (key, child) in node.Children)
                {
                    var failNode = node.Fail;
                    while(failNode != null && !failNode.Children.ContainsKey(key))
                        failNode = failNode.Fail;

                    child.Fail = failNode != null && failNode.Children.TryGetValue(key, out var failChild)
                        ? failChild
                        : root;
                    child.Output.AddRange(child.Fail.Output);
                    nodes.Enqueue(child);
                }
            }
        }

        readonly TrieNode m_Root;

        /// <summary>
        /// Initializes a new instance of the <see cref="AhoCorasickSearch"/> type.
        /// </summary>
        /// <param name="patterns">
        /// The patterns to find.
        /// </param>
        public AhoCorasickSearch(IEnumerable<string> patterns)
        {
            m_Root = BuildTrie(patterns);
            BuildFailureLinks(m_Root);
        }

        /// <summary>
        /// Looks for the patterns of this <see cref="AhoCorasickSearch"/> instance inside the
        /// specified <paramref name="source"/>.
        /// </summary>
        /// <param name="source">
        /// The source where the patterns are searched.
        /// </param>
        /// <param name="output">
        /// The target container of the found <see cref="Match"/> instances.
        /// </param>
        /// <returns>
        /// The number of matches found.
        /// </returns>
        public int Search(ReadOnlySpan<char> source, [NotNull] IList<Match> output)
        {
            var count = 0;
            var node = m_Root;
            for (var index = 0; index < source.Length; index++)
            {
                var c = source[index];
                while (node != null && !node.Children.ContainsKey(c))
                    node = node.Fail;

                if (node == null)
                {
                    node = m_Root;
                    continue;
                }

                node = node.Children[c];

                for (var outputIndex = 0; outputIndex < node.Output.Count; outputIndex++)
                {
                    var pattern = node.Output[outputIndex];
                    output.Add(new(1 + index - pattern.Length, pattern));
                    count++;
                }
            }

            return count;
        }
    }
}
