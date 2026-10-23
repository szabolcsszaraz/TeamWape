using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.InferenceEngine.Tokenization.Truncators.Strategies
{
    /// <summary>
    /// Truncation strategy that only truncates the second sequence.
    /// The first sequence length is preserved, and the second sequence is adjusted to fit within
    /// the maximum length.
    /// </summary>
    public class OnlySecondStrategy : ITruncationStrategy
    {
        /// <summary>
        /// Gets the singleton instance of the <see cref="OnlySecondStrategy"/>.
        /// </summary>
        public static ITruncationStrategy Instance { get; } = new OnlySecondStrategy();

        /// <summary>
        /// Calculates the truncation lengths for two token sequences, truncating only the second
        /// sequence.
        /// </summary>
        /// <param name="maxLength">The maximum combined length allowed for both sequences.</param>
        /// <param name="tokensA">The first sequence of tokens. This sequence will not be truncated.
        /// </param>
        /// <param name="tokensB">The second sequence of tokens to be truncated (optional).</param>
        /// <returns>A tuple containing the preserved length for sequence A and the truncated length
        /// for sequence B.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when sequence B cannot be truncated to fit within the maximum length after
        /// preserving sequence A, or when the calculated length for sequence B is zero while
        /// sequence B has tokens.
        /// </exception>
        public (int lengthA, int lengthB) GetTruncationLength(int maxLength,
            IReadOnlyList<Token> tokensA,
            IReadOnlyList<Token> tokensB)
        {
            var lengthA = tokensA.Count;
            var lengthB = maxLength - lengthA;
            return lengthB < 0 || (tokensB?.Count ?? 0) != 0 && lengthB == 0
                ? throw new InvalidOperationException("Cannot truncate sequence B")
                : (lengthA, lengthB);
        }
    }
}
