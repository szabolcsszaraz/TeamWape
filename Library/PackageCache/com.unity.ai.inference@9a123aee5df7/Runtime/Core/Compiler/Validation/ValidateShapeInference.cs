using Unity.InferenceEngine.Compiler.Analyser;

namespace Unity.InferenceEngine.Compiler.Validation
{
    struct ValidateShapeInference : IValidationPass
    {
        public void Run(Model model)
        {
            MemoryFootprintAnalysis.FindLayersThatRequireStorage(model);
        }
    }
}
