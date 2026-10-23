using System.Data;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Normalizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Normalizers
{
    [HfNormalizer("Replace")]
    class ReplaceNormalizerBuilder : IComponentBuilder<INormalizer>
    {
        const string k_KeyPattern = "pattern";
        const string k_KeyString = "String";
        const string k_KeyRegex = "Regex";
        const string k_KeyContent = "content";

        public INormalizer Build(JToken parameters, HuggingFaceParser parser)
        {
            var patternObject = parameters.GetObject(k_KeyPattern);

            var content = parameters.GetString(k_KeyContent);

            if (patternObject.TryGetValue(k_KeyString, out var patternString))
            {
                var pattern = patternString.Value<string>();
                return new ReplaceNormalizer(pattern, content);
            }

            if (patternObject.TryGetValue(k_KeyRegex, out var patternRegex))
            {
                var pattern = patternRegex.Value<string>();
                var regex = new Regex(pattern);
                return new RegexReplaceNormalizer(regex, content);
            }

            throw new DataException("Unsupported pattern type");
        }
    }
}
