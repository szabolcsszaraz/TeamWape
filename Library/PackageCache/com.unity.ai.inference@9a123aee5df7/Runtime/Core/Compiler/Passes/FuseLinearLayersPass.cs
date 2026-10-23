using System;
using System.Collections.Generic;
using Unity.InferenceEngine.Graph;
using UnityEngine;

namespace Unity.InferenceEngine.Compiler.Passes.Optimization
{
    /// <summary>
    /// Fuses consecutive linear maths ops with constant scales and biases into individual ops where possible.
    /// Constants are recalculated and saved to the graph.
    /// </summary>
    class FuseLinearLayersPass : GraphPass
    {
        static readonly HashSet<string> s_LinearTargets = new() { "Dense", "Conv", "ScaleBias", "ScalarMad" };
        static readonly HashSet<string> s_LinearTargetsEitherInput = new() { "Add", "Sub", "Mul", "Div" };

        public override void Run(GraphModule gm)
        {
            using var ops = new CPUOps();

            var nodes = new Stack<Node>(gm.graph.Nodes());
            while (nodes.TryPop(out var next))
            {
                if (next.erased)
                    continue;
                if (!IsLinearOp(next, out var nextNoneConstantInput))
                    continue;
                var prev = (Node)next.args[nextNoneConstantInput];
                if (!IsLinearOp(prev, out var prevNoneConstantInput))
                    continue;
                // don't fuse if used by multiple nodes
                if (prev.users.Count != 1)
                    continue;
                if (HasActivation(prev))
                    continue;
                if (prev.target == "Add" && next.target == "Sub")
                {
                    var prevBias = GraphPassUtil.GetConstantInput(gm, prev, 1 - prevNoneConstantInput);
                    var nextBias = GraphPassUtil.GetConstantInput(gm, next, 1 - nextNoneConstantInput);

                    // y = (x + a) - b   =>   y = x - (b - a)
                    // y = b - (x + a)   =>   y = (b - a) - x
                    using Tensor bias = prevBias.dataType == DataType.Float ? ops.Sub(nextBias as Tensor<float>, prevBias as Tensor<float>) : ops.Sub(nextBias as Tensor<int>, prevBias as Tensor<int>);

                    var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Sub", nextNoneConstantInput == 1 ? new[] { biasNode, prev.args[prevNoneConstantInput] } : new[] { prev.args[prevNoneConstantInput], biasNode }));
                }
                else if (prev.target == "Sub" && next.target == "Add")
                {
                    var prevBias = GraphPassUtil.GetConstantInput(gm, prev, 1 - prevNoneConstantInput);
                    var nextBias = GraphPassUtil.GetConstantInput(gm, next, 1 - nextNoneConstantInput);

                    // y = (x - a) + b   =>   y = x + (b - a)
                    // y = (a - x) + b   =>   y = (b + a) - x
                    var rightSub = prevNoneConstantInput == 0;
                    Tensor bias;
                    if (prevBias is Tensor<int>)
                        bias = rightSub ? ops.Sub(nextBias as Tensor<int>, prevBias as Tensor<int>) : ops.Add(prevBias as Tensor<int>, nextBias as Tensor<int>);
                    else
                        bias = rightSub ? ops.Sub(nextBias as Tensor<float>, prevBias as Tensor<float>) : ops.Add(prevBias as Tensor<float>, nextBias as Tensor<float>);

                    var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                    if (rightSub)
                        nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Add", new[] { prev.args[prevNoneConstantInput], biasNode }));
                    else
                        nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Sub", new[] { biasNode, prev.args[prevNoneConstantInput] }));
                    bias.Dispose();
                }
                else if (prev.target == "Add" && next.target == "Add")
                {
                    var prevBias = GraphPassUtil.GetConstantInput(gm, prev, 1 - prevNoneConstantInput);
                    var nextBias = GraphPassUtil.GetConstantInput(gm, next, 1 - nextNoneConstantInput);

                    Tensor bias;
                    if (prevBias is Tensor<int>)
                        bias = ops.Add(prevBias as Tensor<int>, nextBias as Tensor<int>);
                    else
                        bias = ops.Add(prevBias as Tensor<float>, nextBias as Tensor<float>);

                    var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Add", new[] { prev.args[prevNoneConstantInput], biasNode }));

                    bias.Dispose();
                }
                else if (prev.target == "Mul" && next.target == "Mul")
                {
                    var prevScale = GraphPassUtil.GetConstantInput(gm, prev, 1 - prevNoneConstantInput);
                    var nextScale = GraphPassUtil.GetConstantInput(gm, next, 1 - nextNoneConstantInput);

                    Tensor scale;
                    if (prevScale is Tensor<int>)
                        scale = ops.Mul(prevScale as Tensor<int>, nextScale as Tensor<int>);
                    else
                        scale = ops.Mul(prevScale as Tensor<float>, nextScale as Tensor<float>);

                    var scaleNode = GraphPassUtil.AddConstant(gm, prev, scale);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Mul", new[] { prev.args[prevNoneConstantInput], scaleNode }));

                    scale.Dispose();
                }
                else if (prev.target == "ScaleBias" && next.target == "ScaleBias")
                {
                    var prevScale = GraphPassUtil.GetConstantInput(gm, prev, 1) as Tensor<float>;
                    var prevBias = GraphPassUtil.GetConstantInput(gm, prev, 2) as Tensor<float>;
                    var nextScale = GraphPassUtil.GetConstantInput(gm, next, 1) as Tensor<float>;
                    var nextBias = GraphPassUtil.GetConstantInput(gm, next, 2) as Tensor<float>;

                    // s1*(s0*x + b0)+b1 = s1*s0*x + s1*b0+b1
                    using var scale = ops.Mul(prevScale, nextScale);
                    using var mul = ops.Mul(prevBias, nextScale);
                    using var bias = ops.Add(mul, nextBias);
                    bias.Reshape(new TensorShape(bias.shape.length));

                    var scaleNode = GraphPassUtil.AddConstant(gm, prev, scale);
                    var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "ScaleBias", new[] { prev.args[0], scaleNode, biasNode }));
                }
                // TODO, previous version in repo wasn't correct as the check for mul scale shape was wrong
                // else if (prev.target == "Mul" && next.target == "Conv") { }
                else if (prev.target == "Conv" && next.target == "ScalarMad")
                {
                    var prevKernel = GraphPassUtil.GetConstantInput(gm, prev, 1) as Tensor<float>;
                    var prevBias = GraphPassUtil.GetConstantInput(gm, prev, 2) as Tensor<float>;
                    var nextScale = next.args[2].AsFloat;
                    var nextBias = next.args[3].AsFloat;

                    using var kernel = ops.ScalarMad(prevKernel, nextScale, 0f);
                    using var bias = prevBias == null ? ops.ConstantOfShape(new TensorShape(prevKernel.shape[0]), nextBias) : ops.ScalarMad(prevBias, nextScale, nextBias);

                    var kernelNode = GraphPassUtil.AddConstant(gm, prev, kernel);
                    var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Conv", new[] { prev.args[0], kernelNode, biasNode, prev.args[3], prev.args[4], prev.args[5], prev.args[6], prev.args[7], prev.args[8], prev.args[9] }));
                }
                else if (prev.target == "ScalarMad" && next.target == "Conv")
                {
                    var prevBias = prev.args[3].AsFloat;
                    if (prevBias != 0f)
                        continue;
                    var prevScale = prev.args[2].AsFloat;
                    var nextKernel = GraphPassUtil.GetConstantInput(gm, next, 1) as Tensor<float>;

                    using var kernel = ops.ScalarMad(nextKernel, prevScale, 0f);

                    var kernelNode = GraphPassUtil.AddConstant(gm, prev, kernel);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Conv", new[] { prev.args[0], kernelNode, next.args[2], next.args[3], next.args[4], next.args[5], next.args[6], next.args[7], next.args[8], next.args[9] }));
                }
                // TODO, previous version in repo wasn't correct as the check for mul scale shape was wrong
                // else if (prev.target == "Add" && next.target == "Conv") { }
                else if (prev.target == "Conv" && next.target == "Add")
                {
                    var prevKernel = GraphPassUtil.GetConstantInput(gm, prev, 1) as Tensor<float>;
                    var prevBias = GraphPassUtil.GetConstantInput(gm, prev, 2) as Tensor<float>;
                    var nextBias = GraphPassUtil.GetConstantInput(gm, next, 1 - nextNoneConstantInput) as Tensor<float>;
                    var spatialDims = prevKernel.shape.rank - 2;
                    // scalar add is also fine, but scalar add is converted to ScalarMad anyway before this pass which is handled elsewhere
                    if (nextBias.shape.length != prevKernel.shape[0] || nextBias.shape.rank > prevKernel.shape.rank || nextBias.shape.rank < spatialDims + 1 || nextBias.shape[-spatialDims - 1] != prevKernel.shape[0])
                        continue;
                    using var reshapedNextBias = ops.Reshape(nextBias, new TensorShape(prevKernel.shape[0]));
                    using var bias = prevBias == null ? reshapedNextBias : ops.Add(prevBias, reshapedNextBias);
                    var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Conv", new[] { prev.args[0], prev.args[1], biasNode, prev.args[3], prev.args[4], prev.args[5], prev.args[6], prev.args[7], prev.args[8], prev.args[9] }));
                }
                else if (prev.target == "Conv" && next.target == "ScaleBias")
                {
                    var prevKernel = GraphPassUtil.GetConstantInput(gm, prev, 1) as Tensor<float>;
                    var prevBias = GraphPassUtil.GetConstantInput(gm, prev, 2) as Tensor<float>;
                    var nextScale = GraphPassUtil.GetConstantInput(gm, next, 1) as Tensor<float>;
                    var nextBias = GraphPassUtil.GetConstantInput(gm, next, 2) as Tensor<float>;

                    // k = s1*k0
                    var prevShape = nextScale.shape;
                    var tempShape = TensorShape.Ones(prevKernel.shape.rank);
                    tempShape[0] = nextScale.shape[0];
                    nextScale.shape = tempShape;
                    using var kernel = ops.Mul(nextScale, prevKernel);
                    nextScale.shape = prevShape;

                    // b = s1*b0+b1
                    Tensor<float> bias;
                    if (prevBias is not null)
                    {
                        using var mul = ops.Mul(prevBias, nextScale);
                        bias = ops.Add(mul, nextBias);
                    }
                    else
                    {
                        bias = ops.Copy(nextBias);
                    }
                    bias.Reshape(new TensorShape(bias.shape.length));

                    var kernelNode = GraphPassUtil.AddConstant(gm, prev, kernel);
                    var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Conv", new[] { prev.args[0], kernelNode, biasNode, prev.args[3], prev.args[4], prev.args[5], prev.args[6], prev.args[7], prev.args[8], prev.args[9] }));
                    bias.Dispose();
                }
                // TODO, previous version in repo wasn't correct as the check for mul scale shape was wrong
                // else if (prev.target == "ScaleBias" && next.target == "Conv") { }
                else if (prev.target == "Dense" && next.target == "Dense")
                {
                    var prevWeights = GraphPassUtil.GetConstantInput(gm, prev, 1) as Tensor<float>;
                    var prevBias = GraphPassUtil.GetConstantInput(gm, prev, 2) as Tensor<float>;
                    var nextWeights = GraphPassUtil.GetConstantInput(gm, next, 1) as Tensor<float>;
                    var nextBias = GraphPassUtil.GetConstantInput(gm, next, 2) as Tensor<float>;

                    // W = W1 x W0
                    using var weights = ops.MatMul2D(prevWeights, nextWeights, false, false);
                    // b = W1 x b0 + b1
                    using var reshapedPrevBias = ops.Reshape(prevBias, new TensorShape(1, prevBias.shape[0]));
                    using var bias = ops.Dense(reshapedPrevBias, nextWeights, nextBias);
                    bias.Reshape(new TensorShape(bias.shape[1]));

                    var weightsNode = GraphPassUtil.AddConstant(gm, prev, weights);
                    var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Dense", new[] { prev.args[0], weightsNode, biasNode, next.args[3] }));
                }
                else if (prev.target == "ScalarMad" && next.target == "ScalarMad")
                {
                    if ((DataType)prev.args[1].AsInt == DataType.Int)
                    {
                        var prevScale = (int)prev.args[4];
                        var prevBias = (int)prev.args[5];
                        var nextScale = (int)next.args[4];
                        var nextBias = (int)next.args[5];
                        nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "ScalarMad", new[] { prev.args[0], (int)DataType.Int, 0f, 0f, nextScale * prevScale, nextScale * prevBias + nextBias }));
                    }
                    else
                    {
                        var prevScale = (float)prev.args[2];
                        var prevBias = (float)prev.args[3];
                        var nextScale = (float)next.args[2];
                        var nextBias = (float)next.args[3];
                        nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "ScalarMad", new[] { prev.args[0], (int)DataType.Float, nextScale * prevScale, nextScale * prevBias + nextBias, 0, 0 }));
                    }
                }
                else if (prev.target == "ScalarMad" && next.target == "Mul")
                {
                    var prevBias = (float)prev.args[3];
                    if ((DataType)prev.args[1].AsInt != DataType.Float || prevBias != 0)
                        continue;

                    var prevScale = (float)prev.args[2];
                    var nextScale = GraphPassUtil.GetConstantInput(gm, next, 1 - nextNoneConstantInput);
                    using var scale = ops.ScalarMad(nextScale as Tensor<float>, prevScale, 0);

                    var scaleNode = GraphPassUtil.AddConstant(gm, prev, scale);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Mul", new[] { prev.args[0], scaleNode }));
                }
                else if (prev.target == "Mul" && next.target == "ScalarMad")
                {
                    var nextBias = (float)next.args[3];
                    if ((DataType)next.args[1].AsInt != DataType.Float || nextBias != 0)
                        continue;

                    var prevScale = GraphPassUtil.GetConstantInput(gm, prev, 1 - prevNoneConstantInput);
                    var nextScale = (float)next.args[2];
                    using var scale = ops.ScalarMad(prevScale as Tensor<float>, nextScale, 0);

                    var scaleNode = GraphPassUtil.AddConstant(gm, prev, scale);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Mul", new[] { prev.args[prevNoneConstantInput], scaleNode }));
                }
                else if (prev.target == "ScalarMad" && next.target == "Add")
                {
                    var prevScale = (float)prev.args[2];
                    if ((DataType)prev.args[1].AsInt != DataType.Float || prevScale != 1f)
                        continue;

                    var prevBias = (float)prev.args[3];
                    var nextBias = GraphPassUtil.GetConstantInput(gm, next, 1 - nextNoneConstantInput);
                    using var bias = ops.ScalarMad(nextBias as Tensor<float>, 1, prevBias);

                    var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Add", new[] { prev.args[0], biasNode }));
                }
                else if (prev.target == "Add" && next.target == "ScalarMad")
                {
                    var nextScale = (float)next.args[2];
                    if ((DataType)next.args[1].AsInt != DataType.Float || nextScale != 1)
                        continue;

                    var prevBias = GraphPassUtil.GetConstantInput(gm, prev, 1 - prevNoneConstantInput);
                    var nextBias = (float)next.args[3];
                    using var bias = ops.ScalarMad(prevBias as Tensor<float>, 1, nextBias);

                    var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Add", new[] { prev.args[prevNoneConstantInput], biasNode }));
                }
                else if (prev.target == "ScalarMad" && next.target == "Sub")
                {
                    var prevScale = (float)prev.args[2];
                    if ((DataType)prev.args[1].AsInt != DataType.Float || prevScale != 1)
                        continue;

                    var prevBias = (float)prev.args[3];
                    var nextBias = GraphPassUtil.GetConstantInput(gm, next, 1 - nextNoneConstantInput);
                    if (nextNoneConstantInput == 0)
                    {
                        using var bias = ops.ScalarMad(nextBias as Tensor<float>, 1, -prevBias);
                        var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                        nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Sub", new[] { prev.args[0], biasNode }));
                    }
                    else
                    {
                        using var bias = ops.ScalarMad(nextBias as Tensor<float>, 1, -prevBias);
                        var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                        nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Sub", new[] { biasNode, prev.args[0] }));
                    }
                }
                else if (prev.target == "Sub" && next.target == "ScalarMad")
                {
                    var nextScale = (float)next.args[2];
                    if ((DataType)next.args[1].AsInt != DataType.Float || nextScale != 1)
                        continue;

                    var prevBias = GraphPassUtil.GetConstantInput(gm, prev, 1 - prevNoneConstantInput);
                    var nextBias = (float)next.args[3];

                    if (prevNoneConstantInput == 0)
                    {
                        using var bias = ops.ScalarMad(prevBias as Tensor<float>, 1, -nextBias);
                        var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                        nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Sub", new[] { prev.args[0], biasNode }));
                    }
                    else
                    {
                        using var bias = ops.ScalarMad(prevBias as Tensor<float>, 1, nextBias);
                        var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                        nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Sub", new[] { biasNode, prev.args[1] }));
                    }
                }
                else if (prev.target == "Add" && next.target == "ScalarMad")
                {
                    var nextScale = (float)next.args[2];
                    if ((DataType)next.args[1].AsInt != DataType.Float || nextScale != 1)
                        continue;

                    var prevBias = GraphPassUtil.GetConstantInput(gm, prev, 1 - prevNoneConstantInput);
                    var nextBias = (float)next.args[3];
                    using var bias = ops.ScalarMad(prevBias as Tensor<float>, 1, nextBias);

                    var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Add", new[] { prev.args[prevNoneConstantInput], biasNode }));
                }
                else if (prev.target == "ScalarMad" && next.target == "Div")
                {
                    var prevBias = (float)prev.args[3];
                    if ((DataType)prev.args[1].AsInt != DataType.Float || prevBias != 0)
                        continue;

                    var prevScale = (float)prev.args[2];
                    var nextScale = GraphPassUtil.GetConstantInput(gm, next, 1 - nextNoneConstantInput);
                    using var scale = ops.ScalarMad(nextScale as Tensor<float>, 1 / prevScale, 0);
                    var scaleNode = GraphPassUtil.AddConstant(gm, prev, scale);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Div", nextNoneConstantInput == 0 ? new[] { prev.args[0], scaleNode } : new[] { scaleNode, prev.args[0] }));
                }
                else if (prev.target == "Div" && next.target == "ScalarMad")
                {
                    var nextBias = (float)next.args[3];
                    if ((DataType)next.args[1].AsInt != DataType.Float || nextBias != 0)
                        continue;

                    var prevScale = GraphPassUtil.GetConstantInput(gm, prev, 1 - prevNoneConstantInput);
                    var nextScale = (float)next.args[2];

                    if (prevNoneConstantInput == 0)
                    {
                        // y = (x / a) * b    =>    y = (x / (a / b))
                        using var scale = ops.ScalarMad(prevScale as Tensor<float>, 1 / nextScale, 0);
                        var scaleNode = GraphPassUtil.AddConstant(gm, prev, scale);
                        nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Div", new[] { prev.args[0], scaleNode }));
                    }
                    else
                    {
                        // y = (a / x) * b    =>    y = (a * b) / x
                        using var scale = ops.ScalarMad(prevScale as Tensor<float>, nextScale, 0);
                        var scaleNode = GraphPassUtil.AddConstant(gm, prev, scale);
                        nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Div", new[] { scaleNode, prev.args[1] }));
                    }
                }
                else if (prev.target == "Dense" && next.target == "ScaleBias")
                {
                    if (!prev.partialTensor.shape.hasRank || prev.partialTensor.shape.rank != 2)
                        continue;

                    var prevWeights = GraphPassUtil.GetConstantInput(gm, prev, 1) as Tensor<float>;
                    var prevBias = GraphPassUtil.GetConstantInput(gm, prev, 2) as Tensor<float>;
                    var nextScale = GraphPassUtil.GetConstantInput(gm, next, 1) as Tensor<float>;
                    var nextBias = GraphPassUtil.GetConstantInput(gm, next, 2) as Tensor<float>;

                    using var weights = ops.Mul(prevWeights, nextScale);
                    using var scaledBias = ops.Mul(prevBias, nextScale);
                    using var bias = ops.Add(scaledBias, nextBias);

                    var weightsNode = GraphPassUtil.AddConstant(gm, prev, weights);
                    var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Dense", new[] { prev.args[0], weightsNode, biasNode, prev.args[3] }));
                }
                else if (prev.target == "ScaleBias" && next.target == "Dense")
                {
                    var inputShape = ((Node)prev.args[0]).partialTensor.shape;
                    if (!inputShape.hasRank || inputShape.rank != 2)
                        continue;

                    var prevScale = GraphPassUtil.GetConstantInput(gm, prev, 1) as Tensor<float>;
                    var prevBias = GraphPassUtil.GetConstantInput(gm, prev, 2) as Tensor<float>;
                    var nextWeights = GraphPassUtil.GetConstantInput(gm, next, 1) as Tensor<float>;
                    var nextBias = GraphPassUtil.GetConstantInput(gm, next, 2) as Tensor<float>;

                    using var reshapedPrevScale = ops.Reshape(prevScale, new TensorShape(nextWeights.shape[0], 1));
                    using var weights = ops.Mul(nextWeights, reshapedPrevScale);
                    using var reshapedPrevBias = ops.Reshape(prevBias, new TensorShape(1, nextWeights.shape[0]));
                    using var biasFromPrevBias = ops.MatMul2D(reshapedPrevBias, nextWeights, false, false);
                    using var bias = ops.Add(nextBias, biasFromPrevBias);

                    var weightsNode = GraphPassUtil.AddConstant(gm, prev, weights);
                    var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Dense", new[] { prev.args[0], weightsNode, biasNode, next.args[3] }));
                }
                else if (prev.target == "Dense" && next.target == "ScalarMad")
                {
                    var prevWeights = GraphPassUtil.GetConstantInput(gm, prev, 1) as Tensor<float>;
                    var prevBias = GraphPassUtil.GetConstantInput(gm, prev, 2) as Tensor<float>;
                    var nextScale = (float)next.args[2];
                    var nextBias = (float)next.args[3];

                    using var weights = ops.ScalarMad(prevWeights, nextScale, 0f);
                    using var bias = ops.ScalarMad(prevBias, nextScale, nextBias);

                    var weightsNode = GraphPassUtil.AddConstant(gm, prev, weights);
                    var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Dense", new[] { prev.args[0], weightsNode, biasNode, prev.args[3] }));
                }
                else if (prev.target == "ScalarMad" && next.target == "Dense")
                {
                    var prevScale = (float)prev.args[2];
                    var prevBias = (float)prev.args[3];
                    var nextWeights = GraphPassUtil.GetConstantInput(gm, next, 1) as Tensor<float>;
                    var nextBias = GraphPassUtil.GetConstantInput(gm, next, 2) as Tensor<float>;

                    using var weights = ops.ScalarMad(nextWeights, prevScale, 0f);
                    using var expandedPrevBias = ops.ConstantOfShape(new TensorShape(1, nextWeights.shape[0]), prevBias);
                    using var biasFromPrevBias = ops.MatMul2D(expandedPrevBias, nextWeights, false, false);
                    using var bias = ops.Add(nextBias, biasFromPrevBias);

                    var weightsNode = GraphPassUtil.AddConstant(gm, prev, weights);
                    var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "Dense", new[] { prev.args[0], weightsNode, biasNode, next.args[3] }));
                }
                else if (prev.target == "ScaleBias" && next.target == "ScalarMad")
                {
                    var prevScale = GraphPassUtil.GetConstantInput(gm, prev, 1) as Tensor<float>;
                    var prevBias = GraphPassUtil.GetConstantInput(gm, prev, 2) as Tensor<float>;
                    var nextScale = (float)next.args[2];
                    var nextBias = (float)next.args[3];

                    // y = c * (a * x + b) + d    =>    y = (a * c) * x + c * b + d
                    using var scale = ops.ScalarMad(prevScale, nextScale, 0f);
                    using var bias = ops.ScalarMad(prevBias, nextScale, nextBias);

                    var scaleNode = GraphPassUtil.AddConstant(gm, prev, scale);
                    var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "ScaleBias", new[] { prev.args[0], scaleNode, biasNode }));
                }
                else if (prev.target == "ScalarMad" && next.target == "ScaleBias")
                {
                    var prevScale = (float)prev.args[2];
                    var prevBias = (float)prev.args[3];
                    var nextScale = GraphPassUtil.GetConstantInput(gm, next, 1) as Tensor<float>;
                    var nextBias = GraphPassUtil.GetConstantInput(gm, next, 2) as Tensor<float>;

                    // y = c * (a * x + b) + d    =>    y = (a * c) * x + c * b + d
                    using var scale = ops.ScalarMad(nextScale, prevScale, 0f);
                    using var scaledPrevBias = ops.ScalarMad(nextScale, prevBias, 0);
                    using var bias = ops.Add(nextBias, scaledPrevBias);

                    var scaleNode = GraphPassUtil.AddConstant(gm, prev, scale);
                    var biasNode = GraphPassUtil.AddConstant(gm, prev, bias);
                    nodes.Push(GraphPassUtil.ReplaceNodes(prev, next, "ScaleBias", new[] { prev.args[0], scaleNode, biasNode }));
                }
            }
        }

        static bool IsLinearOp(Node node, out int nonConstantInputIndex)
        {
            nonConstantInputIndex = -1;
            if (node.op != Node.kOpCallFunction)
                return false;
            var isLinearTarget = s_LinearTargets.Contains(node.target);
            var isLinearTargetEitherInput = s_LinearTargetsEitherInput.Contains(node.target);
            if (!isLinearTarget && !isLinearTargetEitherInput)
                return false;
            for (var i = 0; i < node.args.Length; i++)
            {
                if (node.args[i] is null)
                    continue;
                if (!node.args[i].IsNode)
                    continue;
                if (node.args[i].AsNode.op is Node.kOpGetAttr)
                    continue;
                if (nonConstantInputIndex != -1)
                    return false;
                if (isLinearTarget && i > 0)
                    return false;
                nonConstantInputIndex = i;
            }
            // if all inputs are constant then this will be handled by constant fusing
            if (nonConstantInputIndex == -1)
                return false;
            return true;
        }

        static bool HasActivation(Node node)
        {
            if (node.target is not ("Dense" or "Conv"))
                return false;
            return (Layers.FusableActivation)node.args[^1].AsInt != Layers.FusableActivation.None;
        }
    }
}
