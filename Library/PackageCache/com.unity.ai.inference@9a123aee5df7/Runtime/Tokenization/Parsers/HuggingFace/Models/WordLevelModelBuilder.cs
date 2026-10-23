using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Mappers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Models
{
    using static HuggingFaceHelper;

    [HfModel("WordLevel")]
    class WordLevelModelBuilder : IComponentBuilder<IMapper>
    {
        const string k_VocabKey = "vocab";
        const string k_UnkTokenKey = "unk_token";

        public IMapper Build(JToken parameters, HuggingFaceParser parser)
        {
            var vocabData = parameters.GetObject(k_VocabKey);
            var unkToken = parameters.GetString(k_UnkTokenKey);

            var vocab = BuildVocabulary(vocabData);
            return new WordLevelMapper(vocab, unkToken);
        }
    }
}
