using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Decoders;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Decoders
{
    [HfDecoder("WordPiece")]
    class WordPieceDecoderBuilder : IComponentBuilder<IDecoder>
    {
        public IDecoder Build(JToken parameters, HuggingFaceParser parser)
        {
            var prefix = parameters.GetStringOptional("prefix", "##");
            var cleanUp = parameters.GetBooleanOptional("cleanUp", true);
            return new WordPieceDecoder(prefix, cleanUp);
        }
    }
}
