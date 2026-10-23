using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Normalizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Normalizers
{
    [HfNormalizer("Lowercase")]
    class LowerCaseNormalizerBuilder : IComponentBuilder<INormalizer>
    {
        public INormalizer Build(JToken parameters, HuggingFaceParser parser) =>
            new LowercaseNormalizer();
    }
}
