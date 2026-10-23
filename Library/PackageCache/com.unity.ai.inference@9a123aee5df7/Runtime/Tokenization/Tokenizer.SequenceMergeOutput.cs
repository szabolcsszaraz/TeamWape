using System.Collections;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization
{
    public partial class Tokenizer
    {
        class SequenceMergeOutput : IReadOnlyList<IReadOnlyList<Token>>
        {
            readonly Pool<List<Token>> m_TokensPool;
            List<List<Token>> m_Target;

            public SequenceMergeOutput(Pool<List<Token>> tokensPool)
            {
                m_TokensPool = tokensPool;
                m_Target = new();
            }

            public IReadOnlyList<Token> this[int index] => m_Target[index];

            public int Count => m_Target.Count;

            public Output<IEnumerable<Token>> AsOutput()
            {
                return new Output<IEnumerable<Token>>(Add);
            }

            void Add(IEnumerable<Token> tokens)
            {
                var copy = m_TokensPool.Get();
                copy.AddRange(tokens);
                m_Target.Add(copy);
            }

            public IEnumerator<IReadOnlyList<Token>> GetEnumerator() => m_Target.GetEnumerator();

            public void Reset()
            {
                foreach (var tokens in m_Target)
                    m_TokensPool.Release(tokens);
                m_Target.Clear();
            }

            IEnumerator<IReadOnlyList<Token>> IEnumerable<IReadOnlyList<Token>>.GetEnumerator() =>
                GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            IReadOnlyList<Token> IReadOnlyList<IReadOnlyList<Token>>.this[int index] => this[index];
        }
    }
}
