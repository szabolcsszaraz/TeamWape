using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Decoders
{
    /// <summary>
    /// Decoder for Byte Pair Encoding (BPE) tokens that removes suffix markers
    /// and restores spaces between words.
    /// </summary>
    /// <remarks>
    /// This decoder processes BPE tokens by replacing the suffix marker with spaces
    /// to reconstruct the original text. The last token does not receive a trailing space.
    /// </remarks>
    public class BpeDecoder : IDecoder
    {
        readonly string m_Pattern;

        /// <summary>
        /// Initializes a new instance of the <see cref="BpeDecoder"/> class with the specified
        /// suffix.
        /// </summary>
        /// <param name="pattern">
        /// The suffix marker to be replaced during decoding. Default value is "&lt;/w&gt;".
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="pattern"/> is null.
        /// </exception>
        public BpeDecoder([NotNull] string pattern = "</w>")
        {
            if (string.IsNullOrEmpty(pattern))
                throw new ArgumentNullException(nameof(pattern));
            m_Pattern = pattern;
        }

        /// <inheritdoc />
        public void Decode(IReadOnlyList<string> tokens, Output<string> output)
        {
            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (string.IsNullOrEmpty(token))
                {
                    output.Add(string.Empty);
                    continue;
                }

                var replacement = i == tokens.Count - 1 ? "" : " ";
                if (replacement == m_Pattern
                    || token.IndexOf(m_Pattern, StringComparison.Ordinal) == -1)
                    output.Add(token);
                else
                    output.Add(token.Replace(m_Pattern, replacement));
            }
        }
    }
}
