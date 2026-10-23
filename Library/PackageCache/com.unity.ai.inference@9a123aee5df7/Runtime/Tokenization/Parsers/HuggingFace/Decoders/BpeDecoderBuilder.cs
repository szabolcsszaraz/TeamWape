using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Decoders;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Decoders
{
    [HfDecoder("BPEDecoder")]
    class BpeDecoderBuilder : IComponentBuilder<IDecoder>
    {
        const string k_SuffixKey = "suffix";
        const string k_DefaultSuffix = "</w>";

        public IDecoder Build(JToken parameters, HuggingFaceParser parser)
        {
            var suffix = parameters.GetStringOptional(k_SuffixKey);
            if (string.IsNullOrEmpty(suffix))
                suffix = k_DefaultSuffix;

            return new BpeDecoder(suffix);
        }
    }
}
