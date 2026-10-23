using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.PreTokenizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.PreTokenizers
{
    [HfPreTokenizer("Digits")]
    class DigitsPreTokenizerBuilder  : IComponentBuilder<IPreTokenizer>
    {
        const string k_IndividualDigitsKey = "individual_digits";

        public IPreTokenizer Build(JToken parameters, HuggingFaceParser parser)
        {
            var individualDigits = parameters.GetBooleanOptional(k_IndividualDigitsKey, false);
            return new DigitsPreTokenizer(individualDigits);
        }
    }
}
