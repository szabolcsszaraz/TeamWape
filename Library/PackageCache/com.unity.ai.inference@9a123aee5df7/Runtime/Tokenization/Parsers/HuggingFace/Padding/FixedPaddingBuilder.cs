using System;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Padding;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Padding
{
    [HfPadding("Fixed")]
    class FixedPaddingBuilder : IComponentBuilder<IPadding>
    {
        public IPadding Build(JToken parameters, HuggingFaceParser parser)
        {
            var size = parameters["strategy"]["Fixed"].Value<int>();

            var direction = parameters["direction"].Value<string>();
            var padToMultipleOf = parameters.GetIntegerOptional("pad_to_multiple_of", 1);

            var padId = parameters["pad_id"].Value<int>();
            var padValue = parameters["pad_token"].Value<string>();
            var padTypeId = parameters["pad_type_id"].Value<int>();

            var padToken = new Token(padId, padValue, typeId: padTypeId);

            return direction switch
            {
                "Right" => new RightPadding(new FixedPaddingSizeProvider(size), padToken, padToMultipleOf),
                "Left" => new LeftPadding(new FixedPaddingSizeProvider(size), padToken, padToMultipleOf),
                _ => throw new ArgumentOutOfRangeException(direction)
            };
        }
    }
}
