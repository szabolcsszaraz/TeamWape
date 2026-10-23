using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Normalizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Normalizers
{
    [HfNormalizer("Strip")]
    class StripNormalizerBuilder : IComponentBuilder<INormalizer>
    {
        const string k_LeftKey = "left";
        const string k_RightKey = "right";

        public INormalizer Build(JToken parameters, HuggingFaceParser parser)
        {
            var stripLeft = parameters.GetBooleanOptional(k_LeftKey, true);
            var stripRight = parameters.GetBooleanOptional(k_RightKey, true);

            return new StripNormalizer(stripLeft, stripRight);
        }
    }
}
