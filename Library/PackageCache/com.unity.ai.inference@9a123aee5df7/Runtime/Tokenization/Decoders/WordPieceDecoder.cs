using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Decoders
{
    /// <summary>
    /// An implementation of the WordPiece decoding algorithm.
    /// </summary>
    public class WordPieceDecoder : IDecoder
    {
        /// <summary>
        ///     The prefix to use for subwords that are not a beginning-of-word.
        /// </summary>
        readonly string m_Prefix;

        /// <summary>
        /// Whether to cleanup some tokenization artifacts.
        /// Cleans spaces around some punctuation like <c>.</c>, <c>?</c>, <c>!</c>, <c>,</c>,
        /// <c>'</c>, and patterns (<c>n't</c>, <c>'m</c>, <c>do not</c>, <c>'ve</c>, <c>'re</c>,
        /// <c>'s</c>);
        /// </summary>
        bool m_Cleanup;

        readonly Pool<StringBuilder> m_StringBuilderPool = new(() => new(), sb => sb.Clear());

        /// <summary>
        ///     Initializes a new instance of the <see cref="WordPieceDecoder"/> type.
        /// </summary>
        /// <param name="prefix">
        ///     The prefix to use for subwords that are not a beginning-of-word.
        /// </param>
        /// <param name="cleanup">
        ///     Whether to cleanup some tokenization artifacts.
        ///     Cleans spaces around some punctuation like <c>.</c>, <c>?</c>, <c>!</c>, <c>,</c>,
        ///     <c>'</c>, and patterns (<c>n't</c>, <c>'m</c>, <c>do not</c>, <c>'ve</c>, <c>'re</c>,
        ///     <c>'s</c>);
        /// </param>
        public WordPieceDecoder([NotNull] string prefix = "##", bool cleanup = true)
        {
            m_Prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
            m_Cleanup = cleanup;
        }

        /// <inheritdoc />
        public void Decode(IReadOnlyList<string> tokens, Output<string> output)
        {
            if (tokens == null)
                throw new ArgumentNullException(nameof(tokens));

            var decoded = tokens
                .Select((token, index) =>
                {
                    if(token is null)
                        throw new ArgumentNullException(nameof(tokens), "Cannot contain null token");

                    return index == 0
                        ? token
                        : token.StartsWith(m_Prefix)
                            ? token[m_Prefix.Length..]
                            : $" {token}";
                });

            if (m_Cleanup)
                decoded = decoded.Select(CleanUp);

            output.AddRange(decoded);
        }

        string CleanUp(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            using var _ = m_StringBuilderPool.Get(out var sb);

            return sb.Append(input)
                .Replace(" .", ".")
                .Replace(" ?", "?")
                .Replace(" !", "!")
                .Replace(" ,", ",")
                .Replace(" ' ", "'")
                .Replace(" n't", "n't")
                .Replace(" 'm", "'m")
                .Replace(" do not", " don't")
                .Replace(" 's", "'s")
                .Replace(" 've", "'ve")
                .Replace(" 're", "'re")
                .ToString();
        }
    }
}
