using System.Collections.ObjectModel;

namespace Unity.InferenceEngine.Graph
{
    /// <summary>
    /// Represents a set of Nodes with O(1) storage and retrieval and deterministic ordering
    /// </summary>
    class NodeSet : KeyedCollection<Node, Node>
    {
        protected override Node GetKeyForItem(Node item)
        {
            return item;
        }

        public bool TryAdd(Node node)
        {
            if (Contains(node))
                return false;
            Add(node);
            return true;
        }
    }
}
