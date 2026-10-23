using Unity.InferenceEngine.Graph;

namespace Unity.InferenceEngine.Compiler.Passes
{
    abstract class GraphPass
    {
        public virtual void Run(GraphModule graphModule) { }
    }
}
