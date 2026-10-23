using System.Collections;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization
{
    public partial class Tokenizer
    {
        class PaddingOutput : IReadOnlyList<IReadOnlyList<Token>>
        {
            readonly Pool<List<Token>> m_TokensPool;
            readonly List<List<Token>> m_Target;

            public PaddingOutput(Pool<List<Token>> tokensPool)
            {
                m_TokensPool = tokensPool;
                m_Target = new();
            }

            public IReadOnlyList<Token> this[int index] => m_Target[index];

            public Output<IEnumerable<Token>> AsOutput() => new (Add);

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

            public int Count => m_Target.Count;

            IReadOnlyList<Token> IReadOnlyList<IReadOnlyList<Token>>.this[int index] => this[index];
        }
    }
}
