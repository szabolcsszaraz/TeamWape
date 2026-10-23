namespace Unity.InferenceEngine.Compiler.Validation
{
    interface IValidationPass
    {
        void Run(Model model);
    }
}
