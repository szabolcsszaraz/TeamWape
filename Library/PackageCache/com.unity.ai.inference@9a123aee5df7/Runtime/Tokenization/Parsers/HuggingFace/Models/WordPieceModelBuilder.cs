using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Mappers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Models
{
    [HfModel("WordPiece")]
    class WordPieceModelBuilder : IComponentBuilder<IMapper>
    {
        const string k_DefaultUnknownToken = "[UNK]";
        const string k_DefaultCOntinuingSubwordPrefix = "##";
        const int k_DefaultMaxInputCharsPerWord = 100;

        public IMapper Build(JToken parameters, HuggingFaceParser parser)
        {
            var vocabJson = parameters["vocab"] as JObject;
            var vocab = HuggingFaceHelper.BuildVocabulary(vocabJson);

            var unknownToken = parameters["unk_token"]?.Value<string>() ?? k_DefaultUnknownToken;

            var continuingSubwordPrefix =
                parameters["continuing_subword_prefix"]?.Value<string>()
                ?? k_DefaultCOntinuingSubwordPrefix;
            var maxInputCharsPerWord = parameters["max_input_chars_per_word"]?.Value<int>()
                ?? k_DefaultMaxInputCharsPerWord;

            return new WordPieceMapper(vocab, unknownToken, continuingSubwordPrefix,
                maxInputCharsPerWord);
        }
    }
}
