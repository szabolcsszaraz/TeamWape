using System;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// Fenwick (binary indexed) tree over row heights. O(log N) height updates, prefix sums
    /// (total height above a row) and offset-to-row search.
    /// </summary>
    class CumulativeHeightTree
    {
        // Heights and partial sums are kept in double so prefix sums over thousands of rows do not drift.
        double[] m_Tree;
        double[] m_Heights;
        int m_Count;

        public CumulativeHeightTree(int count)
        {
            Resize(count);
        }

        public int Count => m_Count;

        public float Total => m_Count == 0 ? 0f : (float)PrefixSumInclusive(m_Count);

        public void Resize(int count)
        {
            if (count < 0)
                count = 0;

            var newHeights = new double[count];
            var copy = Math.Min(count, m_Heights?.Length ?? 0);
            for (var i = 0; i < copy; i++)
                newHeights[i] = m_Heights[i];

            m_Heights = newHeights;
            m_Tree = new double[count + 1];
            m_Count = count;

            for (var i = 0; i < count; i++)
                Add(i + 1, m_Heights[i]);
        }

        public void SetHeight(int index, float height)
        {
            if (index < 0 || index >= m_Count)
                return;

            var delta = height - m_Heights[index];
            if (delta == 0d)
                return;

            m_Heights[index] = height;
            Add(index + 1, delta);
        }

        public float GetHeight(int index)
            => index < 0 || index >= m_Count ? 0f : (float)m_Heights[index];

        public float PrefixSum(int index)
        {
            if (index <= 0)
                return 0f;

            if (index > m_Count)
                index = m_Count;

            return (float)PrefixSumInclusive(index);
        }

        public float RangeHeight(int from, int toExclusive)
        {
            if (from < 0)
                from = 0;

            if (toExclusive > m_Count)
                toExclusive = m_Count;

            if (toExclusive <= from)
                return 0f;

            return (float)(PrefixSumInclusive(toExclusive) - PrefixSumInclusive(from));
        }

        public int FindIndexAtOffset(float offset)
        {
            if (m_Count == 0)
                return 0;

            if (offset < 0f)
                return 0;

            var pos = 0;
            var remaining = (double)offset;
            var logN = 1;
            while (1 << (logN + 1) <= m_Count)
                logN++;

            for (var pow = 1 << logN; pow > 0; pow >>= 1)
            {
                var next = pos + pow;
                if (next <= m_Count && m_Tree[next] <= remaining)
                {
                    pos = next;
                    remaining -= m_Tree[next];
                }
            }

            return Math.Min(pos, m_Count - 1);
        }

        void Add(int oneBasedIndex, double delta)
        {
            for (var i = oneBasedIndex; i <= m_Count; i += i & -i)
                m_Tree[i] += delta;
        }

        double PrefixSumInclusive(int oneBasedCount)
        {
            var sum = 0d;
            for (var i = oneBasedCount; i > 0; i -= i & -i)
                sum += m_Tree[i];
            return sum;
        }
    }
}
