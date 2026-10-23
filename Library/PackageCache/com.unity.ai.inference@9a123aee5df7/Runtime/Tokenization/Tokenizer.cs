using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Unity.InferenceEngine.Tokenization.Decoders;
using Unity.InferenceEngine.Tokenization.Mappers;
using Unity.InferenceEngine.Tokenization.Normalizers;
using Unity.InferenceEngine.Tokenization.Padding;
using Unity.InferenceEngine.Tokenization.PostProcessors;
using Unity.InferenceEngine.Tokenization.PreTokenizers;
using Unity.InferenceEngine.Tokenization.Truncators;

namespace Unity.InferenceEngine.Tokenization
{
    /// <summary>
    /// This type is the entry point of the tokenization/detokenization pipeline.
    /// The pipeline is composed of six steps, and turns an input string into an
    /// <see cref="IEncoding" /> chain:
    /// <list type="number">
    ///     <item>
    ///         <term>Pre-tokenization</term>
    ///         <description>
    ///             Splits the result of the normalization step into small pieces (example: split by
    ///             whitespace).
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Encoding</term>
    ///         <description>
    ///             Central step of the tokenization, this one turns each piece from the
    ///             pre-tokenization process into sequence of <see cref="int" /> ids.
    ///             See <see cref="IMapper" /> for more details.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Truncation</term>
    ///         <description>
    ///             Splits the sequence of ids from the encoding step into smaller subsequences.
    ///             The most frequent truncation rule in "max length".
    ///             See <see cref="ITruncator" /> for more details.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Postprocessing</term>
    ///         <description>
    ///             Transforms each subsequences of generated from the truncation.
    ///             The most common transformation is adding <c>[CLS]</c> and <c>[SEP]</c> tokens
    ///             before and after the sequence.
    ///             See <see cref="IPostProcessor" /> for more details.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Padding</term>
    ///         <description>
    ///             Pads each subsequence from the postprocessing to match the expected sequence
    ///             size.
    ///         </description>
    ///     </item>
    /// </list>
    /// </summary>
    public partial class Tokenizer : ITokenizer
    {
        readonly Pool<List<Token>> m_ListOfTokenPool = new(() => new(), list => list.Clear());
        readonly Pool<List<List<Token>>> m_ListOfListOfTokenPool = new(() => new(), list => list.Clear());
        readonly Pool<List<string>> m_ListOfStringPool = new(() => new(), list => list.Clear());
        readonly Pool<List<SubString>> m_ListOfSubStringPool = new(() => new(), list => list.Clear());
        readonly Pool<List<(int? id, Range offsets)>> m_ListOfChunkPool = new(() => new(), list => list.Clear());

        readonly Pool<TruncationOutput> m_TruncationOutputPool;
        readonly Pool<PostProcessOutput> m_PostProcessOutputPool;
        readonly Pool<SequenceMergeOutput> m_SequenceMergeOutputPool;
        readonly Pool<PaddingOutput> m_PaddingOutputPool;

        readonly AddedVocabulary m_AddedVocabulary;

        readonly IMapper m_Mapper;
        readonly INormalizer m_Normalizer;
        readonly IPreTokenizer m_PreTokenizer;

        readonly IPadding m_Padding;

        readonly IPostProcessor m_PostProcessor;
        readonly ITruncator m_Truncator;
        readonly IDecoder m_Decoder;

