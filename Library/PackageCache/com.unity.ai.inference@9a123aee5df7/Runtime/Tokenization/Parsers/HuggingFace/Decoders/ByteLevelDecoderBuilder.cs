using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Decoders;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Decoders
{
    [HfDecoder("ByteLevel")]
    class ByteLevelDecoderBuilder : IComponentBuilder<IDecoder>
    {
        public IDecoder Build(JToken parameters, HuggingFaceParser parser) =>
            new ByteLevelDecoder();
    }
}
