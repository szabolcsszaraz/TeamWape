using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization
{
    partial class AddedVocabulary
    {
        sealed class SearchTree
        {
            sealed class Node
            {
                public readonly Dictionary<char, Node> Next = new();
                public int Id;
                public int Length;
                public Node LongestMatch;
            }

            public readonly struct Match
            {
                public readonly int Id;
                public readonly Range Offset;

                public Match(int id, Range offset)
                {
                    Id = id;
                    Offset = offset;
                }

                public void Deconstruct(out int id, out Range range)
                {
                    id = Id;
                    range = Offset;
                }
            }

            static void BuildLongestMatches(Node root)
            {
                root.LongestMatch = root;

                var queue = new Queue<Node>();
                queue.Enqueue(root);

                while(queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    foreach(var child in node.Next.Values)
                    {
                        child.LongestMatch ??= node.LongestMatch;
                        queue.Enqueue(child);
                    }
                }
            }

            readonly Node m_Root;

            public SearchTree(IEnumerable<(string value, int id)> patterns)
            {
                m_Root = new();
                foreach(var (pattern, id) in patterns)
                {
                    if (string.IsNullOrEmpty(pattern)) continue;
                    InsertPattern(pattern, id);
                }
                BuildLongestMatches(m_Root);
            }

            void InsertPattern(string pattern, int id)
            {
                var node = m_Root;
                foreach (var ch in pattern)
                {
                    if (!node.Next.TryGetValue(ch, out var next))
                    {
                        next = new Node();
                        node.Next[ch] = next;
                    }
                    node = next;
                }

                node.Id = id;
                node.Length = pattern.Length;
                node.LongestMatch = node;
            }

            public int Search(ReadOnlySpan<char> text, IList<Match> matches)
            {
                if(text.Length == 0)
                    return 0;

                var count = 0;

                var offset = 0;
                var startOffset = 0;
                var node = m_Root;

                while(offset < text.Length)
                {
                    var c = text[offset];

                    var found = node.Next.TryGetValue(c, out var candidate);
                    if(found)
                    {
                        node = candidate;
                        offset++;
                        continue;
                    }

                    var longestMatch = node.LongestMatch;
                    if(longestMatch == m_Root)
                    {
                        offset = ++startOffset;
                        node = m_Root;
                        continue;
                    }

                    var match = new Match(longestMatch.Id, startOffset .. (startOffset + longestMatch.Length));
                    matches.Add(match);
                    count++;
                    node = m_Root;
                    startOffset = offset = startOffset + longestMatch.Length;
                }

                var lastMatch = node.LongestMatch;

                if(lastMatch != m_Root)
                {
                    var match = new Match(lastMatch.Id, startOffset  .. (startOffset + lastMatch.Length));
                    matches.Add(match);
                    count++;
                }

                return count;
            }
        }
    }
}
