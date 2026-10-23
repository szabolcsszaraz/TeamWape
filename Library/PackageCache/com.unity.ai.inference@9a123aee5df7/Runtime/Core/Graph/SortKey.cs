using System;

namespace Unity.InferenceEngine.Graph
{
    /// <summary>
    /// Represents a key for sorting nodes.
    /// This allows for arbitrary inserting of new sort keys between existing sort keys.
    ///
    /// The key is made up of an array of integers and keys are compared from the first index onwards.
    /// If two keys are the same for all indices in both then the key of shorter length is shorter.
    /// e.g. (0, 4, 5) &lt; (1, 2, 3) &lt; (4, -4) &lt; (4, -2) &lt; (4, -2, -5)
    ///
    /// Keys are created in the node by inserting between two existing keys.
    /// e.g. between (0, 5) and (1) is (0, 6)
    /// between (0) and (3, 2) is (3, 1)
    /// between (2, 3) and (2, 4) is (2, 3, 0)
    /// </summary>
    class SortKey : IComparable<SortKey>
    {
        int[] m_Parts;

        public int Length => m_Parts.Length;

        public SortKey(params int[] parts)
        {
            m_Parts = parts;
        }

        public int this[Index index]
        {
            get => m_Parts[index];
            set => m_Parts[index] = value;
        }

        public SortKey this[Range range] => new(m_Parts[range]);

        public static SortKey operator +(SortKey key, int suffix)
        {
            var newParts = new int[key.m_Parts.Length + 1];
            Array.Copy(key.m_Parts, newParts, key.m_Parts.Length);
            newParts[^1] = suffix;
            return new SortKey(newParts);
        }

        public int CompareTo(SortKey other)
        {
            if (other == null) return 1;
            var i = 0;
            while (i < m_Parts.Length && i < other.m_Parts.Length)
            {
                if (m_Parts[i] != other.m_Parts[i])
                    return m_Parts[i].CompareTo(other.m_Parts[i]);
                i++;
            }
            return m_Parts.Length.CompareTo(other.m_Parts.Length);
        }

        public override string ToString() => $"({string.Join(", ", m_Parts)})";
    }
}
