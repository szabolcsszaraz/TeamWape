using System.Text;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Normalizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Normalizers
{
    [HfNormalizer("NFKD")]
    class NfkdNormalizerBuilder : IComponentBuilder<INormalizer>
    {
        public INormalizer Build(JToken parameters, HuggingFaceParser parser) =>
            new UnicodeNormalizer(NormalizationForm.FormKD);
    }
}
