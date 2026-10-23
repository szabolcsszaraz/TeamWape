using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization
{
    /// <summary>
    /// The high level API of a tokenization/detokenization pipeline.
    /// </summary>
    public interface ITokenizer
    {
        /// <summary>
        /// Turns <paramref name="inputA" />, optionally <paramref name="inputB" /> into an
        /// <see cref="IEncoding" /> instance.
        /// </summary>
        /// <param name="inputA">
        /// The main input to tokenize. Cannot be null.
        /// </param>
        /// <param name="inputB">
        /// A optional, secondary input to tokenize.
        /// </param>
        /// <param name="addSpecialTokens">
        /// Tells whether special tokens must be added to the final <see cref="IEncoding" />.
        /// </param>
        /// <returns>
        /// The tokenized value as an <see cref="IEncoding" /> instance.
        /// </returns>
        IEncoding Encode(
            [NotNull] string inputA,
            [CanBeNull] string inputB = null,
            bool addSpecialTokens = true);

        /// <summary>
        /// Turns a sequence of token ids into a string.
        /// </summary>
        /// <param name="input">
        /// The sequence of token ids.
        /// </param>
        /// <param name="skipSpecialTokens">
        /// Do not decode the special tokens.
        /// </param>
        /// <returns>
        /// The decoded string.
        /// </returns>
        public string Decode([NotNull] IReadOnlyList<int> input, bool skipSpecialTokens = false);
    }
}
