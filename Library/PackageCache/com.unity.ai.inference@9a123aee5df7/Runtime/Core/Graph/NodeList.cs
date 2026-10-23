using System.Collections;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Graph
{
    /// <summary>
    /// Represents an object for iterating through the nodes in a graph in order.
    /// </summary>
    class NodeList : IEnumerable<Node>
    {
        Graph m_Graph;
        Direction m_Direction;

        public enum Direction
        {
            Next,
            Prev
        }

        public NodeList(Graph graph, Direction direction = Direction.Next)
        {
            m_Graph = graph;
            m_Direction = direction;
        }

        public int Length => m_Graph.len;

        public IEnumerator<Node> GetEnumerator()
        {
            var current = m_Direction == Direction.Prev ? m_Graph.root.prev : m_Graph.root.next;

            while (current != m_Graph.root)
            {
                yield return current;
                current = m_Direction == Direction.Prev ? current.prev : current.next;
            }
        }

        public IEnumerable<Node> GetReversed()
        {
            var current = m_Direction == Direction.Prev ? m_Graph.root.next : m_Graph.root.prev;

            while (current != m_Graph.root)
            {
                yield return current;
                current = m_Direction == Direction.Prev ? current.next : current.prev;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
