using System.Collections.Generic;
using System.Data;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Mappers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Models
{
    [HfModel("Unigram")]
    class UnigramModelBuilder : IComponentBuilder<IMapper>
    {
        const string k_UnkIdField = "unk_id";
        const string k_ByteFallbackField = "byte_fallback";
        const string k_VocabField = "vocab";

        public IMapper Build(JToken parameters, HuggingFaceParser parser)
        {
            var unkId = parameters.GetIntegerOptional(k_UnkIdField, -1);
            var byteFallback = parameters.GetBooleanOptional(k_ByteFallbackField, false);

            var vocab = parameters.GetArray(k_VocabField);

            var entries = new List<UnigramVocabEntry>();

            for(var i = 0; i < vocab.Count; i++)
            {
                var entryToken = vocab[i];
                if (entryToken.Type != JTokenType.Array)
                    throw new DataException($"Invalid vocab entry #{i}: not a JSON array");

                var entryArray = entryToken as JArray;
                if (entryArray!.Count != 2)
                    throw new DataException($"Invalid vocab entry #{i}: expected size 2");

                string value;
                {
                    var valueToken = entryArray[0];
                    if(valueToken.Type != JTokenType.String)
                        throw new DataException($"Invalid vocab entry #{i}: expected string value");
                    value = valueToken.Value<string>();
                }

                double score;
                {
                    var scoreToken = entryArray[1];
                    if(scoreToken.Type != JTokenType.Float)
                        throw new DataException($"Invalid vocab entry #{i}: expected float score");
                    score = scoreToken.Value<double>();
                }

                var entry = new UnigramVocabEntry(value, score);
                entries.Add(entry);
            }

            return new UnigramMapper(entries, unkId, byteFallback);
        }
    }
}
