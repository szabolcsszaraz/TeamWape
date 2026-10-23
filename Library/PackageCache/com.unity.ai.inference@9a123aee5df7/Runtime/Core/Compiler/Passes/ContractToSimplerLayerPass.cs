using System.Collections.Generic;
using System;
using Unity.InferenceEngine.Graph;
using UnityEngine;

namespace Unity.InferenceEngine.Compiler.Passes.Optimization
{
    /// <summary>
    /// Replaces a single op with another simpler op if certain conditions are met.
    /// </summary>
    class ContractToSimplerLayerPass : GraphPass
    {
        // All the reduction ops, by name.
        static readonly string[] s_ReductionTargets = { "ReduceMax", "ReduceMin", "ReduceL1", "ReduceL2", "ReduceLogSum", "ReduceLogSumExp", "ReduceMean", "ReduceProd", "ReduceSum", "ReduceSumSquare", "ReduceVariance" };

        public override void Run(GraphModule gm)
        {
            var concatNodes = gm.graph.FindNodes(Node.kOpCallFunction, "Concat");
            foreach (var concatNode in concatNodes)
            {
                var inputNodes = concatNode.args[0].AsNodeArray;

                // replace concat with one input with identity
                if (inputNodes.Length == 1)
                {
                    GraphPassUtil.ReplaceNode(concatNode, "Identity", new Argument[] { inputNodes[0] });
                    continue;
                }

                // replace Concat layer which concatenates the only same tensor multiple times with Tile layer
                var isConcatTile = true;
                for (var i = 0; i < inputNodes.Length && isConcatTile; i++)
                    if (inputNodes[i] != inputNodes[0])
                        isConcatTile = false;

                if (!isConcatTile)
                    continue;

                var shape = concatNode.partialTensor.shape;
                if (!shape.hasRank)
                    continue;

                var repeatsTensorShape = TensorShape.Ones(shape.rank);
                var axis = (int)concatNode.args[1];
                repeatsTensorShape[axis] = inputNodes.Length;
                var repeatsNode = GraphPassUtil.AddConstant(gm, concatNode, new TensorShape(repeatsTensorShape.rank), repeatsTensorShape.ToArray());
                GraphPassUtil.ReplaceNode(concatNode, "Tile", new Argument[] { inputNodes[0], repeatsNode });
            }

            var transposeNodes = gm.graph.FindNodes(Node.kOpCallFunction, "Transpose");
            foreach (var transposeNode in transposeNodes)
            {
                // replace Transpose layer which does not actually transpose the dimensions with the identity
                // TODO: can add cases such as tensor.shape = (1, 1, 4) permutation = [1, 0, 2]
                if (transposeNode.args[1] == null)
                    continue;
                var permutations = transposeNode.args[1].AsIntArray;

                var nopTranspose = true;
                for (var i = 0; i < permutations.Length && nopTranspose; ++i)
                {
                    if (permutations[i] != i && permutations[i] + permutations.Length != i)
                        nopTranspose = false;
                }

                if (!nopTranspose)
                    continue;

                GraphPassUtil.ReplaceNode(transposeNode, "Identity", new[] { transposeNode.args[0] });
            }

            var reshapeNodes = gm.graph.FindNodes(Node.kOpCallFunction, "Reshape");
            foreach (var reshapeNode in reshapeNodes)
            {
                // replace Reshape layer which does not reshape with identity layer
                var outputShape = reshapeNode.partialTensor.shape;
                var inputNode = (Node)reshapeNode.args[0];
                var inputShape = inputNode.partialTensor.shape;
                if (!outputShape.hasRank || !inputShape.hasRank || outputShape.rank != inputShape.rank)
                    continue;

                var shapeTensor = ((Node)reshapeNode.args[1]).partialTensor;
                var allowZero = (bool)reshapeNode.args[2];

                var nonMatches = 0;
                for (var i = 0; i < outputShape.rank && nonMatches < 2; i++)
                {
                    if (outputShape[i] == inputShape[i] || (!allowZero && shapeTensor.Get<int>(i).Equals(0)))
                        continue;
                    nonMatches++;
                }

                if (nonMatches > 1)
                    continue;

                GraphPassUtil.ReplaceNode(reshapeNode, "Identity", new[] { reshapeNode.args[0] });
            }

            var expandNodes = gm.graph.FindNodes(Node.kOpCallFunction, "Expand");
            foreach (var expandNode in expandNodes)
            {
                // replace Expand layer which does not change the number of elements with a reshape or identity layer
                // TODO: add support for cases such as tensor.shape = (A), expandShape = [1, A] which is a Reshape
                var outputShape = expandNode.partialTensor.shape;
                var inputNode = (Node)expandNode.args[0];
                var inputShape = inputNode.partialTensor.shape;
                if (!outputShape.hasRank || !inputShape.hasRank)
                    continue;

                var shape = ((Node)expandNode.args[1]).partialTensor;

                if (outputShape.rank == inputShape.rank)
                {
                    var isIdentity = true;
                    for (var i = 0; i < outputShape.rank && isIdentity; i++)
                    {
                        if (outputShape[i] == inputShape[i] || (i < shape.length && shape.Get<int>(i).Equals(1)))
                            continue;
                        isIdentity = false;
                    }

                    if (isIdentity)
                        GraphPassUtil.ReplaceNode(expandNode, "Identity", new Argument[] { inputNode });

                    continue;
                }

                if (outputShape.IsStatic() && inputShape.IsStatic() && outputShape.Length() == inputShape.Length())
                {
                    var shapeNode = GraphPassUtil.AddConstant(gm, expandNode, new TensorShape(outputShape.rank), outputShape.ToTensorShape().ToArray());
                    GraphPassUtil.ReplaceNode(expandNode, "Reshape", new Argument[] { inputNode, shapeNode, false });
                }
            }

            var scalarMadNodes = gm.graph.FindNodes(Node.kOpCallFunction, "ScalarMad");
            foreach (var scalarMadNode in scalarMadNodes)
            {
                var dataType = (DataType)scalarMadNode.args[1].AsInt;

                // replace ScalarMad layer with scale 1 and bias 0 with identity
                if (dataType == DataType.Float && ((float)scalarMadNode.args[2] != 1f || (float)scalarMadNode.args[3] != 0f))
                    continue;

                if (dataType == DataType.Int && ((int)scalarMadNode.args[4] != 1 || (int)scalarMadNode.args[5] != 0))
                    continue;

                GraphPassUtil.ReplaceNode(scalarMadNode, "Identity", new[] { scalarMadNode.args[0] });
            }

            var tileNodes = gm.graph.FindNodes(Node.kOpCallFunction, "Tile");
            foreach (var tileNode in tileNodes)
            {
                // replace Tile layer where the tile values are all 1 with Identity
                var repeatsNode = (Node)tileNode.args[1];
                var repeats = repeatsNode.partialTensor;

                if (!repeats.IsStatic())
                    continue;
                var allOnes = true;
                for (var i = 0; i < repeats.length; i++)
                {
                    allOnes &= repeats.Get<int>(i).value == 1;
                }
                if (!allOnes)
                    continue;

                GraphPassUtil.ReplaceNode(tileNode, "Identity", new[] { tileNode.args[0] });
            }

            var reduceNodes = new List<Node>();
            foreach (var reductionTarget in s_ReductionTargets)
                reduceNodes.AddRange(gm.graph.FindNodes(Node.kOpCallFunction, reductionTarget));
            foreach (var reduceNode in reduceNodes)
            {
                // replace Reduce layer which does not perform any reduction with Identity
                var axesNode = (Node)reduceNode.args[1];
                var axes = axesNode?.partialTensor;
                var keepdims = (bool)reduceNode.args[2];
                var noopWithEmptyAxes = (bool)reduceNode.args[3];
                var isEmptyAxes = (axes == null || axes.shape.Length() == 0);
                if (noopWithEmptyAxes && isEmptyAxes)
                {
                    switch (reduceNode.target)
                    {
                        case "ReduceL1":
                        case "ReduceL2":
                            GraphPassUtil.ReplaceNode(reduceNode, "Abs", new[] { reduceNode.args[0] });
                            continue;
                        case "ReduceSumSquare":
                            GraphPassUtil.ReplaceNode(reduceNode, "Square", new[] { reduceNode.args[0] });
                            continue;
                        case "ReduceLogSum":
                            GraphPassUtil.ReplaceNode(reduceNode, "Log", new[] { reduceNode.args[0] });
                            continue;
                        default:
                            GraphPassUtil.ReplaceNode(reduceNode, "Identity", new[] { reduceNode.args[0] });
                            continue;
                    }
                }

                if (isEmptyAxes || !axes.IsStatic())
                    continue;
                // these reductions would simplify to another value, not identity
                if (reduceNode.target is "ReduceL1" or "ReduceL2" or "ReduceSumSquare" or "ReduceLogSum" or "ReduceVariance")
                    continue;

                var inputNode = (Node)reduceNode.args[0];
                var input = inputNode.partialTensor;
                if (!input.shape.hasRank)
                    continue;

                var isTrivialReduction = true;
                for (var i = 0; i < axes.length && isTrivialReduction; i++)
                {
                    if (input.shape[axes.Get<int>(i).value] == 1)
                        continue;
                    isTrivialReduction = false;
                }

                if (!isTrivialReduction)
                    continue;

                if (keepdims)
                {
                    GraphPassUtil.ReplaceNode(reduceNode, "Identity", new Argument[] { inputNode });
                }
                else
                {
                    GraphPassUtil.ReplaceNode(reduceNode, "Squeeze", new Argument[] { inputNode, axesNode });
                }
            }

            var castNodes = gm.graph.FindNodes(Node.kOpCallFunction, "Cast");
            foreach (var castNode in castNodes)
            {
                // replace Tile layer where the tile values are all 1 with Identity
                var inputNode = (Node)castNode.args[0];
                var dataType = (DataType)castNode.args[1].AsInt;
                if (inputNode.partialTensor.dataType != dataType)
                    continue;

                GraphPassUtil.ReplaceNode(castNode, "Identity", new Argument[] { inputNode });
            }

            var castLikeNodes = gm.graph.FindNodes(Node.kOpCallFunction, "CastLike");
            foreach (var castLikeNode in castLikeNodes)
            {
                // replace CastLike with Identity or Cast
                var inputNode = (Node)castLikeNode.args[0];
                if (inputNode.partialTensor.dataType == castLikeNode.partialTensor.dataType)
                {
                    GraphPassUtil.ReplaceNode(castLikeNode, "Identity", new Argument[] { inputNode });
                }
                else
                {
                    GraphPassUtil.ReplaceNode(castLikeNode, "Cast", new Argument[] { inputNode, (int)castLikeNode.partialTensor.dataType });
                }
            }

            var batchNormalizationNodes = gm.graph.FindNodes(Node.kOpCallFunction, "BatchNormalization");
            foreach (var batchNormalizationNode in batchNormalizationNodes)
            {
                var allConstInputs = true;
                for (var i = 1; i < 5; i++)
                {
                    allConstInputs &= ((Node)batchNormalizationNode.args[i]).op == Node.kOpGetAttr;
                }
                if (!allConstInputs)
                    continue;

                var gamma = GraphPassUtil.GetConstantInput(gm, batchNormalizationNode, 1) as Tensor<float>;
                var beta = GraphPassUtil.GetConstantInput(gm, batchNormalizationNode, 2) as Tensor<float>;
                var mean = GraphPassUtil.GetConstantInput(gm, batchNormalizationNode, 3) as Tensor<float>;
                var variance = GraphPassUtil.GetConstantInput(gm, batchNormalizationNode, 4) as Tensor<float>;
                var epsilon = (float)batchNormalizationNode.args[5];

                var op = new CPUOps();
                using var epsilonTensor = op.ConstantOfShape(new TensorShape(1), epsilon);
                using var a0 = op.Add(variance, epsilonTensor);
                using var sqrtVar = op.Sqrt(a0);
                using var scale = op.Div(gamma, sqrtVar);
                using var m0 = op.Mul(scale, mean);
                using var bias = op.Sub(beta, m0);

                var scaleNode = GraphPassUtil.AddConstant(gm, batchNormalizationNode, scale);
                var biasNode = GraphPassUtil.AddConstant(gm, batchNormalizationNode, bias);

                GraphPassUtil.ReplaceNode(batchNormalizationNode, "ScaleBias", new[] { batchNormalizationNode.args[0], scaleNode, biasNode });
            }

            var gatherNodes = gm.graph.FindNodes(Node.kOpCallFunction, "Gather");
            foreach (var gatherNode in gatherNodes)
            {
                // replace Gather with single index value with Split or Narrow
                var indicesNode = (Node)gatherNode.args[1];
                if (!indicesNode.partialTensor.IsStatic())
                    continue;
                var indicesShape = indicesNode.partialTensor.shape;
                if (!indicesShape.hasRank || indicesShape.rank > 1 || !(indicesShape.Length() == 1))
                    continue;

                var axis = (int)gatherNode.args[2];
                var dimNode = GraphPassUtil.AddConstant(gm, gatherNode, new TensorShape(), new[] { axis });

                if (indicesShape.rank == 0)
                {
                    GraphPassUtil.ReplaceNode(gatherNode, "Select", new[] { gatherNode.args[0], dimNode, gatherNode.args[1] });
                }
                else if (indicesShape.rank == 1)
                {
                    var lengthNode = GraphPassUtil.AddConstant(gm, gatherNode, new TensorShape(), new[] { 1 });
                    GraphPassUtil.ReplaceNode(gatherNode, "Narrow", new[] { gatherNode.args[0], dimNode, gatherNode.args[1], lengthNode });
                }
            }

            var powNodes = gm.graph.FindNodes(Node.kOpCallFunction, "Pow");
            foreach (var powNode in powNodes)
            {
                var baseNode = (Node)powNode.args[0];
                var exponentNode = (Node)powNode.args[1];
                if (!exponentNode.partialTensor.IsStatic())
                    continue;
                var exponentShape = exponentNode.partialTensor.shape.ToTensorShape();
                if (exponentShape.length > 1 || !powNode.partialTensor.shape.hasRank || exponentShape.rank > powNode.partialTensor.shape.rank)
                    continue;
                if (exponentNode.partialTensor.dataType == DataType.Float && baseNode.partialTensor.dataType == DataType.Float)
                {
                    var exponent = exponentNode.partialTensor.Get<float>().value;
                    switch (exponent)
                    {
                        case -1f:
                            GraphPassUtil.ReplaceNode(powNode, "Reciprocal", new[] { powNode.args[0] });
                            break;
                        case -0.5f:
                            GraphPassUtil.ReplaceNode(powNode, "Rsqrt", new[] { powNode.args[0] });
                            break;
                        case 0.5f:
                            GraphPassUtil.ReplaceNode(powNode, "Sqrt", new[] { powNode.args[0] });
                            break;
                        case 1f:
                            GraphPassUtil.ReplaceNode(powNode, "Identity", new[] { powNode.args[0] });
                            break;
                        case 2f:
                            GraphPassUtil.ReplaceNode(powNode, "Square", new[] { powNode.args[0] });
                            break;
                    }
                }
                if (exponentNode.partialTensor.dataType == DataType.Int)
                {
                    var exponent = exponentNode.partialTensor.Get<int>().value;
                    switch (exponent)
                    {
                        case -1:
                            GraphPassUtil.ReplaceNode(powNode, "Reciprocal", new[] { powNode.args[0] });
                            break;
                        case 1:
                            GraphPassUtil.ReplaceNode(powNode, "Identity", new[] { powNode.args[0] });
                            break;
                        case 2:
                            GraphPassUtil.ReplaceNode(powNode, "Square", new[] { powNode.args[0] });
                            break;
                    }
                }
            }

            var addNodes = gm.graph.FindNodes(Node.kOpCallFunction, "Add");
            foreach (var addNode in addNodes)
            {
                for (var index = 0; index < 2 && !addNode.erased; index++)
                {
                    var inputNode = (Node)addNode.args[index];
                    var constantNode = (Node)addNode.args[1 - index];
                    if (!constantNode.partialTensor.IsStatic())
                        continue;
                    var constantShape = constantNode.partialTensor.shape.ToTensorShape();
                    if (constantShape.length > 1 || !inputNode.partialTensor.shape.hasRank || constantShape.rank > inputNode.partialTensor.shape.rank)
                        continue;
                    var dataType = constantNode.partialTensor.dataType;
                    if (dataType == DataType.Float)
                        GraphPassUtil.ReplaceNode(addNode, "ScalarMad", new Argument[] { inputNode, (int)dataType, 1f, constantNode.partialTensor.Get<float>().value, 0, 0 });
                    if (dataType == DataType.Int)
                        GraphPassUtil.ReplaceNode(addNode, "ScalarMad", new Argument[] { inputNode, (int)dataType, 0f, 0f, 1, constantNode.partialTensor.Get<int>().value });
                }
            }

            var subNodes = gm.graph.FindNodes(Node.kOpCallFunction, "Sub");
            foreach (var subNode in subNodes)
            {
                var dataType = subNode.partialTensor.dataType;
                for (var index = 0; index < 2 && !subNode.erased; index++)
                {
                    var inputNode = (Node)subNode.args[index];
                    var constantNode = (Node)subNode.args[1 - index];
                    if (!constantNode.partialTensor.IsStatic())
                        continue;
                    var constantShape = constantNode.partialTensor.shape.ToTensorShape();
                    if (constantShape.length > 1 || !inputNode.partialTensor.shape.hasRank || constantShape.rank > inputNode.partialTensor.shape.rank)
                        continue;
                    if (dataType == DataType.Float)
                        GraphPassUtil.ReplaceNode(subNode, "ScalarMad", new Argument[] { inputNode, (int)dataType, 1f - 2f * index, (-1f + 2f * index) * constantNode.partialTensor.Get<float>().value, 0, 0 });
                    if (dataType == DataType.Int)
                        GraphPassUtil.ReplaceNode(subNode, "ScalarMad", new Argument[] { inputNode, (int)dataType, 0f, 0f, 1 - 2 * index, (-1 + 2 * index) * constantNode.partialTensor.Get<int>().value });
                }
            }

            var mulNodes = gm.graph.FindNodes(Node.kOpCallFunction, "Mul");
            foreach (var mulNode in mulNodes)
            {
                var dataType = mulNode.partialTensor.dataType;
                for (var index = 0; index < 2 && !mulNode.erased; index++)
                {
                    var inputNode = (Node)mulNode.args[index];
                    var constantNode = (Node)mulNode.args[1 - index];
                    if (!constantNode.partialTensor.IsStatic())
                        continue;
                    var constantShape = constantNode.partialTensor.shape.ToTensorShape();
                    if (constantShape.length > 1 || !inputNode.partialTensor.shape.hasRank || constantShape.rank > inputNode.partialTensor.shape.rank)
                        continue;
                    if (dataType == DataType.Float)
                        GraphPassUtil.ReplaceNode(mulNode, "ScalarMad", new Argument[] { inputNode, (int)dataType, constantNode.partialTensor.Get<float>().value, 0f, 0, 0 });
                    if (dataType == DataType.Int)
                        GraphPassUtil.ReplaceNode(mulNode, "ScalarMad", new Argument[] { inputNode, (int)dataType, 0f, 0f, constantNode.partialTensor.Get<int>().value, 0 });
                }
            }

            var divNodes = gm.graph.FindNodes(Node.kOpCallFunction, "Div");
            foreach (var divNode in divNodes)
            {
                var inputNode = (Node)divNode.args[0];
                var dataType = divNode.partialTensor.dataType;
                if (dataType != DataType.Float)
                    continue;
                var constantNode = (Node)divNode.args[1];
                if (!constantNode.partialTensor.IsStatic())
                    continue;
                var constantShape = constantNode.partialTensor.shape.ToTensorShape();
                if (constantShape.length > 1 || !inputNode.partialTensor.shape.hasRank || constantShape.rank > inputNode.partialTensor.shape.rank)
                    continue;
                GraphPassUtil.ReplaceNode(divNode, "ScalarMad", new Argument[] { inputNode, (int)dataType, 1f / constantNode.partialTensor.Get<float>().value, 0f, 0, 0 });
            }
        }
    }
}
