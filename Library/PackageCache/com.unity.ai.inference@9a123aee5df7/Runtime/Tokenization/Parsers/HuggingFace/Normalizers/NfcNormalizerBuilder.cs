using System.Text;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Normalizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Normalizers
{
    [HfNormalizer("NFC")]
    class NfcNormalizerBuilder : IComponentBuilder<INormalizer>
    {
        public INormalizer Build(JToken parameters, HuggingFaceParser parser) =>
            new UnicodeNormalizer(NormalizationForm.FormC);
    }
}
