using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization
{
    /// <summary>
    /// Contains the result of a tokenization pipeline ran by a <see cref="Tokenizer" /> instance.
    /// </summary>
    public class Encoding : IEncoding
    {
        /// <inheritdoc cref="IEncoding.GetTokens" />
        readonly Token[] m_Tokens;

        /// <inheritdoc cref="IEncoding.Overflow" />
        Encoding m_Overflow;

        /// <summary>
        /// Initializes a new instance of the <see cref="Encoding" /> type.
        /// </summary>
        /// <param name="tokens">
        /// The tokens.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="tokens" /> cannot be null.
        /// </exception>
        internal Encoding([NotNull] Token[] tokens) => m_Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));

        /// <inheritdoc />
        public int Length => m_Tokens.Length;

        /// <inheritdoc />
        public int GetTokens(ICollection<Token> output)
        {
            foreach (var t in m_Tokens)
                output.Add(t);
            return m_Tokens.Length;
        }

        /// <inheritdoc />
        public int GetIds(ICollection<int> output)
        {
            foreach (var token in m_Tokens)
                output.Add(token.Id);
            return m_Tokens.Length;
        }

        /// <inheritdoc />
        public int GetValues(ICollection<string> output)
        {
            foreach (var token in m_Tokens)
                output.Add(token.Value);
            return m_Tokens.Length;
        }

        /// <inheritdoc />
        public int GetAttentionMask(ICollection<int> output)
        {
            foreach (var token in m_Tokens)
                output.Add(token.Attention ? 1 : 0);
            return m_Tokens.Length;
        }

        /// <inheritdoc />
        public int GetTypeIds(ICollection<int> output)
        {
            foreach (var token in m_Tokens)
                output.Add(token.TypeId);
            return m_Tokens.Length;
        }

        /// <inheritdoc />
        public int GetSpecialMask(ICollection<int> output)
        {
            foreach (var token in m_Tokens)
                output.Add(token.Special ? 1 : 0);
            return m_Tokens.Length;
        }

        /// <inheritdoc />
        public int GetOffsets(ICollection<Range> output)
        {
            foreach (var token in m_Tokens)
                output.Add(token.Offsets);
            return m_Tokens.Length;
        }

        /// <inheritdoc />
        public IEncoding Overflow => m_Overflow;

        /// <summary>
        /// Sets the next encoding instance storing the overflowing tokens.
        /// </summary>
        /// <param name="overflow">
        /// The encoding storing the overflowing tokens.
        /// </param>
        /// <returns>
        /// <see langword="this" />
        /// </returns>
        internal Encoding SetOverflow(Encoding overflow)
        {
            m_Overflow = overflow;
            return this;
        }
    }
}
