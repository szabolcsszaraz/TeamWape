using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.InferenceEngine.Tokenization.Normalizers
{
    public partial class PrecompiledNormalizer
    {
        class DoubleArray
        {
            readonly ArrayUnit[] m_Array;

            public DoubleArray(IReadOnlyList<ulong> rawArray)
            {
                m_Array = rawArray.Select(v => new ArrayUnit(v)).ToArray();
            }

            public bool CommonPrefixSearch(ReadOnlySpan<byte> key, out long result)
            {
                result = 0;
                if (m_Array.Length == 0)
                    return false;

                ulong nodePos = 0;
                var unit = m_Array[(int) nodePos];
                nodePos ^= unit.Offset();

                foreach (var c in key)
                {
                    if (c == 0)
                        break;

                    nodePos ^= c;
                    if (nodePos >= (ulong) m_Array.Length)
                        return false;

                    unit = m_Array[(int) nodePos];

                    if (unit.Label() != c)
                        return false;

                    nodePos ^= unit.Offset();
                    if (nodePos >= (ulong) m_Array.Length)
                        return false;

                    if (unit.HasLeaf())
                    {
                        var leafUnit = m_Array[(int) nodePos];
                        result = leafUnit.Value();
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
