using System;
using Unity.InferenceEngine.Graph;
using UnityEngine;

namespace Unity.InferenceEngine.Compiler.Passes.Optimization
{
    /// <summary>
    /// Replaces op + relu with the fused activation op, if the op can be fused and the output isn't used elsewhere in the graph.
    /// </summary>
    class FuseActivationPass : GraphPass
    {
        public override void Run(GraphModule gm)
        {
            var reluNodes = gm.graph.FindNodes(Node.kOpCallFunction, "Relu");

            for (var i = reluNodes.Count - 1; i >= 0; i--)
            {
                var reluNode = reluNodes[i];
                var inputNode = (Node)reluNode.args[0];

                if (inputNode.op != Node.kOpCallFunction)
                    continue;

                if (inputNode.users.Count > 1)
                    continue;

                if (inputNode.target is "Conv" or "ConvTranspose" or "Dense" or "DenseBatched")
                {
                    inputNode.args[^1] = (int)Layers.FusableActivation.Relu;
                    reluNode.ReplaceAllUsesWith(inputNode);
                    gm.graph.EraseNode(reluNode);
                }
            }
        }
    }
}
