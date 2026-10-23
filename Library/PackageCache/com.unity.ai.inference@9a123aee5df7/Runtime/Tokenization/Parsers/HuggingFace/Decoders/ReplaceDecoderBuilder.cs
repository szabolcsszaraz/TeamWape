using System.Data;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Decoders;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Decoders
{
    [HfDecoder("Replace")]
    class ReplaceDecoderBuilder : IComponentBuilder<IDecoder>
    {
        const string k_KeyPattern = "pattern";
        const string k_KeyString = "String";
        const string k_KeyRegex = "Regex";
        const string k_KeyContent = "content";

        public IDecoder Build(JToken parameters, HuggingFaceParser parser)
        {
            var patternObject = parameters.GetObject(k_KeyPattern);

            var content = parameters.GetString(k_KeyContent);

            if (patternObject.TryGetValue(k_KeyString, out var patternString))
            {
                var pattern = patternString.Value<string>();
                return new ReplaceDecoder(pattern, content);
            }

            if (patternObject.TryGetValue(k_KeyRegex, out var patternRegex))
            {
                var pattern = patternRegex.Value<string>();
                var regex = new Regex(pattern);
                return new RegexReplaceDecoder(regex, content);
            }

            throw new DataException("Unsupported pattern type");
        }
    }
}
