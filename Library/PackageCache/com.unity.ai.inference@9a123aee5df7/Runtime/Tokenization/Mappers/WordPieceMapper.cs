using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Mappers
{
    /// <summary>
    /// Turns an input string into a sequence of token ids using the Word Piece strategy.
    /// </summary>
    public class WordPieceMapper : IMapper
    {
        readonly Pool<List<Token>> m_ListOfTokenPool = new(() => new(), list => list.Clear());

        readonly IReadOnlyDictionary<string ,int> m_Vocabulary;
        readonly IReadOnlyDictionary<int, string> m_VocabularyR;
        readonly IReadOnlyDictionary<string ,int> m_PrefixedVocabulary;

        readonly (string value, int id) m_UnknownToken;
        readonly int m_MaxInputCharsPerWord;

        /// <summary>
        /// Initializes a new instance of the <see cref="WordPieceMapper" /> type.
        /// </summary>
        /// <param name="vocabulary">
        /// The value->ids map for token definitions.
        /// </param>
        /// <param name="unknownToken">
        /// The value of the unknown token.
        /// </param>
        /// <param name="continuingSubWordPrefix">
        /// The prefix to add to inner subwords (not at the beginning of a word).
        /// </param>
        /// <param name="maxInputCharsPerWord">
        /// Maximum length of a tokenizable word.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="maxInputCharsPerWord" /> is negative or <c>0</c>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="vocabulary" /> cannot be <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="unknownToken" /> not found in the vocabulary.
        /// </exception>
        public WordPieceMapper(
            [NotNull] IReadOnlyDictionary<string, int> vocabulary,
            SubString unknownToken,
            [CanBeNull] string continuingSubWordPrefix = "##",
            int maxInputCharsPerWord = 100)
        {
            if (vocabulary == null)
                throw new ArgumentNullException(nameof(vocabulary));

            {
                m_Vocabulary = vocabulary;
                m_VocabularyR = vocabulary.ToDictionary(t => t.Value, t => t.Key);
                m_PrefixedVocabulary = string.IsNullOrEmpty(continuingSubWordPrefix)
                    ? null
                    : m_Vocabulary
                        .Where(kvp => kvp.Key.StartsWith(continuingSubWordPrefix))
                        .ToDictionary(kvp => kvp.Key[continuingSubWordPrefix.Length ..], kvp => kvp.Value);
            }

            if (maxInputCharsPerWord <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(maxInputCharsPerWord), maxInputCharsPerWord, null);

            if (vocabulary is null)
                throw new ArgumentNullException(nameof(vocabulary));

            if (unknownToken.IsEmpty)
                throw new ArgumentNullException(nameof(unknownToken), "Cannot be empty");

            if (!m_Vocabulary.TryGetValue(unknownToken, out var unknownId))
                throw new ArgumentException(
                    $"Cannot find the unknown token {unknownToken} in the vocabulary",
                    nameof(unknownToken));

            m_MaxInputCharsPerWord = maxInputCharsPerWord;
            m_UnknownToken = (unknownToken, unknownId);
        }

        /// <inheritdoc />
        public void Tokenize(IReadOnlyList<SubString> inputs, Output<Token> output)
        {
            if (inputs == null)
                throw new ArgumentNullException(nameof(inputs));

            for (int i = 0, iLimit = inputs.Count; i < iLimit; i++)
            {
                var input = inputs[i];

                if (input.IsNull)
                    throw new ArgumentNullException(nameof(inputs), "Cannot contain null values");

                if (input.UtfLength > m_MaxInputCharsPerWord)
                {
                    output.Add(new(m_UnknownToken.id, m_UnknownToken.value));
                    continue;
                }

                using var _ = m_ListOfTokenPool.Get(out var tokens);

                var @continue = false;
                while (input.UtfLength > 0)
                {
                    var searchInput = input;
                    var utfLength = searchInput.UtfLength;

                    var vocabulary = @continue && m_PrefixedVocabulary != null
                        ? m_PrefixedVocabulary
                        : m_Vocabulary;

                    var found = vocabulary.TryGetValue(searchInput, out var result);

                    while (!found && utfLength > 1)
                    {
                        utfLength--;
                        searchInput = input.UtfSub(.. utfLength);

                        found = vocabulary.TryGetValue(searchInput, out result);
                    }

                    if (!found)
                    {
                        tokens.Clear();
                        tokens.Add(new(m_UnknownToken.id, m_UnknownToken.value));
                        break;
                    }

                    tokens.Add(new(result, m_VocabularyR[result]));

                    if (input.UtfLength - utfLength == 0)
                        break;

                    input = input.UtfSub(utfLength ..);
                    @continue = true;
                }

                output.AddRange(tokens);
            }
        }

        /// <inheritdoc />
        public string IdToToken(int id) => m_VocabularyR.GetValueOrDefault(id);

        /// <inheritdoc />
        public bool TokenToId(string value, out int id)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return m_Vocabulary.TryGetValue(value, out id);
        }
    }
}
