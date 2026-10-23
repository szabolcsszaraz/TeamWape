using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.PreTokenizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.PreTokenizers
{
    [HfPreTokenizer("BertPreTokenizer")]
    class BertPreTokenizerBuilder : IComponentBuilder<IPreTokenizer>
    {
        public IPreTokenizer Build(JToken _, HuggingFaceParser parser) => new BertPreTokenizer();
    }
}
