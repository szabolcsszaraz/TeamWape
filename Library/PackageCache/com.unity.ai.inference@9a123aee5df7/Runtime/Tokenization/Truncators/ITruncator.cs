using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.InferenceEngine.Tokenization.PostProcessors;

namespace Unity.InferenceEngine.Tokenization.Truncators
{
    /// <summary>
    /// Splits sequences of tokens into smaller collection of sequences.
    /// </summary>
    public interface ITruncator
    {
        /// <summary>
        /// Splits sequences of tokens into smaller collection of sequences.
        /// </summary>
        /// <param name="inputA">
        /// The primary sequence of tokens (mandatory).
        /// </param>
        /// <param name="inputB">
        /// The optional secondary sequence of tokens.
        /// </param>
        /// <param name="numAddedTokens">
        /// The number of tokens that the <see cref="IPostProcessor" /> steps will add.
        /// </param>
        /// <param name="outputA">
        /// The target container of the truncated subsequences of <paramref name="inputA" />.
        /// </param>
        /// <param name="outputB">
        /// The target container of the truncated subsequences of <paramref name="inputB" />.
        /// </param>
        void Truncate(
            [NotNull] IReadOnlyList<Token> inputA,
            [CanBeNull] IReadOnlyList<Token> inputB,
            int numAddedTokens,
            Output<IEnumerable<Token>> outputA,
            Output<IEnumerable<Token>> outputB);
    }
}
