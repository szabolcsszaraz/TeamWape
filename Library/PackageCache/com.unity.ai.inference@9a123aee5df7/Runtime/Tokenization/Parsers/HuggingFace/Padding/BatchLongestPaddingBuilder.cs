using System;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Padding;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Padding
{
    [HfPadding("BatchLongest")]
    class BatchLongestPaddingBuilder : IComponentBuilder<IPadding>
    {
        public IPadding Build(JToken parameters, HuggingFaceParser parser)
        {
            var direction = parameters["direction"].Value<string>();
            var padToMultipleOf = parameters.GetIntegerOptional("pad_to_multiple_of", 1);

            var padId = parameters["pad_id"].Value<int>();
            var padValue = parameters["pad_token"].Value<string>();
            var padTypeId = parameters["pad_type_id"].Value<int>();

            var padToken = new Token(padId, padValue, typeId: padTypeId);

            return direction switch
            {
                "Right" => new RightPadding(new BatchLongestSizeProvider(), padToken, padToMultipleOf),
                "Left" => new LeftPadding(new BatchLongestSizeProvider(), padToken, padToMultipleOf),
                _ => throw new ArgumentOutOfRangeException(direction)
            };
        }
    }
}
