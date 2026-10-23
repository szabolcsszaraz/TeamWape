using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.PreTokenizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.PreTokenizers
{
    [HfPreTokenizer("Sequence")]
    class SequencePreTokenizerBuilder : IComponentBuilder<IPreTokenizer>
    {
        const string k_PretokenizersKey = "pretokenizers";

        public IPreTokenizer Build(JToken parameters, HuggingFaceParser parser)
        {
            if (parameters.Type != JTokenType.Object)
                throw new("Expected object type");

            return Build((parameters as JObject)!, parser);
        }

        public IPreTokenizer Build([NotNull] JObject parameters, HuggingFaceParser parser)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var preTokenizersArray = parameters.GetArray(k_PretokenizersKey);

            var preTokenizers = new List<IPreTokenizer>();

            foreach (var preTokenizerData in preTokenizersArray)
            {
                var preTokenizer = parser.BuildPreTokenizer(preTokenizerData);
                preTokenizers.Add(preTokenizer);
            }

            return new SequencePreTokenizer(preTokenizers.ToArray());
        }
    }
}