        readonly SequenceMerger m_SequenceMerger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Tokenizer" /> type.
        /// </summary>
        /// <param name="mapper">
        /// The <see cref="IMapper" /> encoding to use to turn the strings into tokens.
        /// </param>
        /// <param name="normalizer">
        /// Normalizes portions of the input.
        /// </param>
        /// <param name="preTokenizer">
        /// The pre-tokenization rules.
        /// </param>
        /// <param name="postProcessor">
        /// The post-processing of the token sequence.
        /// See <see cref="IPostProcessor" />.
        /// </param>
        /// <param name="truncator">
        /// The truncation rules.
        /// See <see cref="ITruncator" />.
        /// </param>
        /// <param name="paddingProcessor">
        /// The padding rules.
        /// </param>
        /// <param name="decoder">
        /// Modifiers applied to the decoded token sequence.
        /// </param>
        /// <param name="addedVocabulary">
        /// Special token configurations.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="mapper" /> cannot be <see langword="null" />.
        /// </exception>
        public Tokenizer(
            [NotNull] IMapper mapper,
            [CanBeNull] INormalizer normalizer = null,
            [CanBeNull] IPreTokenizer preTokenizer = null,
            [CanBeNull] IPostProcessor postProcessor = null,
            [CanBeNull] ITruncator truncator = null,
            [CanBeNull] IPadding paddingProcessor = null,
            [CanBeNull] IDecoder decoder = null,
            [CanBeNull] IEnumerable<TokenConfiguration> addedVocabulary = null)
        {
            // Pools initialization
            {
                m_TruncationOutputPool =
                    new(() => new(m_ListOfTokenPool), output => output.Reset());

                m_PostProcessOutputPool = new(
                    () => new(m_ListOfListOfTokenPool, m_ListOfTokenPool),
                    output => output.Reset());

                m_SequenceMergeOutputPool = new(
                    () => new(m_ListOfTokenPool), output => output.Reset());

                m_PaddingOutputPool = new(() => new(m_ListOfTokenPool), output => output.Reset());
            }

            m_Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));

            m_Normalizer = normalizer ?? new DefaultNormalizer();
            m_PreTokenizer = preTokenizer ?? new DefaultPreTokenizer();
            m_PostProcessor = postProcessor ?? new DefaultPostProcessor();
            m_Truncator = truncator ?? new DefaultTruncator();
            m_Padding = paddingProcessor ?? new DefaultPadding();
            m_Decoder = decoder ?? new DefaultDecoder();

            m_AddedVocabulary = new(addedVocabulary, m_Normalizer, false);

