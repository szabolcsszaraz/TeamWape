using System;
using Unity.InferenceEngine.Graph;
using UnityEngine;

namespace Unity.InferenceEngine.Compiler.Passes.Cleanup
{
    /// <summary>
    /// Removes unnecessary identity nodes from the graph.
    /// </summary>
    class RemoveNoOpsPass : GraphPass
    {
        public override void Run(GraphModule gm)
        {
            var outputNode = gm.graph.OutputNode();

            var identityNodes = gm.graph.FindNodes(Node.kOpCallFunction, "Identity");

            for (var i = identityNodes.Count - 1; i >= 0; i--)
            {
                var node = identityNodes[i];
                var inputNode = (Node)node.args[0];

                // if the op is a single operator that maps an input to the output we need to keep it as a copy op
                if (node.users.Contains(outputNode) && inputNode.op is Node.kOpPlaceholder)
                    continue;

                node.ReplaceAllUsesWith(inputNode);
                gm.graph.EraseNode(node);
            }

            // add copy ops to repeated outputs
            var outputs = outputNode.args[0].AsNodeArray;
            for (var i = 0; i < outputs.Length; i++)
            {
                var firstIndex = Array.IndexOf(outputs, outputs[i]);
                if (firstIndex < i)
                {
                    gm.graph.InsertingBefore(outputNode);
                    outputs[i] = gm.graph.CallFunction("Identity", new Argument[] { outputs[i] });
                    outputs[i].partialTensor = outputs[i].partialTensor;
                }
            }
            outputNode.UpdateArgs(new[] { new Argument(outputs) });
        }
    }
}
