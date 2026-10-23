using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Mappers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Models
{
    using static HuggingFaceHelper;

    [HfModel("BPE")]
    class BpeModelBuilder : IComponentBuilder<IMapper>
    {
        public IMapper Build(JToken parameters, HuggingFaceParser parser)
        {
            var byteFallback = parameters.GetBooleanOptional("byte_fallback", false);
            var continuingSubwordPrefix =
                parameters.GetStringOptional("continuing_subword_prefix", null);
            var dropOut = parameters.GetFloatOptional("dropout", 0);
            var endOfWordSuffix = parameters.GetStringOptional("end_of_word_suffix", null);
            var fuseUnk = parameters.GetBooleanOptional("fuse_unk", false);
            var ignoreMerges = parameters.GetBooleanOptional("ignore_merges", false);
            var unkTokenData = parameters.GetStringOptional("unk_token", null);

            var mergesData = parameters["merges"] as JArray;
            var vocabData = parameters["vocab"] as JObject;

            var vocab = BuildVocabulary(vocabData);
            var merges = BuildMerges(mergesData);

            if( unkTokenData is not null && !vocab.ContainsKey(unkTokenData))
                unkTokenData = null;

            var opt = new BpeMapperOptions
            {
                UnknownToken = unkTokenData,
                FuseUnknown = fuseUnk,
                ByteFallback = byteFallback,
                SubWordPrefix = continuingSubwordPrefix,
                WordSuffix = endOfWordSuffix,
                IgnoreMerges = ignoreMerges,
                DropOut = dropOut
            };

            return new BpeMapper(vocab, merges, opt);
        }
    }
}
