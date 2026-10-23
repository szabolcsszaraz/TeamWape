using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization.Truncators.Strategies
{
    /// <summary>
    /// Defines a strategy for truncating token sequences to fit within a maximum length constraint.
    /// Implementations determine how to distribute the available length between two token sequences.
    /// </summary>
    public interface ITruncationStrategy
    {
        /// <summary>
        /// Calculates the target lengths for two token sequences after truncation to fit within a
        /// maximum length.
        /// </summary>
        /// <param name="maxLength">The maximum combined length allowed for both sequences after
        /// truncation.</param>
        /// <param name="tokensA">The first sequence of tokens to consider for truncation.</param>
        /// <param name="tokensB">The second sequence of tokens to consider for truncation. Can be
        /// <c>null</c> if only one sequence is present.</param>
        /// <returns>
        /// A tuple containing the target length for sequence A (lengthA) and the target length for
        /// sequence B (lengthB).
        /// The sum of these lengths should not exceed <paramref name="maxLength"/>.
        /// </returns>
        (int lengthA, int lengthB) GetTruncationLength(int maxLength, IReadOnlyList<Token> tokensA,
            IReadOnlyList<Token> tokensB);
    }
}
