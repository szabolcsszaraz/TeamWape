using System;
using System.Collections.Generic;
using System.Linq;
using Unity.InferenceEngine.Graph;

namespace Unity.InferenceEngine.Compiler.Passes.Optimization
{
    /// <summary>
    /// Replaces an Einsum op with a MatMul op (plus transposes and reshapes) if the conditions are met.
    /// </summary>
    class EinsumToMatMulPass : GraphPass
    {
        const int k_IsOperandA = 1 << 0;
        const int k_IsOperandB = 1 << 1;
        const int k_IsOutput = 1 << 2;

        static void CalculatePermutations(TensorIndex originalDims, TensorIndex[] transformedDims,
            out int[] permutation, out int[] inversePermutation, out bool isPermuted)
        {
            isPermuted = false;
            permutation = new int[originalDims.rank];
            inversePermutation = new int[originalDims.rank];
            var index = 0;
            foreach (var dims in transformedDims)
            {
                for (var i = 0; i < dims.rank; i++)
                {
                    for (var j = 0; j < originalDims.rank; j++)
                    {
                        if (originalDims[j] == dims[i])
                        {
                            permutation[j] = index;
                            inversePermutation[index] = j;
                            if (permutation[j] != j)
                                isPermuted = true;
                            index++;
                        }
                    }
                }
            }
        }

        static bool InsertPermuteReshapeLayers(GraphModule gm, Node einsumNode, ref Node node, TensorIndex originalDims, TensorIndex[] transformedDims, DynamicTensorShape originalShape)
        {
            CalculatePermutations(originalDims, transformedDims, out _, out var inversePermutation, out var isPermuted);

            var shape = DynamicTensorShape.Ones(transformedDims.Length);
            var index = 0;
            for (var i = 0; i < transformedDims.Length; i++)
            {
                for (var j = 0; j < transformedDims[i].rank; j++)
                {
                    shape[i] *= originalShape[inversePermutation[index++]];
                }
            }

            var isReshaped = false;
            foreach (var t in transformedDims)
            {
                if (t.rank != 1)
                    isReshaped = true;
            }

            if (isReshaped && !shape.IsStatic())
                return false;

            if (isPermuted)
            {
                gm.graph.InsertingBefore(einsumNode);
                node = gm.graph.CallFunction("Transpose", new Argument[] { node, inversePermutation });
            }

            if (isReshaped)
            {
                var shapeConstant = new ConstantTensor(new TensorShape(shape.rank), shape.ToTensorShape().ToArray());
                var shapeNode = GraphPassUtil.AddConstant(gm, einsumNode, shapeConstant);
                node = gm.graph.CallFunction("Reshape", new Argument[] { node, shapeNode, false });
            }

            return true;
        }

        static bool InsertInversePermuteReshapeLayers(GraphModule gm, ref Node node, TensorIndex originalDims, TensorIndex[] transformedDims, DynamicTensorShape originalShape)
        {
            CalculatePermutations(originalDims, transformedDims, out var permutation, out var inversePermutation, out var isPermuted);

            var shape = DynamicTensorShape.DynamicOfRank(originalDims.rank);
            for (var i = 0; i < originalDims.rank; i++)
            {
                shape[i] = originalShape[permutation[i]];
            }

            var isReshaped = false;
            foreach (var t in transformedDims)
            {
                if (t.rank != 1)
                    isReshaped = true;
            }

            if (isReshaped && !shape.IsStatic())
                return false;

            if (isReshaped)
            {
                var shapeConstant = new ConstantTensor(new TensorShape(shape.rank), shape.ToTensorShape().ToArray());
                var shapeNode = GraphPassUtil.AddConstant(gm, node.next, shapeConstant);
                gm.graph.InsertingAfter(shapeNode);
                node = gm.graph.CallFunction("Reshape", new Argument[] { node, shapeNode, false });
            }

            if (isPermuted)
            {
                gm.graph.InsertingAfter(node);
                node = gm.graph.CallFunction("Transpose", new Argument[] { node, inversePermutation });
            }

            return true;
        }

