using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Decoders
{
    /// <summary>
    /// Replaces a string pattern from the tokens in the list.
    /// </summary>
    public class ReplaceDecoder : IDecoder
    {
        readonly string m_Content;
        readonly string m_Pattern;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReplaceDecoder"/> type.
        /// </summary>
        /// <param name="pattern">
        /// The pattern to replace with the <paramref name="content"/>.
        /// </param>
        /// <param name="content">
        /// The content replacing the <paramref name="pattern"/> in the input string.
        /// </param>
        public ReplaceDecoder([NotNull] string pattern, [NotNull] string content)
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

                output.Add(token.Replace(m_Pattern, m_Content));
            }
        }
    }
}