            m_SequenceMerger = new(m_ListOfTokenPool);
        }

        /// <inheritdoc cref="ITokenizer.Encode" />
        public IEncoding Encode(
            string inputA,
            string inputB = null,
            bool addSpecialTokens = true)
        {
            if (inputA == null)
                throw new ArgumentNullException(nameof(inputA));

            var isPair = inputB is not null;

            // 1. Tokenization
            var sequenceAHandle = m_ListOfTokenPool.Get(out var sequenceA);
            var sequenceBHandle = m_ListOfTokenPool.Get(out var sequenceB);

            try
            {
                TokenizeInput(inputA, 0, sequenceA.AsOutput());

                if (isPair)
                    TokenizeInput(inputB, 1, sequenceB.AsOutput());
            }
            catch (Exception)
            {
                sequenceAHandle.Dispose();
                sequenceBHandle.Dispose();
                throw;
            }

            // 2. Truncation
            var truncatedAHandle = m_TruncationOutputPool.Get(out var truncatedA);
            var truncatedBHandle = m_TruncationOutputPool.Get(out var truncatedB);

            try
            {
                Truncate(sequenceA, isPair ? sequenceB : null, addSpecialTokens,
                    truncatedA.AsOutput(), truncatedB.AsOutput());
            }
            catch (Exception)
            {
                truncatedAHandle.Dispose();
                truncatedBHandle.Dispose();
                throw;
            }
            finally
            {
                sequenceAHandle.Dispose();
                sequenceBHandle.Dispose();
                sequenceA = null;
                sequenceB = null;
            }

            // 3. Post Processing
            var postProcessOutputHandle = m_PostProcessOutputPool.Get(out var postProcessOutput);

            try
            {
                m_PostProcessor.PostProcess(truncatedA, isPair ? truncatedB : null,
                    addSpecialTokens, postProcessOutput.AsOutput());
            }
            catch (Exception)
            {
                postProcessOutputHandle.Dispose();
                throw;
            }
            finally
            {
                truncatedAHandle.Dispose();
                truncatedBHandle.Dispose();
                truncatedA = null;
                truncatedB = null;
            }

            var sequenceMergeHandle = m_SequenceMergeOutputPool.Get(out var sequenceMerge);

            try
            {
                m_SequenceMerger.Merge(postProcessOutput, sequenceMerge.AsOutput());
            }
            catch (Exception)
            {
                sequenceMergeHandle.Dispose();
                throw;
            }
            finally
            {
                postProcessOutputHandle.Dispose();
                postProcessOutput = null;
            }

            // 4. Padding
            var paddingOutputHandle = m_PaddingOutputPool.Get(out var paddingOutput);

            try
            {
                m_Padding.Pad(sequenceMerge, paddingOutput.AsOutput());
            }
            catch (Exception)
            {
                paddingOutputHandle.Dispose();
                throw;
            }
            finally
            {
                sequenceMergeHandle.Dispose();
                sequenceMerge = null;
            }

            Encoding head = null;

            try
            {
                Encoding parent = null;

                foreach (var tokens in paddingOutput)
                {
                    var encoding = new Encoding(tokens.ToArray());

                    if (parent is not null)
                        parent.SetOverflow(encoding);
                    else
                        head = encoding;

                    parent = encoding;
                }
            }
            finally
            {
                paddingOutputHandle.Dispose();
                paddingOutput = null;
            }

            return head;

            void TokenizeInput(string input, int typeId,  Output<Token> output)
            {
                using var nonNormalizedChunksHandle = m_ListOfChunkPool.Get(out var nonNormalizedChunks);
                m_AddedVocabulary.Split(input, false, nonNormalizedChunks.AsOutput());

                for (var i = 0; i < nonNormalizedChunks.Count; i++)
                {
                    var (id, offsets) = nonNormalizedChunks[i];
                    if (id.HasValue)
                    {
                        var token = m_AddedVocabulary.TryGetConfiguration(id.Value, out var config)
                            ? config.Value
                            : m_Mapper.IdToToken(id.Value);
                        output.Add(new(id.Value, value: token, typeId: typeId));
                        continue;
                    }

                    var normalizableChunk = new SubString(input, offsets);
                    var normalizedChunk = m_Normalizer.Normalize(normalizableChunk);

                    using var chunksHandle = m_ListOfChunkPool.Get(out var chunks);
                    m_AddedVocabulary.Split(normalizedChunk, true, chunks.AsOutput());

                    for (int j = 0; j < chunks.Count; j++)
                    {
                        (id, offsets) = chunks[j];

                        if (id.HasValue)
                        {
                            var token =
                                m_AddedVocabulary.TryGetConfiguration(id.Value, out var config)
                                    ? config.Value
                                    : m_Mapper.IdToToken(id.Value);
                            output.Add(new(id.Value, value: token, typeId: typeId));
                            continue;
                        }

                        var preTokenizableChunk = normalizedChunk[offsets];
                        using var preTokenizedOutputHandle =
                            m_ListOfSubStringPool.Get(out var preTokenizedOutput);
                        m_PreTokenizer.PreTokenize(preTokenizableChunk,
                            preTokenizedOutput.AsOutput());

                        using var tokenizerHandle = m_ListOfTokenPool.Get(out var tokenized);
                        m_Mapper.Tokenize(preTokenizedOutput, tokenized.AsOutput());

                        for (int k = 0; k < tokenized.Count; k++)
                        {
                            output.Add(tokenized[k].SetTypeId(typeId));
                        }
                    }
                }
            }

            void Truncate(
                IReadOnlyList<Token> pInputA,
                IReadOnlyList<Token> pInputB,
                bool pAddSpecialTokens,
                Output<IEnumerable<Token>> pOutputA,
                Output<IEnumerable<Token>> pOutputB)
            {
                var numAddedTokens = pAddSpecialTokens
                    ? m_PostProcessor.GetNumAddedTokens(pInputB is not null)
                    : 0;

                m_Truncator.Truncate(pInputA, pInputB, numAddedTokens, pOutputA, pOutputB);
            }
        }

        /// <inheritdoc cref="ITokenizer.Decode" />
        public string Decode(IReadOnlyList<int> input, bool skipSpecialTokens = false)
        {
            if (input is null)
                throw new ArgumentNullException(nameof(input));

            using var detokenizeOutputHandle = m_ListOfStringPool.Get(out var detokenized);

            for (var i = 0; i < input.Count; i++)
            {
                var id = input[i];
                if (m_AddedVocabulary.TryGetConfiguration(id, out var configuration))
                {
                    if(!configuration.Special || !skipSpecialTokens)
                        detokenized.Add(configuration.Value);
                }
                else
                {
                    var token = m_Mapper.IdToToken(id);
                    if(token is not null)
                        detokenized.Add(token);
                }
            }

            using var finalHandle = m_ListOfStringPool.Get(out var decoded);
            m_Decoder.Decode(detokenized, decoded.AsOutput());

            return string.Concat(decoded);
        }
    }
}
