using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Decoders;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Decoders
{
    [HfDecoder("ByteFallback")]
    class ByteFallBackDecoderBuilder : IComponentBuilder<IDecoder>
    {
        public IDecoder Build(JToken parameters, HuggingFaceParser parser) =>
            new ByteFallbackDecoder();
    }
}
