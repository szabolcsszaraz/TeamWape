using System.Runtime.CompilerServices;
using Unity.InferenceEngine.Compiler.Passes;
using Unity.InferenceEngine.Compiler.Passes.Cleanup;
using Unity.InferenceEngine.Compiler.Passes.Optimization;
using Unity.InferenceEngine.Graph;

[assembly: InternalsVisibleTo("Unity.InferenceEngine.ONNX.Editor")]
[assembly: InternalsVisibleTo("Unity.InferenceEngine.RuntimeTests")]
[assembly: InternalsVisibleTo("Unity.InferenceEngine.EditorTests")]

namespace Unity.InferenceEngine
{
    static class ModelOptimizer
    {
        static void RunPasses(GraphModule gm, GraphPass[] passes)
        {
            foreach (var pass in passes)
            {
                pass.Run(gm);
            }
        }

        internal static void OptimizeGraph(GraphModule gm)
        {
            var optimizationPasses = new GraphPass[]
            {
                new ContractSubExpressionPass(),
                new EinsumToMatMulPass(),
                new FuseConstantsPass(),
                new PreComputeWindowedDFTMatrixPass(), // this needs to run after the FuseConstantPass
                new RemoveNoOpsPass(),
                new RemoveUnusedPass(),
                new ConcatenateTransposesPass(),
                new ContractToSimplerLayerPass(),
                new RemoveNoOpsPass(),
                new SimplifyReshapeInputPass(),
                new FuseDensePass(),
                new FuseLinearLayersPass(),
                new FuseActivationPass(),
                new RemoveDuplicatesPass(),
                new RemoveNoOpsPass(),
                // Good to do those passes at the very end
                new RemoveUnusedPass(),
                new RoundDenormalWeightsPass(),
            };

            RunPasses(gm, optimizationPasses);
        }
    }
}
