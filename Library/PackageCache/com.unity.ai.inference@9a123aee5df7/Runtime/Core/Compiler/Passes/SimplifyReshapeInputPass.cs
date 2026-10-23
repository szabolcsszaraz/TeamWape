using System;
using Unity.InferenceEngine.Graph;
using UnityEngine;

namespace Unity.InferenceEngine.Compiler.Passes.Optimization
{
    /// <summary>
    /// Replaces shape tensors for Reshape nodes with constant tensors where possible by using 0 and -1 values.
    /// </summary>
    class SimplifyReshapeInputPass : GraphPass
    {
        public override void Run(GraphModule gm)
        {
            var reshapeNodes = gm.graph.FindNodes(Node.kOpCallFunction, "Reshape");

            foreach (var reshapeNode in reshapeNodes)
            {
                var inputNode = (Node)reshapeNode.args[0];
                var shapeNode = (Node)reshapeNode.args[1];
                var allowZero = (bool)reshapeNode.args[2];
                var shapePartialTensor = shapeNode.partialTensor;
                if (!shapePartialTensor.isPartiallyKnown)
                    continue;
                var newShape = new PartialTensor<int>(shapePartialTensor.shape);
                for (var i = 0; i < shapePartialTensor.length; i++)
                    newShape[i] = shapePartialTensor.Get<int>(i);

                var input = inputNode.partialTensor;
                var output = reshapeNode.partialTensor;

                // try and replace params and unknowns with values
                for (var i = 0; i < output.shape.rank; i++)
                {
                    if (!output.shape[i].isValue)
                        continue;
                    newShape[i] = (PartialTensorElement<int>)output.shape[i];
                }

                // try and replace params with 0
                if (input.shape.hasRank && !allowZero)
                {
                    for (var i = 0; i < Mathf.Min(input.shape.rank, shapePartialTensor.length); i++)
                    {
                        if (input.shape[i].EqualsParam(output.shape[i]))
                            newShape[i] = PartialTensorElement<int>.Zero;
                    }
                }

                // try and replace single param or unknown with -1
                var numZero = 0;
                var numMinusOne = 0;
                var numUnknown = 0;
                var unknownIndex = 0;
                for (var i = 0; i < newShape.length; i++)
                {
                    if (!newShape[i].isValue)
                    {
                        numUnknown++;
                        unknownIndex = i;
                        continue;
                    }

                    if (newShape[i].value == 0)
                        numZero++;
                    else if (newShape[i].value == -1)
                        numMinusOne++;
                }

                if (numMinusOne == 0 && numUnknown == 1 && (!allowZero || numZero == 0))
                    newShape[unknownIndex] = PartialTensorElement<int>.Value(-1);

                if (!newShape.IsStatic())
                    continue;

                var newShapeNode = GraphPassUtil.AddConstant(gm, reshapeNode, newShape.shape.ToTensorShape(), newShape.ToArray());
                reshapeNode.UpdateArgs(new Argument[] { inputNode, newShapeNode, allowZero });
            }
        }
    }
}
