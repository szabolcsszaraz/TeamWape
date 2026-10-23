using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.PreTokenizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.PreTokenizers
{
    [HfPreTokenizer("WhitespaceSplit")]
    class WhitespaceSplitPreTokenizerBuilder : IComponentBuilder<IPreTokenizer>
    {
        public IPreTokenizer Build(JToken parameters, HuggingFaceParser parser) =>
            new WhitespaceSplitPreTokenizer();
    }
}
