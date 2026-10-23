using System.Data;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Decoders;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Decoders
{
    [HfDecoder("Strip")]
    class StripDecoderBuilder : IComponentBuilder<IDecoder>
    {
        const string k_KeyContent = "content";
        const string k_KeyStart = "start";
        const string k_KeyStop = "stop";

        public IDecoder Build(JToken parameters, HuggingFaceParser parser)
        {
            var contentString = parameters.GetString(k_KeyContent);
            var start = parameters.GetInteger(k_KeyStart);
            var stop = parameters.GetInteger(k_KeyStop);

            if (contentString.Length > 1)
                throw new DataException($"{k_KeyContent} must be a single character");

            var content = contentString[0];

            return new StripDecoder(content, start, stop);
        }
    }
}
