using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Normalizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Normalizers
{
    [HfNormalizer("Prepend")]
    class PrependNormalizerBuilder : IComponentBuilder<INormalizer>
    {
        const string k_KeyPrepend = "prepend";

        public INormalizer Build(JToken parameters, HuggingFaceParser parser)
        {
            var content = parameters.GetString(k_KeyPrepend);
            return new PrependNormalizer(content);
        }
    }
}
