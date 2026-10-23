using System;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.PreTokenizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.PreTokenizers
{
    [HfPreTokenizer("Punctuation")]
    class PunctuationPreTokenizerBuilder : IComponentBuilder<IPreTokenizer>
    {
        const string k_BehaviorKey = "behavior";

        public IPreTokenizer Build(JToken parameters, HuggingFaceParser parser)
        {
            var behaviorString = parameters.GetStringOptional(k_BehaviorKey, "Isolated");

            var behavior = behaviorString switch
            {
                "Removed" => SplitDelimiterBehavior.Removed,
                "Isolated" => SplitDelimiterBehavior.Isolated,
                "MergedWithPrevious" => SplitDelimiterBehavior.MergedWithPrevious,
                "MergedWithNext" => SplitDelimiterBehavior.MergedWithNext,
                "Contiguous" => SplitDelimiterBehavior.Contiguous,
                _ => throw new ArgumentOutOfRangeException(
                    $"{k_BehaviorKey}: Unsupported value: {behaviorString}")
            };

            return new PunctuationPreTokenizer(behavior);
        }
    }
}
