using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization.Decoders
{
    /// <summary>
    /// Decoder for "metaspace" tokenization, where spaces are represented by a
    /// special visible character (by default, U+2581 "▁").
    /// </summary>
    /// <remarks>
    /// <para>
    /// This decoder converts the metaspace replacement character back into
    /// regular spaces in the decoded string sequence.
    /// </para>
    /// <para>
    /// Behaviour depends on <see cref="PrependScheme"/>:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// If <see cref="PrependScheme.Never"/> is used, tokens are passed through
    /// unchanged.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Otherwise, occurrences of the metaspace replacement character are
    /// replaced with a leading empty string for the first token (no space
    /// prefix) and with a regular space for all subsequent tokens.
    /// </description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public class MetaspaceDecoder : IDecoder
    {
        /// <summary>
        /// The string representation of the metaspace replacement character
        /// used by this decoder.
        /// </summary>
        readonly string m_Replacement;

        /// <summary>
        /// Defines how and when to prepend spaces during decoding.
        /// </summary>
        readonly PrependScheme m_PrependScheme;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetaspaceDecoder"/> class.
        /// </summary>
        /// <param name="replacement">
        /// The character that represents a space in the metaspace-encoded tokens.
        /// Defaults to U+2581 ("▁"), which is commonly used by HuggingFace tokenizers.
        /// </param>
        /// <param name="prependScheme">
        /// The scheme that controls whether and how a leading space is inserted when
        /// decoding tokens. Defaults to <see cref="PrependScheme.Always"/>.
        /// </param>
        public MetaspaceDecoder(char replacement = '\u2581',
            PrependScheme prependScheme = PrependScheme.Always)
        {
            m_Replacement = replacement.ToString();
            m_PrependScheme = prependScheme;
        }

        /// <inheritdoc />
        public void Decode(IReadOnlyList<string> tokens, Output<string> output)
        {
            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (token.Length == 0 || token.IndexOf(m_Replacement, StringComparison.InvariantCulture) < 0)
                {
                    output.Add(token);
                    continue;
                }

                var decoded = token.Replace(m_Replacement,
                    i == 0 && m_PrependScheme != PrependScheme.Never ? "" : " ");
                output.Add(decoded);
            }
        }
    }
}
