using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization.Mappers
{
    public partial class UnigramMapper
    {
        class PrefixTrie
        {
            class Node
            {
                public VocabEntry Value;
                public readonly Dictionary<byte, Node> Children = new();

                public bool IsLeaf => Value != null;
            }

            readonly Node m_Root = new();

            public void Push(VocabEntry entry)
            {
                var node = m_Root;
                var bytes = entry.Bytes;
                for (var i = 0; i < bytes.Length; i++)
                {
                    var element = bytes[i];
                    if (!node.Children.TryGetValue(element, out var child))
                        node = node.Children[element] = new();
                    else
                        node = child;
                }
                node.Value = entry;
            }

            public void CommonPrefixSearch(ReadOnlySpan<byte> bytes, List<VocabEntry> output)
            {
                var node = m_Root;
                foreach (var element in bytes)
                {
                    if (!node.Children.TryGetValue(element, out var child))
                        return;

                    node = child;
                    if (node.IsLeaf)
                        output.Add(node.Value);
                }
            }
        }
    }
}
