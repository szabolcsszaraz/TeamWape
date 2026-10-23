using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Normalizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Normalizers
{
    [HfNormalizer("BertNormalizer")]
    class BertNormalizerBuilder : IComponentBuilder<INormalizer>
    {
        public INormalizer Build(JToken parameters, HuggingFaceParser parser)
        {
            var cleanText = parameters.GetBooleanOptional("clean_text", true);
            var handleCjk = parameters.GetBooleanOptional("handle_chinese_chars", true);
            var lowercase = parameters.GetBooleanOptional("lowercase", true);

            var stripAccentsToken = parameters["strip_accents"];

            var stripAccents =
                stripAccentsToken is null || stripAccentsToken.Type == JTokenType.Null
                    ? lowercase
                    : stripAccentsToken.Value<bool>();

            return new BertNormalizer(cleanText, handleCjk, stripAccents, lowercase);
        }
    }
}
