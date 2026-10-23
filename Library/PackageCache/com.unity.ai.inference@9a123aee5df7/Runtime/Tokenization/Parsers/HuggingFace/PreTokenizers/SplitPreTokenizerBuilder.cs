using System;
using System.Data;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.PreTokenizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.PreTokenizers
{
    [HfPreTokenizer("Split")]
    class SplitPreTokenizerBuilder : IComponentBuilder<IPreTokenizer>
    {
        const string k_PatternKey = "pattern";
        const string k_RegexKey = "Regex";
        const string k_StringKey = "String";
        const string k_BehaviorKey = "behavior";
        const string k_InvertKey = "invert";

        const bool k_DefaultInvert = false;

        public IPreTokenizer Build(JObject parameters)
        {
            SplitDelimiterBehavior behavior;
            {
                if (!parameters.TryGetValue(k_BehaviorKey, out var behaviorData))
                    throw new ArgumentException(
                        $"{nameof(parameters)} must contain {k_BehaviorKey}");

                if (behaviorData.Type != JTokenType.String)
                    throw new DataException($"{k_BehaviorKey} must be a string");

                var behaviourString = behaviorData.Value<string>();

                behavior = behaviourString switch
                {
                    "Removed" => SplitDelimiterBehavior.Removed,
                    "Isolated" => SplitDelimiterBehavior.Isolated,
                    "MergedWithPrevious" => SplitDelimiterBehavior.MergedWithPrevious,
                    "MergedWithNext" => SplitDelimiterBehavior.MergedWithNext,
                    "Contiguous" => SplitDelimiterBehavior.Contiguous,
                    _ => throw new ArgumentOutOfRangeException(
                        $"{k_BehaviorKey}: Unsupported value: {behaviourString}")
                };
            }

            bool invert;
            {
                if (!parameters.TryGetValue(k_InvertKey, out var invertData))
                    invert = k_DefaultInvert;
                else if (invertData.Type != JTokenType.Boolean)
                    throw new ArgumentException($"{nameof(parameters)} must contain {k_InvertKey}");
                else
                    invert = invertData.Value<bool>();
            }

            if (!parameters.TryGetValue(k_PatternKey, out var patternData))
                throw new ArgumentException($"{nameof(parameters)} must contain {k_PatternKey}");

            if (patternData.Type != JTokenType.Object)
                throw new DataException($"{k_PatternKey} field must be a {typeof(JObject)}");

            var patternObject = patternData as JObject;

            if (patternObject!.TryGetValue(k_RegexKey, out var patternRegexData))
            {
                if (patternRegexData.Type != JTokenType.String)
                    throw new DataException($"Field {k_RegexKey} must be a string");

                var pattern = patternRegexData.Value<string>();
                if (pattern == "")
                    return new RuneSplitPreTokenizer(behavior, invert);
                return new RegexSplitPreTokenizer(pattern, behavior, invert);
            }

            if (patternObject!.TryGetValue(k_StringKey, out var patternStringData))
            {
                if (patternStringData.Type != JTokenType.String)
                    throw new DataException($"Field {k_StringKey} must be a string");

                var pattern = patternStringData.Value<string>();
                if (pattern == "")
                    return new RuneSplitPreTokenizer(behavior, invert);

                // Optimization when it is only one char
                if (pattern.Length == 1)
                    return new CharSplitPreTokenizer(pattern[0], behavior, invert);

                pattern = Regex.Escape(pattern);
                return new RegexSplitPreTokenizer(pattern, behavior, invert);
            }

            throw new NotSupportedException("Unsupported pattern");
        }

        public IPreTokenizer Build(JToken parameters, HuggingFaceParser parser) =>
            parameters is {Type: JTokenType.Object}
                ? Build(parameters as JObject)
                : throw new DataException($"{nameof(parameters)} must be a {nameof(JObject)}");
    }

}
