using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.InferenceEngine.Tokenization.Truncators.Strategies
{
    /// <summary>
    /// Truncation strategy that only truncates the first sequence.
    /// The second sequence length is preserved, and the first sequence is adjusted to fit within
    /// the maximum length.
    /// </summary>
    public class OnlyFirstStrategy : ITruncationStrategy
    {
        /// <summary>
        /// Gets the singleton instance of the <see cref="OnlyFirstStrategy"/>.
        /// </summary>
        public static ITruncationStrategy Instance { get; } = new OnlyFirstStrategy();

        /// <summary>
        /// Calculates the truncation lengths for two token sequences, truncating only the first
        /// sequence.
        /// </summary>
        /// <param name="maxLength">The maximum combined length allowed for both sequences.</param>
        /// <param name="tokensA">The first sequence of tokens to be truncated.</param>
        /// <param name="tokensB">The second sequence of tokens (optional). This sequence will not
        /// be truncated.</param>
        /// <returns>A tuple containing the truncated length for sequence A and the preserved length
        /// for sequence B.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when sequence A is too short to accommodate sequence B within the maximum length,
        /// or when the calculated length for sequence A is zero while sequence A has tokens.
        /// </exception>
        public (int lengthA, int lengthB) GetTruncationLength(int maxLength,
            IReadOnlyList<Token> tokensA, IReadOnlyList<Token> tokensB)
        {
            var lengthB = tokensB?.Count ?? 0;
            var lengthA = maxLength - lengthB;
            return lengthA < 0 || tokensA.Count != 0 && lengthA == 0
                ? throw new InvalidOperationException("Sequence A too short")
                : (lengthA, lengthB);
        }
    }
}
