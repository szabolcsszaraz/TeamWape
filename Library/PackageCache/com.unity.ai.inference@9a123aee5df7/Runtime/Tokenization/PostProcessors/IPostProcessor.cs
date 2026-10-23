using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.InferenceEngine.Tokenization.Mappers;

namespace Unity.InferenceEngine.Tokenization.PostProcessors
{
    /// <summary>
    /// Transforms the sequences of tokens from the truncated output of <see cref="IMapper" /> and
    /// merges it into a single sequence.
    /// </summary>
    public interface IPostProcessor
    {
        /// <summary>
        /// Determines the number of tokens that this <see cref="IPostProcessor" /> will add to the
        /// sequence of tokens.
        /// </summary>
        /// <param name="isPair">
        /// Tells if we want the number of added tokens for a pair of sequences of tokens
        /// (<see langword="true" />), of a single sequence (<see langword="false" />).
        /// </param>
        /// <returns>
        /// Number of tokens that this <see cref="IPostProcessor" /> will add to the sequence of
        /// tokens
        /// </returns>
        int GetNumAddedTokens(bool isPair);

        /// <summary>
        /// Processes the sequence of tokens.
        /// </summary>
        /// <param name="sequenceA">
        /// The single, or first sequence of tokens.
        /// </param>
        /// <param name="sequenceB">
        /// The second sequence of a pair.
        /// </param>
        /// <param name="addSpecialTokens">
        /// Tells whether adding a special tokens in the result sequences.
        /// </param>
        /// <param name="output">
        /// The recipient of processed sequences.
        /// </param>
        void PostProcess(
            [NotNull] IReadOnlyList<IReadOnlyList<Token>> sequenceA,
            [CanBeNull] IReadOnlyList<IReadOnlyList<Token>> sequenceB,
            bool addSpecialTokens,
            Output<IEnumerable<IEnumerable<Token>>> output);
    }
}
