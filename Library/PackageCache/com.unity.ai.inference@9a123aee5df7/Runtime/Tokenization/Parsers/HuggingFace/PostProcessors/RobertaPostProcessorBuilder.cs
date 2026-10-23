using System;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.PostProcessors;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.PostProcessors
{
    using static HuggingFaceHelper;

    [HfPostProcessor("RobertaProcessing")]
    class RobertaPostProcessorBuilder : IComponentBuilder<IPostProcessor>
    {
        static Token GetToken(JToken src, string field)
        {
            if(src == null)
                throw new ArgumentNullException(nameof(src));

            var data = src[field];

            if (data == null)
                throw new("Token data cannot be null");

            if (data is not JArray a || a.Count != 2)
                throw new("Invalid token data");

            var valueData = a[0];
            if(valueData.Type != JTokenType.String)
                throw new($"Invalid token value: {valueData.Type}");
            var value = valueData.Value<string>();

            var idData = a[1];
            if(idData.Type != JTokenType.Integer)
                throw new($"Invalid token id: {idData.Type}");
            var id = idData.Value<int>();

            return new (id, value);
        }

        public IPostProcessor Build(JToken parameters, HuggingFaceParser parser)
        {
            var sepToken = GetToken(parameters, "sep");
            var clsToken = GetToken(parameters, "cls");
            var trimOffsets = parameters.GetBooleanOptional("trim_offsets", true);
            var addPrefixSpace = parameters.GetBooleanOptional("add_prefix_space", true);

            return new RobertaPostProcessor(sepToken, clsToken, addPrefixSpace, trimOffsets);
        }
    }
}
