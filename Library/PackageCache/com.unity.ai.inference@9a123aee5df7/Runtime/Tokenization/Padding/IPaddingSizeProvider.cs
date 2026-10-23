using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Padding
{
    /// <summary>
    /// Computes the size of the padded sequence of tokens.
    /// </summary>
    public interface IPaddingSizeProvider
    {
        /// <summary>
        /// Computes the size of the padded sequence of tokens.
        /// </summary>
        /// <param name="sizes">
        /// The size of all the sequences of tokens to pad.
        /// </param>
        /// <returns>
        /// The size of the padded sequence of tokens.
        /// </returns>
        int GetPaddingSize([NotNull] IReadOnlyList<int> sizes);
    }
}
