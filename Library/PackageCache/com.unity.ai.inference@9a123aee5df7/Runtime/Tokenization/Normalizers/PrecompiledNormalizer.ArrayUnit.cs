namespace Unity.InferenceEngine.Tokenization.Normalizers
{
    public partial class PrecompiledNormalizer
    {
        readonly struct ArrayUnit
        {
            readonly ulong m_Value;

            public ArrayUnit(ulong value) => m_Value = value;

            public bool HasLeaf()
            {
                return (m_Value >> 8 & 1UL) == 1UL;
            }

            public long Value()
            {
                const ulong mask = (1UL << 31) - 1UL;
                return (long) (m_Value & mask);
            }

            public ulong Label()
            {
                const ulong mask = 1UL << 31 | 0xFFUL;
                return m_Value & mask;
            }

            public ulong Offset()
            {
                var baseVal = m_Value >> 10;
                var shiftBits = (m_Value & 1UL << 9) >> 6;
                return baseVal << (int) shiftBits;
            }
        }
    }
}
