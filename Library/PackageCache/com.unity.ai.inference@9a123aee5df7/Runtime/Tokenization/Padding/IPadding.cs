using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Padding
{
    /// <summary>
    /// Applies a padding to sequences of tokens.
    /// </summary>
    public interface IPadding
    {
        /// <summary>
        /// Applies a padding to sequences of tokens.
        /// </summary>
        /// <param name="sequences">
        /// The sequences of tokens to pad.
        /// </param>
        /// <param name="output">
        /// The target container of padded sequences of tokens.
        /// </param>
        void Pad(
            [NotNull] IReadOnlyList<IReadOnlyList<Token>> sequences,
            Output<IEnumerable<Token>> output);
    }
}
