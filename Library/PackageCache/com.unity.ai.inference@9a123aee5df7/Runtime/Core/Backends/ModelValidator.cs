using System;
using Unity.InferenceEngine.Compiler.Passes;
using Unity.InferenceEngine.Compiler.Validation;

// ToArray(), ToDictionary()

namespace Unity.InferenceEngine
{
    static class ModelValidator
    {
        internal static Model ValidateModel(Model model)
        {
            var validationPasses = new IValidationPass[] {
                new ValidateBrokenLinks(),
                new ValidateUnconnectedLayers(),
                new ValidateUniqueOutputs() };

            foreach (var pass in validationPasses)
            {
                pass.Run(model);
            }

            return model;
        }
    }
}