        public override void Run(GraphModule gm)
        {
            var einsumNodes = gm.graph.FindNodes(Node.kOpCallFunction, "Einsum");

            foreach (var einsumNode in einsumNodes)
            {
                var operands = einsumNode.args[0].AsNodeArray;
                var numOperands = operands.Length;
                if (numOperands != 2)
                {
                    // only einsums with two operands can currently be reduced to matmul
                    continue;
                }

                var isInputShapes = true;
                var operandShapes = new DynamicTensorShape[2];
                for (var i = 0; i < numOperands && isInputShapes; i++)
                {
                    var shape = operands[i].partialTensor.shape;
                    if (shape.hasRank)
                        operandShapes[i] = shape;
                    else
                        isInputShapes = false;
                }

                if (!isInputShapes)
                {
                    // input shapes not found so can't parse equation
                    continue;
                }

                var equation = (string)einsumNode.args[1];
                var operandIndices = new TensorIndex[operands.Length];
                EinsumHelper.ParseEquationStringShape(equation, operandShapes, ref operandIndices, out var outputIndices, out var numIndices);
                var isUnsupportedConversion = false;

                var operandPositions = new List<int>[numOperands];

                for (var i = 0; i < numOperands; i++)
                {
                    operandPositions[i] = new List<int>();
                    for (var j = 0; j < numIndices; j++)
                    {
                        operandPositions[i].Add(-1);
                    }
                    for (var j = 0; j < operandIndices[i].rank; j++)
                    {
                        if (operandPositions[i][operandIndices[i][j]] >= 0)
                        {
                            // label repeats in operand, no optimization supported
                            isUnsupportedConversion = true;
                            break;
                        }

                        operandPositions[i][operandIndices[i][j]] = j;
                    }
                }

                var outputPositions = new List<int>();
                for (var j = 0; j < numIndices; j++)
                {
                    outputPositions.Add(-1);
                }
                for (var j = 0; j < outputIndices.rank; j++)
                {
                    outputPositions[outputIndices[j]] = j;
                }

                if (isUnsupportedConversion)
                    continue;

                var dimClassification = new int[numIndices];

                // classify einsum dimensions depending on type using flags
                for (var i = 0; i < numIndices; i++)
                {
                    dimClassification[i] += operandPositions[0][i] >= 0 ? k_IsOperandA : 0;
                    dimClassification[i] += operandPositions[1][i] >= 0 ? k_IsOperandB : 0;
                    dimClassification[i] += outputPositions[i] >= 0 ? k_IsOutput : 0;
                    if (dimClassification[i] == k_IsOperandA || dimClassification[i] == k_IsOperandB)
                    {
                        // label only appears in one operand, not a matmul
                        isUnsupportedConversion = true;
                        break;
                    }
                }

                if (isUnsupportedConversion)
                    continue;

                // categorize dims into sum, broadcast and operandOut
                var broadcastDims = Enumerable.Range(0, numIndices).Where(i => dimClassification[i] == k_IsOperandA + k_IsOperandB + k_IsOutput).ToList();
                var sumDims = Enumerable.Range(0, numIndices).Where(i => dimClassification[i] == k_IsOperandA + k_IsOperandB).ToList();
                var outputOperandDimsA = Enumerable.Range(0, numIndices).Where(i => dimClassification[i] == k_IsOperandA + k_IsOutput).ToList();
                var outputOperandDimsB = Enumerable.Range(0, numIndices).Where(i => dimClassification[i] == k_IsOperandB + k_IsOutput).ToList();

                // reorder dimensions within classifications to match given
                // inputs and outputs as closely as possible
                broadcastDims.Sort((a, b) => outputPositions[a].CompareTo(outputPositions[b]));
                sumDims.Sort((a, b) => operandPositions[0][a].CompareTo(operandPositions[0][b]));
                outputOperandDimsA.Sort((a, b) => outputPositions[a].CompareTo(outputPositions[b]));
                outputOperandDimsB.Sort((a, b) => outputPositions[a].CompareTo(outputPositions[b]));

                var broadcastDimsIndex = new TensorIndex(broadcastDims.ToArray());
                var sumDimsIndex = new TensorIndex(sumDims.ToArray());
                var outputOperandDimsAIndex = new TensorIndex(outputOperandDimsA.ToArray());
                var outputOperandDimsBIndex = new TensorIndex(outputOperandDimsB.ToArray());

                var transformedDims0 = broadcastDimsIndex.rank > 0 ? new[] { broadcastDimsIndex, outputOperandDimsAIndex, sumDimsIndex } : new[] { outputOperandDimsAIndex, sumDimsIndex };
                var transformedDims1 = broadcastDimsIndex.rank > 0 ? new[] { broadcastDimsIndex, sumDimsIndex, outputOperandDimsBIndex } : new[] { sumDimsIndex, outputOperandDimsBIndex };
                var transformedDimsOut = broadcastDimsIndex.rank > 0 ? new[] { broadcastDimsIndex, outputOperandDimsAIndex, outputOperandDimsBIndex } : new[] { outputOperandDimsAIndex, outputOperandDimsBIndex };

                // insert permute and reshape layers to take inputs to desired matmul inputs
                if (!InsertPermuteReshapeLayers(gm, einsumNode, ref operands[0], operandIndices[0], transformedDims0, operandShapes[0]))
                    break;
                if (!InsertPermuteReshapeLayers(gm, einsumNode, ref operands[1], operandIndices[1], transformedDims1, operandShapes[1]))
                    break;

                var einsumShape = einsumNode.partialTensor.shape;
                gm.graph.InsertingAfter(einsumNode);
                var outNode = gm.graph.CallFunction("MatMul", new Argument[] { operands[0], operands[1] });

                // insert reshape and permute layers to get desired output from matmul output
                if (!InsertInversePermuteReshapeLayers(gm, ref outNode, outputIndices, transformedDimsOut, einsumShape))
                    break;

                einsumNode.ReplaceAllUsesWith(outNode);
                gm.graph.EraseNode(einsumNode);
            }
        }
    }
}
