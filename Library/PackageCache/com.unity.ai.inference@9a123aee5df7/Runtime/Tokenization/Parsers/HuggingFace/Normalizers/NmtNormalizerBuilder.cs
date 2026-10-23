using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Normalizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Normalizers
{
    [HfNormalizer("Nmt")]
    class NmtNormalizerBuilder : IComponentBuilder<INormalizer>
    {
        public INormalizer Build(JToken parameters, HuggingFaceParser parser) =>
            new NmtNormalizer();
    }
}
