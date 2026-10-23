using System;
using System.Collections.Generic;
using System.Data;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Decoders;
using Unity.InferenceEngine.Tokenization.Mappers;
using Unity.InferenceEngine.Tokenization.Normalizers;
using Unity.InferenceEngine.Tokenization.Padding;
using Unity.InferenceEngine.Tokenization.PostProcessors;
using Unity.InferenceEngine.Tokenization.PreTokenizers;
using Unity.InferenceEngine.Tokenization.Truncators;
using Unity.InferenceEngine.Tokenization.Truncators.Strategies;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace
{
    /// <summary>
    /// Parses a Hugging Face JSON configuration and builds a <see cref="Tokenizer"/>.
    /// </summary>
    public class HuggingFaceParser
    {
        static readonly string[] k_LegacyModelTypePriority = new [] {"BPE", "WordPiece", "WordLevel", "Unigram"};

        /// <summary>
        /// Gets a parser configured with all the built-in components builders, with the additional
        /// builders decorated with <see cref="HfAttribute"/> (with priority to the built-ins.
        /// </summary>
        /// <returns>
        /// A pre-configured parser.
        /// </returns>
        public static HuggingFaceParser GetDefault()
        {
            var parser = new HuggingFaceParser();

            foreach (var (type, decoder) in HuggingFaceHelper.Decoders)
                parser.SetBuilder(type, decoder);

            foreach (var (type, mapper) in HuggingFaceHelper.Mappers)
                parser.SetBuilder(type, mapper);

            foreach (var (type, normalizer) in HuggingFaceHelper.Normalizers)
                parser.SetBuilder(type, normalizer);

            foreach (var (type, paddingProcessor) in HuggingFaceHelper.PaddingProcessor)
                parser.SetBuilder(type, paddingProcessor);

            foreach (var (type, postProcessor) in HuggingFaceHelper.PostProcessors)
                parser.SetBuilder(type, postProcessor);

            foreach (var (type, preTokenizer) in HuggingFaceHelper.PreTokenizers)
                parser.SetBuilder(type, preTokenizer);

            foreach (var (type, truncator) in HuggingFaceHelper.Truncators)
                parser.SetBuilder(type, truncator);

            return parser;
        }

        static IEnumerable<TokenConfiguration> GetTokenConfigs(JObject config)
        {
            var unique = new HashSet<int>();

            var addedTokens = config["added_tokens"] as JArray;
            foreach (var addedToken in addedTokens)
            {
                var id = addedToken["id"].Value<int>();

                // found some duplicated configurations.
                if (!unique.Add(id))
                    continue;

                var value = addedToken["content"].Value<string>();
                var wholeWord = addedToken["single_word"].Value<bool>();
                var strip = (addedToken["lstrip"].Value<bool>() ? Direction.Left : Direction.None) |
                    (addedToken["rstrip"].Value<bool>() ? Direction.Right : Direction.None);
                var normalized = addedToken["normalized"].Value<bool>();
                var special = addedToken["special"].Value<bool>();

                yield return new(id, value, wholeWord, strip, normalized, special);
            }
        }

        readonly Dictionary<string, Lazy<IComponentBuilder<INormalizer>>> m_Normalizers = new();
        readonly Dictionary<string, Lazy<IComponentBuilder<IPreTokenizer>>> m_PreTokenizers = new();
        readonly Dictionary<string, Lazy<IComponentBuilder<IMapper>>> m_Mappers = new();

        readonly Dictionary<string, Lazy<IComponentBuilder<IPostProcessor>>> m_PostProcessors =
            new();

        readonly Dictionary<string, Lazy<IComponentBuilder<ITruncator>>> m_Truncators = new();
        readonly Dictionary<string, Lazy<IComponentBuilder<IPadding>>> m_PaddingProcessor = new();
        readonly Dictionary<string, Lazy<IComponentBuilder<IDecoder>>> m_Decoders = new();

        /// <summary>
        /// Defines a builder for the type of normalizer.
        /// </summary>
        /// <param name="type">Type of the normalizer.</param>
        /// <param name="builder">
        /// A lazy reference of a builder for the component.
        /// </param>
        public void SetBuilder([NotNull] string type,
            [NotNull] Lazy<IComponentBuilder<INormalizer>> builder)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            m_Normalizers[type] = builder ?? throw new ArgumentNullException(nameof(builder));
        }

        /// <summary>
        /// Defines a builder for the type of pre-tokenizer.
        /// </summary>
        /// <param name="type">
        /// Type of the pre-tokenizer.
        /// </param>
        /// <param name="builder">
        /// A lazy reference of a builder for the component.
        /// </param>
        public void SetBuilder([NotNull] string type,
            [NotNull] Lazy<IComponentBuilder<IPreTokenizer>> builder)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            m_PreTokenizers[type] = builder ?? throw new ArgumentNullException(nameof(builder));
        }

        /// <summary>
        /// Defines a builder for the type of tokenizer model (<see cref="IMapper"/>).
        /// </summary>
        /// <param name="type">
        /// Type of the tokenizer model (<see cref="IMapper"/>).
        /// </param>
        /// <param name="builder">
        /// A lazy reference of a builder for the component.
        /// </param>
        public void SetBuilder([NotNull] string type,
            [NotNull] Lazy<IComponentBuilder<IMapper>> builder)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            m_Mappers[type] = builder ?? throw new ArgumentNullException(nameof(builder));
        }

        /// <summary>
        /// Defines a builder for the type of post processor.
        /// </summary>
        /// <param name="type">
        /// Type of the post processor.
        /// </param>
        /// <param name="builder">
        /// A lazy reference of a builder for the component.
        /// </param>
        public void SetBuilder([NotNull] string type,
            [NotNull] Lazy<IComponentBuilder<IPostProcessor>> builder)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            m_PostProcessors[type] = builder ?? throw new ArgumentNullException(nameof(builder));
        }

        /// <summary>
        /// Defines a builder for the type of truncator.
        /// </summary>
        /// <param name="strategy">
        /// Strategy of the truncator.
        /// </param>
        /// <param name="builder">
        /// A lazy reference of a builder for the component.
        /// </param>
        public void SetBuilder([NotNull] string strategy,
            [NotNull] Lazy<IComponentBuilder<ITruncator>> builder)
        {
            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));
            m_Truncators[strategy] = builder ?? throw new ArgumentNullException(nameof(builder));
        }

        /// <summary>
        /// Defines a builder for the type of padding.
        /// </summary>
        /// <param name="strategy">
        /// Strategy of the padding.
        /// </param>
        /// <param name="builder">
        /// A lazy reference of a builder for the component.
        /// </param>
        public void SetBuilder([NotNull] string strategy,
            [NotNull] Lazy<IComponentBuilder<IPadding>> builder)
        {
            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));
            m_PaddingProcessor[strategy] =
                builder ?? throw new ArgumentNullException(nameof(builder));
        }

        /// <summary>
        /// Defines a builder for the type of decoder.
        /// </summary>
        /// <param name="type">
        /// Type of the decoder.
        /// </param>
        /// <param name="builder">
        /// A lazy reference of a builder for the component.
        /// </param>
        public void SetBuilder([NotNull] string type,
            [NotNull] Lazy<IComponentBuilder<IDecoder>> builder)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            m_Decoders[type] = builder ?? throw new ArgumentNullException(nameof(builder));
        }

        /// <summary>
        /// Defines a builder for the type of normalizer.
        /// </summary>
        /// <param name="type">
        /// Type of the normalizer.
        /// </param>
        /// <param name="builder">
        /// A reference of a builder for the component.
        /// </param>
        public void SetBuilder([NotNull] string type,
            [NotNull] IComponentBuilder<INormalizer> builder)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            SetBuilder(type, new Lazy<IComponentBuilder<INormalizer>>(() => builder));
        }

        /// <summary>
        /// Defines a builder for the type of pre tokenizer.
        /// </summary>
        /// <param name="type">
        /// Type of the pre tokenizer.
        /// </param>
        /// <param name="builder">
        /// A reference of a builder for the component.
        /// </param>
        public void SetBuilder([NotNull] string type,
            [NotNull] IComponentBuilder<IPreTokenizer> builder)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            SetBuilder(type, new Lazy<IComponentBuilder<IPreTokenizer>>(() => builder));
        }

        /// <summary>
        /// Defines a builder for the type of tokenizer model (<see cref="IMapper"/>).
        /// </summary>
        /// <param name="type">Type of the tokenizer model (<see cref="IMapper"/>).</param>
        /// <param name="builder">
        /// A reference of a builder for the component.
        /// </param>
        public void SetBuilder([NotNull] string type, [NotNull] IComponentBuilder<IMapper> builder)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            SetBuilder(type, new Lazy<IComponentBuilder<IMapper>>(() => builder));
        }

        /// <summary>
        /// Defines a builder for the type of post processor.
        /// </summary>
        /// <param name="type">
        /// Type of the post processor.
        /// </param>
        /// <param name="builder">
        /// A reference of a builder for the component.
        /// </param>
        public void SetBuilder([NotNull] string type,
            [NotNull] IComponentBuilder<IPostProcessor> builder)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            SetBuilder(type, new Lazy<IComponentBuilder<IPostProcessor>>(() => builder));
        }

        /// <summary>
        /// Defines a builder for the type of truncator.
        /// </summary>
        /// <param name="strategy">
        /// Strategy of the truncator.
        /// </param>
        /// <param name="builder">
        /// A reference of a builder for the component.
        /// </param>
        public void SetBuilder([NotNull] string strategy,
            [NotNull] IComponentBuilder<ITruncator> builder)
        {
            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            SetBuilder(strategy, new Lazy<IComponentBuilder<ITruncator>>(() => builder));
        }

        /// <summary>
        /// Defines a builder for the type of padding.
        /// </summary>
        /// <param name="strategy">
        /// Strategy of the padding.
        /// </param>
        /// <param name="builder">
        /// A reference of a builder for the component.
        /// </param>
        public void SetBuilder([NotNull] string strategy,
            [NotNull] IComponentBuilder<IPadding> builder)
        {
            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            SetBuilder(strategy, new Lazy<IComponentBuilder<IPadding>>(() => builder));
        }

        /// <summary>
        /// Defines a builder for the type of decoder.
        /// </summary>
        /// <param name="type">
        /// Type of the decoder.
        /// </param>
        /// <param name="builder">
        /// A reference of a builder for the component.
        /// </param>
        public void SetBuilder([NotNull] string type, [NotNull] IComponentBuilder<IDecoder> builder)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            SetBuilder(type, new Lazy<IComponentBuilder<IDecoder>>(() => builder));
        }

        TComponent Get<TComponent>(JToken parameters, string componentType,
            Dictionary<string, Lazy<IComponentBuilder<TComponent>>> builders,
            string defaultType = null)
        {
            if (parameters is not {Type: JTokenType.Object})
                return default;

            var type = parameters.GetStringOptional("type", defaultType);

            if (type is null)
                throw new DataException("Type not found");

            return !builders.TryGetValue(type, out var builder)
                ? throw new DataException($"{componentType} {type} not found")
                : builder.Value.Build(parameters, this);
        }

        bool TryGet<TComponent>(JToken parameters, string componentType,
            Dictionary<string, Lazy<IComponentBuilder<TComponent>>> builders,
            out TComponent result,
            string defaultType = null)
        {
            try
            {
                result = Get(parameters, componentType, builders, defaultType);
                return true;
            }
            catch (Exception )
            {
                result = default;
                return false;
            }
        }

        /// <summary>
        /// Builds a normalizer from its JSON serialized form.
        /// </summary>
        /// <param name="parameters">
        /// The JSON serialized form of the normalizer.
        /// </param>
        /// <returns>
        /// The <see cref="INormalizer"/> instance configured with the
        /// <paramref name="parameters"/>.
        /// </returns>
        internal INormalizer BuildNormalizer(JToken parameters) =>
            Get(parameters, "Normalizer", m_Normalizers);

        /// <summary>
        /// Builds a pre tokenizer from its JSON serialized form.
        /// </summary>
        /// <param name="parameters">
        /// The JSON serialized form of the pre tokenizer.
        /// </param>
        /// <returns>
        /// The <see cref="IPreTokenizer"/> instance configured with the
        /// <paramref name="parameters"/>.
        /// </returns>
        internal IPreTokenizer BuildPreTokenizer(JToken parameters) =>
            Get(parameters, "PreTokenizer", m_PreTokenizers);

        /// <summary>
        /// Builds a mapper from its JSON serialized form.
        /// </summary>
        /// <param name="parameters">
        /// The JSON serialized form of the tokenizer model.
        /// </param>
        /// <param name="decoder">
        /// Decoder configuration. Sometimes helps determining the type of the mapper.
        /// </param>
        /// <returns>
        /// The <see cref="IMapper"/> instance configured with the <paramref name="parameters"/>.
        /// </returns>
        IMapper BuildMapper(JToken parameters, JToken decoder)
        {
            var defaultType = parameters.GetStringOptional("type", string.Empty);
            if (!string.IsNullOrEmpty(defaultType))
                return Get(parameters, "Mapper", m_Mappers, defaultType);

            foreach (var modelType in k_LegacyModelTypePriority)
            {
                if(TryGet(parameters, "Mapper", m_Mappers, out var mapper, modelType))
                    return mapper;
            }

            throw new DataException($"Cannot determine the type of the model");
        }

        /// <summary>
        /// Builds a post processor from its JSON serialized form.
        /// </summary>
        /// <param name="parameters">
        /// The JSON serialized form of the post processor.
        /// </param>
        /// <returns>
        /// The <see cref="IPostProcessor"/> instance configured with the
        /// <paramref name="parameters"/>.
        /// </returns>
        internal IPostProcessor BuildPostProcessor(JToken parameters) =>
            Get(parameters, "PostProcessor", m_PostProcessors);

        /// <summary>
        /// Builds a decoder from its JSON serialized form.
        /// </summary>
        /// <param name="parameters">
        /// The JSON serialized form of the decoder.
        /// </param>
        /// <returns>
        /// The <see cref="IDecoder"/> instance configured with the <paramref name="parameters"/>.
        /// </returns>
        internal IDecoder BuildDecoder(JToken parameters) =>
            Get(parameters, "Decoder", m_Decoders);

        /// <summary>
        /// Builds a padding processor from its JSON serialized form.
        /// </summary>
        /// <param name="parameters">
        /// The JSON serialized form of the padding processor.
        /// </param>
        /// <returns>
        /// The <see cref="IPadding"/> instance configured with the <paramref name="parameters"/>.
        /// </returns>
        IPadding BuildPadding(JToken parameters)
        {
            if (parameters is {Type: JTokenType.Object})
            {
                var paddingStrategy = GetStrategy(parameters);
                return !m_PaddingProcessor.TryGetValue(paddingStrategy, out var builder)
                    ? throw new DataException($"Unsupported Padding strategy {paddingStrategy}")
                    : builder.Value.Build(parameters, this);
            }
            return null;

            string GetStrategy(JToken pParameters)
            {
                if (!((JObject) parameters).TryGetValue("strategy", out var strategyData))
                    throw new DataException($"Unsupported strategy {pParameters}.");

                if (strategyData.Type == JTokenType.Object
                    && (strategyData as JObject)!.TryGetValue("Fixed", out _))
                {
                    return "Fixed";
                }

                if (strategyData.Type == JTokenType.String
                    && strategyData.Value<string>() == "BatchLongest")
                    return "BatchLongest";

                throw new DataException($"Unknown strategy");
            }
        }

        /// <summary>
        /// Builds a normalizer from its JSON serialized form.
        /// </summary>
        /// <param name="parameters">
        /// The JSON serialized form of the normalizer.
        /// </param>
        /// <returns>
        /// The <see cref="INormalizer"/> instance configured with the <paramref name="parameters"/>.
        /// </returns>
        ITruncator BuildTruncator(JToken parameters)
        {
            const string k_StrategyKey = "strategy";
            const string k_MaxLengthKey = "max_length";
            const string k_StrideKey = "stride";
            const string k_DirectionKey = "direction";

            const string k_DefaultDirection = "Right";
            const string k_DefaultStrategy = "LongestFirst";
            const int k_DefaultMaxLength = 512;
            const int k_DefaultStride = 0;

            if (parameters is {Type: JTokenType.Object})
            {
                var direction = parameters.GetStringOptional(k_DirectionKey, k_DefaultDirection);
                var rangeGenerator = direction switch
                {
                    "Right" => RightDirectionRangeGenerator.Instance,
                    "Left" => LeftDirectionRangeGenerator.Instance,
                    _ => throw new($"Unknown direction: {direction}")
                };

                var strategyString = parameters.GetStringOptional(k_StrategyKey, k_DefaultStrategy);
                var strategy = strategyString switch
                {
                    "LongestFirst" => LongestFirstStrategy.Instance,
                    "OnlySecond" => OnlySecondStrategy.Instance,
                    "OnlyFirst" => OnlyFirstStrategy.Instance,
                    _ => throw new($"Unknown strategy: {strategyString}")
                };

                var maxLength = parameters.GetIntegerOptional(k_MaxLengthKey, k_DefaultMaxLength);
                var stride = parameters.GetIntegerOptional(k_StrideKey, k_DefaultStride);

                return new GenericTruncator(strategy, rangeGenerator, maxLength, stride);
            }
            return DefaultTruncator.Instance;
        }

        /// <summary>
        /// Parses a Hugging Face JSON configuration and builds a <see cref="Tokenizer"/>.
        /// </summary>
        /// <param name="content">
        /// Contains the JSON
        /// </param>
        /// <returns>
        /// The corresponding tokenizer.
        /// </returns>
        public ITokenizer Parse(string content)
        {
            var json = JObject.Parse(content);

            var tokenConfigs = GetTokenConfigs(json);

            var mapper = BuildMapper(json["model"], json["decoder"]);
            var normalizer = BuildNormalizer(json["normalizer"]);
            var preTokenizer = BuildPreTokenizer(json["pre_tokenizer"]);
            var postProcessor = BuildPostProcessor(json["post_processor"]);
            var truncator = BuildTruncator(json["truncation"]);
            var padding = BuildPadding(json["padding"]);
            var decoder = BuildDecoder(json["decoder"]);

            return new Tokenizer(
                mapper,
                normalizer: normalizer,
                preTokenizer: preTokenizer,
                postProcessor: postProcessor,
                truncator: truncator,
                paddingProcessor: padding,
                addedVocabulary: tokenConfigs,
                decoder: decoder);
        }
    }
}
