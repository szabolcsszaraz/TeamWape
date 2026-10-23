using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.PreTokenizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.PreTokenizers
{
    [HfPreTokenizer("ByteLevel")]
    class ByteLevelPreTokenizerBuilder : IComponentBuilder<IPreTokenizer>
    {
        static bool GetAddPrefixSpace(JToken data)
        {
            const bool k_Default = true;

            if (data == null)
                return k_Default;

            if (data.Type == JTokenType.Null)
                return k_Default;

            if (data.Type == JTokenType.Boolean)
                return data.Value<bool>();

            throw new($"Unknown add prefix space value: {data}");
        }

        static bool GetUseRegex(JToken data)
        {
            const bool k_Default = true;

            if (data == null)
                return k_Default;

            if (data.Type == JTokenType.Null)
                return k_Default;

            if (data.Type == JTokenType.Boolean)
                return data.Value<bool>();

            throw new($"Unknown use regex value: {data}");
        }

        public IPreTokenizer Build(JToken parameters, HuggingFaceParser parser)
        {
            var addPrefixSpace = GetAddPrefixSpace(parameters["add_prefix_space"]);
            var useRegex = GetUseRegex(parameters["use_regex"]);

            return new ByteLevelPreTokenizer(addPrefixSpace, useRegex);
        }
    }
}
