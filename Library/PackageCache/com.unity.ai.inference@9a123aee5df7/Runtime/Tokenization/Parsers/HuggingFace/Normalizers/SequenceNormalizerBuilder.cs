using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Normalizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Normalizers
{
    [HfNormalizer("Sequence")]
    class SequenceNormalizerBuilder : IComponentBuilder<INormalizer>
    {
        const string k_NormalizersKey = "normalizers";

        public INormalizer Build(JToken parameters, HuggingFaceParser parser)
        {
            if (parameters.Type != JTokenType.Object)
                throw new("Expected object type");

            return Build((parameters as JObject)!, parser);
        }

        public INormalizer Build([NotNull] JObject parameters, HuggingFaceParser parser)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var normalizersArray = parameters.GetArray(k_NormalizersKey);

            var normalizers = new List<INormalizer>();

            foreach (var normalizerData in normalizersArray)
            {
                var normalizer = parser.BuildNormalizer(normalizerData);
                normalizers.Add(normalizer);
            }

            return new SequenceNormalizer(normalizers.ToArray());
        }
    }
}
