using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Mappers
{
    /// <summary>
    /// Turns a string input into a sequence of <see cref="Token" /> instances using the Byte-Pair
    /// Encoding strategy.
    /// </summary>
    public partial class BpeMapper : IMapper
    {
        const float k_DefaultDropout = 0;
        const bool k_DefaultIgnoreMerges = false;
        const bool k_DefaultFuseUnknown = false;
        const bool k_DefaultByteFallback = false;

        static BpeMapperOptions GetDefaultOptions(BpeMapperOptions options = default) =>
            new()
            {
                DropOut = options.DropOut ?? k_DefaultDropout,
                IgnoreMerges = options.IgnoreMerges ?? k_DefaultIgnoreMerges,
                FuseUnknown = options.FuseUnknown ?? k_DefaultFuseUnknown,
                ByteFallback = options.ByteFallback ?? k_DefaultByteFallback,
                SubWordPrefix = options.SubWordPrefix,
                WordSuffix = options.WordSuffix,
                UnknownToken = options.UnknownToken,
            };

        readonly Pool<List<Token>> m_ListOfTokenPool = new(() => new(), output => output.Clear());

        /// <summary>
        /// The converter in charge of optimizing the output of the <see cref="m_Tokenizer" /> using
        /// the merge rules.
        /// </summary>
        IManyToManyConverter<Token, Token> m_Merger;

        /// <summary>
        /// The converter in charge of turning each character of the string into an instance of
        /// <see cref="Token" /> using the given vocabulary.
        /// </summary>
        IOneToManyConverter<SubString, Token> m_Tokenizer;

        IReadOnlyDictionary<string, int> m_Vocabulary;
        IReadOnlyDictionary<int, string> m_VocabularyR;

        /// <summary>
        /// Converts a substring into a sequence of <see cref="Token" /> instances using the
        /// Byte-Pair Encoding strategy.
        /// </summary>
        /// <param name="vocabulary">
        /// The map associating token string representation with their ids.
        /// </param>
        /// <param name="merges">
        /// The list of mergeable token pairs, ordered by priority.
        /// </param>
        /// <param name="options">
        /// See <see cref="BpeMapperOptions"/>
        /// </param>
        public BpeMapper(
            [NotNull] IReadOnlyDictionary<string, int> vocabulary,
            [CanBeNull] IEnumerable<MergePair> merges = null,
            BpeMapperOptions options = default)
        {
            if (vocabulary == null)
                throw new ArgumentNullException(nameof(vocabulary));

            options = GetDefaultOptions(options);

            // building the unknown token configuration.

            if (options.UnknownToken is not null)
            {
                if (!vocabulary.ContainsKey(options.UnknownToken))
                    throw new ArgumentOutOfRangeException(
                        nameof(options.UnknownToken), options.UnknownToken, null);

            }

            // building the token decorator configuration.

            // creating the default tokenizer
            var stringToTokenSequence = new InternalTokenizer(vocabulary, options);

            // creating the merger
            var merger = BuildMerger(vocabulary, merges, options.SubWordPrefix);

            Init(vocabulary, stringToTokenSequence, merger);
        }

        /// <summary>
        /// This constructor is used for unit testing.
        /// </summary>
        /// <param name="vocabulary">
        /// The ID &lt;-> Value map.
        /// </param>
        /// <param name="tokenizer">
        /// An implementation of the string->token conversion.
        /// </param>
        /// <param name="merger">
        /// An implementation of the token merging process.
        /// </param>
        internal BpeMapper(
            Dictionary<string, int> vocabulary,
            IOneToManyConverter<SubString, Token> tokenizer,
            IManyToManyConverter<Token, Token> merger)
        {
            Init(vocabulary, tokenizer, merger);
        }

        static IManyToManyConverter<Token, Token> BuildMerger(
            IReadOnlyDictionary<string, int> vocabulary,
            IEnumerable<MergePair> merges,
            string subWordPrefix)
        {
            IManyToManyConverter<Token, Token> merger;

            // If no merge rules, returning an instance of DefaultMerger, which does nothing.
            if (merges is null)
            {
                merger = new DefaultMerger();
            }
            else
            {
                if(subWordPrefix is null)
                    subWordPrefix = string.Empty;

                var mergeDefinitions = merges.Select((pair, rank) =>
                {
                    if (!vocabulary.TryGetValue(pair.First, out var firstId))
                        throw new ArgumentException(
                            $"Token {pair.First} not found in the vocabulary", nameof(merges));

                    if (!vocabulary.TryGetValue(pair.Second, out var secondId))
                        throw new ArgumentException(
                            $"Token {pair.Second} not found in the vocabulary", nameof(merges));

                    if (subWordPrefix.Length > pair.Second.Length)
                        throw new ArgumentException($"Invalid merge rule ('{pair.First}', '{pair.Second}'): second token is shorter than the sub-word prefix.", nameof(merges));

                    var mergedValue = string.Concat(pair.First, pair.Second[subWordPrefix.Length..]);
                    if (!vocabulary.TryGetValue(mergedValue, out var mergedId))
                        throw new ArgumentException(
                            $"Merged key '{mergedValue}' not found in the vocabulary");

                    return (new Token(firstId, pair.First), new Token(secondId, pair.Second), new Token(mergedId, mergedValue), rank);
                });

                merger = new Merger(mergeDefinitions);
            }

            return merger;
        }

        /// <summary>
        /// Initializes the <see cref="BpeMapper" /> instance.
        /// </summary>
        /// <param name="vocabulary">
        /// The ID &lt;-> Value map
        /// </param>
        /// <param name="tokenizer">
        /// An implementation of the string->token conversion.
        /// </param>
        /// <param name="merger">
        /// An implementation of the token merging process.
        /// </param>
        void Init(
            IReadOnlyDictionary<string, int> vocabulary,
            IOneToManyConverter<SubString, Token> tokenizer,
            IManyToManyConverter<Token, Token> merger)
        {
            m_Vocabulary = vocabulary;
            m_VocabularyR = vocabulary.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
            m_Tokenizer = tokenizer;
            m_Merger = merger;
        }

        /// <inheritdoc />
        public void Tokenize(IReadOnlyList<SubString> inputs, Output<Token> output)
        {
            if (inputs == null)
                throw new ArgumentNullException(nameof(inputs));

            using var definitionOutputHandle = m_ListOfTokenPool.Get(out var tokenized);

            for (int sI = 0, sLimit = inputs.Count; sI < sLimit; sI++)
            {
                var input = inputs[sI];
                m_Tokenizer.Convert(input, tokenized.AsOutput());
                m_Merger.Convert(tokenized, output);
                tokenized.Clear();
            }
        }

        /// <inheritdoc />
        public string IdToToken(int id) => m_VocabularyR.GetValueOrDefault(id);

        /// <inheritdoc />
        public bool TokenToId(string token, out int id) =>
            token == null
                ? throw new ArgumentNullException(nameof(token))
                : m_Vocabulary.TryGetValue(token, out id);

        /// <inheritdoc />
        public void DeTokenize(IReadOnlyList<int> ids, bool _, Output<string> output)
        {
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            for (int idI = 0, idLimit = ids.Count; idI < idLimit; idI++)
            {
                var id = ids[idI];
                if(m_VocabularyR.TryGetValue(id, out var token))
                    output.Add(token);
            }
        }
    }
}
