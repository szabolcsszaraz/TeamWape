using System.Data;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.PreTokenizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.PreTokenizers
{
    [HfPreTokenizer("CharDelimiterSplit")]
    class CharDelimiterSplitPreTokenizerBuilder : IComponentBuilder<IPreTokenizer>
    {
        const string k_DelimiterKey = "delimiter";

        public IPreTokenizer Build(JToken parameters, HuggingFaceParser parser)
        {
            var delimiterString = parameters.GetString(k_DelimiterKey);
            if (delimiterString.Length != 1)
                throw new DataException($"{k_DelimiterKey} must be a single char");

            var delimiter = delimiterString[0];
            return new CharSplitPreTokenizer(delimiter);
        }
    }
}
