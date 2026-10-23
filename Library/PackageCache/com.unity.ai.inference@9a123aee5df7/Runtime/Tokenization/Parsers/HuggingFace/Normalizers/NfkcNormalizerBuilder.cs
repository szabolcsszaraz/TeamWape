using System.Text;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Normalizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Normalizers
{
    [HfNormalizer("NFKC")]
    class NfkcNormalizerBuilder : IComponentBuilder<INormalizer>
    {
        public INormalizer Build(JToken parameters, HuggingFaceParser parser) =>
            new UnicodeNormalizer(NormalizationForm.FormKC);
    }
}
