using System.Collections.Generic;
using UnityEngine.Assertions;

namespace Unity.InferenceEngine.Tokenization.Padding
{
    /// <summary>
    /// Placeholder padding processor.
    /// Does not apply in padding rules.
    /// </summary>
    public class DefaultPadding : IPadding
    {
        readonly Pool<List<Token>> m_ListOfTokenPool = new(() => new(), list => list.Clear());

        /// <inheritdoc />
        public void Pad(IReadOnlyList<IReadOnlyList<Token>> sequences,
            Output<IEnumerable<Token>> output)
        {
            Assert.IsNotNull(sequences);

            using var _ = m_ListOfTokenPool.Get(out var paddedTokens);
            for (int sI = 0, sLimit = sequences.Count; sI < sLimit; sI++)
            {
                var tokens = sequences[sI];
                for (int tI = 0, tLimit = tokens.Count; tI < tLimit; tI++)
                    paddedTokens.Add(tokens[tI].SetAttention(true));

                output.Add(paddedTokens);
                paddedTokens.Clear();
            }
        }
    }
}
