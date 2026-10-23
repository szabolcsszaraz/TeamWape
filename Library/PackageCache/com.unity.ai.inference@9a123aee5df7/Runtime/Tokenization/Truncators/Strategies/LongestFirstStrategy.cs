using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization.Truncators.Strategies
{
    /// <summary>
    /// Truncation strategy that iteratively truncates the longest sequence first.
    /// This strategy attempts to balance the lengths of both sequences by removing tokens from the
    /// longer sequence
    /// until the combined length fits within the maximum allowed length.
    /// </summary>
    public class LongestFirstStrategy : ITruncationStrategy
    {
        /// <summary>
        /// Gets the singleton instance of the <see cref="LongestFirstStrategy"/>.
        /// </summary>
        public static ITruncationStrategy Instance { get; } = new LongestFirstStrategy();

        /// <summary>
        /// Calculates the truncation lengths for two token sequences by truncating the longest
        /// sequence first.
        /// If both sequences need truncation, they will be balanced to approximately equal lengths.
        /// </summary>
        /// <param name="maxLength">The maximum combined length allowed for both sequences.</param>
        /// <param name="tokensA">The first sequence of tokens.</param>
        /// <param name="tokensB">The second sequence of tokens (optional).</param>
        /// <returns>
        /// A tuple containing the truncated lengths for both sequences.
        /// When significant truncation is needed, both sequences will be truncated to approximately
        /// half of the maximum length.
        /// </returns>
        public (int lengthA, int lengthB) GetTruncationLength(int maxLength,
            IReadOnlyList<Token> tokensA, IReadOnlyList<Token> tokensB)
        {
            var (lengthA, lengthB) = (tokensA.Count, tokensB?.Count ?? 0);

            var swap = lengthA > lengthB;
            if (swap) lengthA = lengthB;

            lengthB = lengthA > maxLength
                ? lengthA
                : Math.Max(lengthA, maxLength - lengthA);

            if (lengthA + lengthB > maxLength)
            {
                lengthA = maxLength / 2;
                lengthB = lengthA + maxLength % 2;
            }

            if (swap)
                (lengthA, lengthB) = (lengthB, lengthA);

            return (lengthA, lengthB);
        }
    }
}
