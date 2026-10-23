using System;
using System.Collections.Generic;
using Unity.InferenceEngine.Graph;
using UnityEngine;

namespace Unity.InferenceEngine.Compiler.Passes.Optimization
{
    /// <summary>
    /// Replaces multiple chained transposes with a single op.
    /// </summary>
    class ConcatenateTransposesPass : GraphPass
    {
        public override void Run(GraphModule gm)
        {
            var transposeNodes = gm.graph.FindNodes(Node.kOpCallFunction, "Transpose");

            for (var i = transposeNodes.Count - 1; i >= 0; i--)
            {
                var node = transposeNodes[i];
                if (node.erased)
                    continue;

                // Accumulate the chain of transpose nodes backwards
                var chainNodes = new List<Node> { node };
                var currentInputNode = (Node)node.args[0];

                while (currentInputNode.op == Node.kOpCallFunction && currentInputNode.target == "Transpose")
                {
                    chainNodes.Add(currentInputNode);
                    currentInputNode = (Node)currentInputNode.args[0];
                }

                // If chain length is 1, nothing to merge, continue
                if (chainNodes.Count == 1)
                    continue;

                // Compose all permutations starting from the deepest node input upwards
                var composedPerm = chainNodes[^1].args[1].AsIntArray;
                for (var j = chainNodes.Count - 2; j >= 0; j--)
                {
                    var perm = chainNodes[j].args[1].AsIntArray;
                    composedPerm = MergeTranspose(perm, composedPerm);
                }

                // Insert new combined transpose before the first node in the chain (which is the last node in the chainNodes list)
                var baseInput = (Node)chainNodes[^1].args[0];

                GraphPassUtil.ReplaceNode(node, "Transpose", new Argument[] { baseInput, composedPerm });

                foreach (var chainNode in chainNodes)
                {
                    if (chainNode.users.Count > 0)
                        break;
                    gm.graph.EraseNode(chainNode);
                }
            }
        }

        static int[] MergeTranspose(int[] permA, int[] permB)
        {
            var n = permA.Length;
            var result = new int[n];
            for (var i = 0; i < n; i++)
            {
                result[i] = permB[permA[i]];
            }
            return result;
        }
    }
}
