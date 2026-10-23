using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Decoders
{
    /// <summary>
    /// Replaces a regex pattern from the tokens in the list.
    /// </summary>
    public class RegexReplaceDecoder : IDecoder
    {
        readonly string m_Content;
        readonly Regex m_Pattern;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegexReplaceDecoder"/> type.
        /// </summary>
        /// <param name="pattern">
        /// The pattern to replace with the <paramref name="content"/>.
        /// </param>
        /// <param name="content">
        /// The content replacing the <paramref name="pattern"/> in the input string.
        /// </param>
        public RegexReplaceDecoder([NotNull] Regex pattern, [NotNull] string content)
        {
            m_Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            m_Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        /// <inheritdoc />
        public void Decode(IReadOnlyList<string> tokens, Output<string> output)
        {
            if (tokens == null)
                throw new ArgumentNullException(nameof(tokens));

            for (int i = 0, _ = tokens.Count; i < _; i++)
            {
                var token = tokens[i];
                if(token is null)
                    throw new ArgumentNullException(nameof(tokens), "Cannot contain null token");

                output.Add(m_Pattern.Replace(token, m_Content));
            }
        }
    }
}
