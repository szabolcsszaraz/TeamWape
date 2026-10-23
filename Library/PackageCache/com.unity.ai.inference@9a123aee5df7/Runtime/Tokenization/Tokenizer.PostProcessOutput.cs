using System.Collections;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization
{
    class PostProcessOutput : IReadOnlyList<IReadOnlyList<IReadOnlyList<Token>>>
    {
        readonly Pool<List<List<Token>>> m_SequencePool;
        readonly Pool<List<Token>> m_TokensPool;

        readonly List<List<List<Token>>> m_Target;

        public PostProcessOutput(
            Pool<List<List<Token>>> sequencePool,
            Pool<List<Token>> tokensPool)
        {
            m_SequencePool = sequencePool;
            m_TokensPool = tokensPool;

            m_Target = new();
        }

        public IReadOnlyList<IReadOnlyList<Token>> this[int index] => m_Target[index];

        public int Count => m_Target.Count;

        public Output<IEnumerable<IEnumerable<Token>>> AsOutput() => new(Add);

        void Add(IEnumerable<IEnumerable<Token>> sequence)
        {
            var sequenceCopy = m_SequencePool.Get();
            foreach (var tokens in sequence)
            {
                var tokensCopy = m_TokensPool.Get();
                tokensCopy.AddRange(tokens);
                sequenceCopy.Add(tokensCopy);
            }

            m_Target.Add(sequenceCopy);
        }

        public IEnumerator<IReadOnlyList<IReadOnlyList<Token>>> GetEnumerator() =>
            m_Target.GetEnumerator();

        public void Add(IEnumerable<IEnumerable<IEnumerable<Token>>> sequences)
        {
            foreach (var sequence in sequences)
                Add(sequence);
        }

        public void Reset()
        {
            foreach (var sequence in m_Target)
                m_SequencePool.Release(sequence);
            m_Target.Clear();
        }

        IEnumerator<IReadOnlyList<IReadOnlyList<Token>>>
            IEnumerable<IReadOnlyList<IReadOnlyList<Token>>>.GetEnumerator() =>
            GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        IReadOnlyList<IReadOnlyList<Token>> IReadOnlyList<IReadOnlyList<IReadOnlyList<Token>>>.
            this[int index] =>
            this[index];
    }
}
