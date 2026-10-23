using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.PostProcessors;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.PostProcessors
{
    [HfPostProcessor("ByteLevel")]
    class ByteLevelPostProcessorBuilder : IComponentBuilder<IPostProcessor>
    {
        const string k_KeyTrimOffsets = "trim_offsets";

        public IPostProcessor Build(JToken parameters, HuggingFaceParser parser)
        {
            var trimOffsets = parameters.GetBooleanOptional(k_KeyTrimOffsets, true);
            return new ByteLevelPostProcessor(trimOffsets);
        }
    }
}
