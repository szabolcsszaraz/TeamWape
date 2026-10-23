using System.Data;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.PreTokenizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.PreTokenizers
{
    [HfPreTokenizer("Metaspace")]
    class MetaspacePreTokenizerBuilder : IComponentBuilder<IPreTokenizer>
    {
        const string k_KeyReplacement = "replacement";
        const string k_KeyPrependScheme = "prepend_scheme";
        const string k_KeySplit = "split";

        public IPreTokenizer Build(JToken parameters, HuggingFaceParser parser)
        {
            var replacementString = parameters.GetStringOptional(k_KeyReplacement, "\u2581");
            if (replacementString.Length is > 1 or 0)
                throw new DataException(
                    $"{k_KeyReplacement}: should be exactly one character long");

            var replacement = replacementString[0];

            PrependScheme prependScheme;
            {
                var prependSchemeString =
                    parameters.GetStringOptional(k_KeyPrependScheme, "always");
                prependScheme = prependSchemeString switch
                {
                    "always" => PrependScheme.Always,
                    "never" => PrependScheme.Never,
                    "first" => PrependScheme.First,
                    _ => throw new DataException(
                        $"{k_KeyPrependScheme}: unsupported value '{prependSchemeString}'.")
                };
            }

            if (prependScheme == PrependScheme.First)
                throw new DataException(
                    $"{nameof(PrependScheme)} {PrependScheme.First} not yet supported");

            var split = parameters.GetBooleanOptional(k_KeySplit, true);

            return new MetaspacePreTokenizer(replacement, prependScheme, split);
        }
    }
}
