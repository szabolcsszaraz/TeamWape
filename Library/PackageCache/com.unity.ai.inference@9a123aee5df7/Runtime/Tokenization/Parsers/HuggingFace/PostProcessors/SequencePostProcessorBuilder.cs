using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.PostProcessors;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.PostProcessors
{
    [HfPostProcessor("Sequence")]
    class SequencePostProcessorBuilder : IComponentBuilder<IPostProcessor>
    {
        const string k_KeyProcessors = "processors";

        public IPostProcessor Build(JToken parameters, HuggingFaceParser parser)
        {
            var processorsArray = parameters.GetArray(k_KeyProcessors);

            var processors = new List<IPostProcessor>();
            foreach (var processorsData in processorsArray)
            {
                var processor = parser.BuildPostProcessor(processorsData);
                processors.Add(processor);
            }

            return new SequencePostProcessor(processors.ToArray());
        }
    }
}
