using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization
{
    /// <summary>
    /// Describes the result of a tokenization pipeline execution.
    /// </summary>
    public interface IEncoding
    {
        /// <summary>
        /// The number of tokens.
        /// </summary>
        int Length { get; }

        /// <summary>
        /// In case the tokenization pipeline produces more tokens than the expected size, the
        /// following tokens are stored into another <see cref="IEncoding" /> instance.
        /// This overflow can also define its own overflow, similarly to a linked list.
        /// </summary>
        IEncoding Overflow { get; }

        /// <summary>
        /// The list of tokens.
        /// </summary>
        /// <param name="output">
        /// The target container of tokens.
        /// </param>
        /// <returns>
        /// The number of available tokens.
        /// </returns>
        int GetTokens([NotNull] ICollection<Token> output);

        /// <summary>
        /// The list of token ids.
        /// </summary>
        /// <param name="output">
        /// The target container of ids.
        /// </param>
        /// <returns>
        /// The number of available tokens.
        /// </returns>
        int GetIds([NotNull] ICollection<int> output);

        /// <summary>
        /// The list of token ids.
        /// </summary>
        /// <param name="output">
        /// The target container of values.
        /// </param>
        /// <returns>
        /// The number of available tokens.
        /// </returns>
        int GetValues([NotNull] ICollection<string> output);

        /// <summary>
        /// The attention mask.
        /// When a tokenization requires truncation and padding, this mask indicates which tokens
        /// are the most relevant.
        /// </summary>
        /// <param name="output">
        /// The target container of attention state.
        /// </param>
        /// <returns>
        /// The number of available tokens.
        /// </returns>
        int GetAttentionMask([NotNull] ICollection<int> output);

        /// <summary>
        /// The type ids.
        /// </summary>
        /// <param name="output">
        /// The target container of type ids.
        /// </param>
        /// <returns>
        /// The number of available tokens.
        /// </returns>
        int GetTypeIds([NotNull] ICollection<int> output);

        /// <summary>
        /// The special tokens mask
        /// </summary>
        /// <param name="output">
        /// The target container of special states.
        /// </param>
        /// <returns>
        /// The number of available tokens.
        /// </returns>
        int GetSpecialMask([NotNull] ICollection<int> output);

        /// <summary>
        /// The token offsets.
        /// </summary>
        /// <param name="output">
        /// The target container of offsets.
        /// </param>
        /// <returns>
        /// The number of available tokens.
        /// </returns>
        int GetOffsets([NotNull] ICollection<Range> output);

        /// <summary>
        /// Presents encodings one by one, starting by the main, then overflowing sequences.
        /// </summary>
        /// <returns>
        /// Main encoding followed by its overflowing sequences.
        /// </returns>
        public IEnumerable<IEncoding> GetEncodings()
        {
            var encoding = this;
            while (encoding is not null)
            {
                yield return encoding;
                encoding = encoding.Overflow;
            }
        }

        /// <inheritdoc cref="GetTokens(System.Collections.Generic.ICollection{Unity.InferenceEngine.Tokenization.Token})"/>
        public IReadOnlyList<Token> GetTokens()
        {
            var tokens = new List<Token>();
            GetTokens(tokens);
            return tokens;
        }

        /// <inheritdoc cref="GetIds(System.Collections.Generic.ICollection{int})"/>
        public IReadOnlyList<int> GetIds()
        {
            var ids = new List<int>();
            GetIds(ids);
            return ids;
        }

        /// <inheritdoc cref="GetValues(System.Collections.Generic.ICollection{string})"/>
        public IReadOnlyList<string> GetValues()
        {
            var values = new List<string>();
            GetValues(values);
            return values;
        }

        /// <inheritdoc cref="GetAttentionMask(System.Collections.Generic.ICollection{int})"/>
        public IReadOnlyList<int> GetAttentionMask()
        {
            var attentions = new List<int>();
            GetAttentionMask(attentions);
            return attentions;
        }

        /// <inheritdoc cref="GetTypeIds(System.Collections.Generic.ICollection{int})"/>
        public IReadOnlyList<int> GetTypeIds()
        {
            var typeIds = new List<int>();
            GetTypeIds(typeIds);
            return typeIds;
        }

        /// <inheritdoc cref="GetSpecialMask(System.Collections.Generic.ICollection{int})"/>
        public IReadOnlyList<int> GetSpecialMask()
        {
            var specialMask = new List<int>();
            GetSpecialMask(specialMask);
            return specialMask;
        }

        /// <inheritdoc cref="GetOffsets(System.Collections.Generic.ICollection{System.Range})"/>
        public IReadOnlyList<Range> GetOffsets()
        {
            var offsets = new List<Range>();
            GetOffsets(offsets);
            return offsets;
        }
    }
}
