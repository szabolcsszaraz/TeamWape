using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Graph
{
    /// <summary>
    /// Represents a special dictionary for quick retrieval of function nodes by their op target.
    /// </summary>
    class FindNodesLookupTable
    {
        // Maps a tuple (op, target) to a NodeSet for fast retrieval of e.g. all input nodes or all 'Conv' op nodes
        Dictionary<(string, string), NodeSet> m_Table = new();

        static (string, string) Key(Node node)
        {
            return (node.op, node.op == Node.kOpCallFunction ? node.target : null);
        }

        public bool Contains(Node node)
        {
            return m_Table.ContainsKey(Key(node));
        }

        public void Insert(Node node)
        {
            var key = Key(node);
            if (!m_Table.TryGetValue(key, out var nodeSet))
            {
                nodeSet = new NodeSet();
                m_Table[key] = nodeSet;
            }
            nodeSet.TryAdd(node);
        }

        public void Remove(Node node)
        {
            var key = Key(node);
            if (m_Table.TryGetValue(key, out var nodes))
                nodes.Remove(node);
        }

        public List<Node> FindNodes(string op, string target)
        {
            var nodeList = new List<Node>();
            if (m_Table.TryGetValue((op, target), out var nodes))
            {
                foreach (var node in nodes)
                    nodeList.Add(node);
            }

            return nodeList;
        }
    }
}
