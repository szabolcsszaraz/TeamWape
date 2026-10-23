using System;
using Unity.InferenceEngine.Graph;
using UnityEngine;

namespace Unity.InferenceEngine.Compiler.Passes.Optimization
{
    /// <summary>
    /// Fuses MatMul + Add into single Dense or DenseBatched node.
    /// </summary>
    class FuseDensePass : GraphPass
    {
        public override void Run(GraphModule gm)
        {
            using var ops = new CPUOps();

            foreach (var node in gm.graph.Nodes())
            {
                if (node.op != Node.kOpCallFunction || node.target is not ("MatMul" or "MatMul2D"))
                    continue;
                if (node.target == "MatMul2D" && (bool)node.args[2])
                    continue;
                if (node.users.Count != 1)
                    continue;
                Node outNode = null;
                foreach (var n in node.users)
                {
                    outNode = n;
                    break;
                }
                if (outNode.op != Node.kOpCallFunction || outNode.target is not ("Add" or "ScalarMad"))
                    continue;

                // const weights of rank 2
                var weightsNode = (Node)node.args[1];
                if (weightsNode.op != Node.kOpGetAttr)
                    continue;

                var inputNode = (Node)node.args[0];

                var shapeX = inputNode.partialTensor.shape;
                var shapeW = weightsNode.partialTensor.shape;

                if (shapeW.rank > 2 && node.target == "MatMul" && outNode.target == "Add")
                {
                    var biasNode = outNode.args[0].AsNode == node ? (Node)outNode.args[1] : (Node)outNode.args[0];
                    var shapeB = biasNode.partialTensor.shape;
                    if (shapeX.rank != shapeW.rank || shapeX.rank != shapeB.rank)
                        continue;

                    var allEqual = true;
                    for (var i = 0; i < shapeX.rank - 2; i++)
                        allEqual &= shapeX[i] == shapeW[i] && shapeX[i] == shapeB[i];

                    if (allEqual)
                        GraphPassUtil.ReplaceNodes(node, outNode, "DenseBatched", new[] { node.args[0], node.args[1], biasNode, (int)Layers.FusableActivation.None });
                }
                else
                {
                    // const bias of rank 1

                    var shouldTransposeWeights = node.target == "MatMul2D" && (bool)node.args[3];

                    Node biasNode;
                    if (outNode.target == "ScalarMad")
                    {
                        if ((DataType)outNode.args[1].AsInt == DataType.Int || (float)outNode.args[2] != 1f)
                            continue;
                        var biasValue = (float)outNode.args[3];
                        // Skip fusion if bias is zero. This is just MatMul, not Dense
                        if (biasValue == 0f)
                            continue;
                        using var biasTensor = ops.ConstantOfShape(new TensorShape(weightsNode.partialTensor.shape.ToTensorShape()[shouldTransposeWeights ? -2 : -1]), biasValue);
                        biasNode = GraphPassUtil.AddConstant(gm, node, biasTensor);
                    }
                    else
                    {
                        biasNode = outNode.args[0].AsNode == node ? (Node)outNode.args[1] : (Node)outNode.args[0];
                        var inputShape = inputNode.partialTensor.shape;
                        var biasShape = biasNode.partialTensor.shape;
                        if (!biasShape.IsStatic())
                            continue;
                        var biasStaticShape = biasShape.ToTensorShape();
                        if (biasStaticShape.length != biasStaticShape[-1])
                            continue;
                        if (biasStaticShape.rank > (inputShape.hasRank ? inputShape.rank : 2))
                            continue;
                    }

                    if (shouldTransposeWeights)
                    {
                        using var transposedWeightsTensor = ops.Transpose(gm.attributes[weightsNode.target].ToTensor() as Tensor<float>);
                        weightsNode = GraphPassUtil.AddConstant(gm, node, transposedWeightsTensor);
                    }

                    GraphPassUtil.ReplaceNodes(node, outNode, "Dense", new[] { node.args[0], weightsNode, biasNode, (int)Layers.FusableActivation.None });
                }
            }
        }
    }
}
