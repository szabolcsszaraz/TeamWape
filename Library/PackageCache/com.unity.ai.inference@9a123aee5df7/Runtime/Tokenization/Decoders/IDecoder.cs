using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Decoders
{
    /// <summary>
    /// Applies modifications to the input detokenized strings.
    /// </summary>
    public interface IDecoder
    {
        /// <summary>
        /// Applies modifications to the input detokenized strings.
        /// </summary>
        /// <param name="tokens">
        /// The string values to modify.
        /// </param>
        /// <param name="output">
        /// The recipient of modified strings.
        /// </param>
        void Decode([NotNull] IReadOnlyList<string> tokens, Output<string> output);
    }
}
