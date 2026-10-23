using System.Data;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Decoders;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Decoders
{
    [HfDecoder("Metaspace")]
    class MetaspaceDecoderBuilder : IComponentBuilder<IDecoder>
    {
        const string k_KeyReplacement = "replacement";
        const string k_KeyPrependScheme = "prepend_scheme";

        public IDecoder Build(JToken parameters, HuggingFaceParser parser)
        {
            var replacementString = parameters.GetStringOptional(k_KeyReplacement, "\u2581");
            if (replacementString.Length is > 1 or 0)
                throw new DataException($"{k_KeyReplacement}: should be exactly one character long");

            var replacement = replacementString[0];

            PrependScheme prependScheme;
            {
                var prependSchemeString = parameters.GetStringOptional(k_KeyPrependScheme, "always");
                prependScheme = prependSchemeString switch
                {
                    "always" => PrependScheme.Always,
                    "never" => PrependScheme.Never,
                    "first" => PrependScheme.First,
                    _ => throw new DataException(
                        $"{k_KeyPrependScheme}: unsupported value '{prependSchemeString}'.")
                };
            }

            return new MetaspaceDecoder(replacement, prependScheme);
        }
    }
}
