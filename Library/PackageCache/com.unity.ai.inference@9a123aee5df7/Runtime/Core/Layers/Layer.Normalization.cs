using System;
using Unity.Collections;
using UnityEngine;

namespace Unity.InferenceEngine.Layers
{
    /// <summary>
    /// Represents an element-wise `ScaleBias` normalization layer: f(x, s, b) = x * s + b.
    /// </summary>
    [Operator(category = "Normalization")]
    [Inputs(names = new[] { "input", "scale", "bias" })]
    partial class ScaleBias : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor scale, PartialTensor bias)
        {
            var dataType = input.dataType;
            var shapeInput = input.shape;
            var shapeScale = scale.shape;
            var shapeBias = bias.shape;
            var c = DynamicTensorDim.Unknown;
            shapeScale.DeclareRank(1);
            c = DynamicTensorDim.MaxDefinedDim(c, shapeScale[0]);
            shapeBias.DeclareRank(1);
            c = DynamicTensorDim.MaxDefinedDim(c, shapeBias[0]);
            if (!shapeInput.hasRank)
                return PartialTensor.Create(dataType);

            Logger.AssertIsTrue(!shapeInput.hasRank || shapeInput.rank >= 2, "RankError: incorrect rank, expecting at least {0}, got {1}", 2, shapeInput.rank);

            var shapeOut = new DynamicTensorShape(shapeInput);
            shapeOut[1] = DynamicTensorDim.MaxDefinedDim(shapeOut[1], c);
            return PartialTensor.Create(dataType, shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.ScaleBias(X as Tensor<float>, ctx.storage.GetTensor(inputs[1]) as Tensor<float>, ctx.storage.GetTensor(inputs[2]) as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents an `InstanceNormalization` normalization layer. This computes the mean variance on the spatial dims of the input tensor and normalizes them according to `scale` and `bias` tensors.
    /// </summary>
    [Operator(category = "Normalization")]
    [Inputs(names = new[] { "input", "scale", "bias" })]
    partial class InstanceNormalization : Layer
    {
        public float epsilon;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor scale, PartialTensor bias, float epsilon)
        {
            var dataType = input.dataType;
            var shapeInput = input.shape;
            var shapeScale = scale.shape;
            var shapeBias = bias.shape;
            var c = DynamicTensorDim.Unknown;
            shapeScale.DeclareRank(1);
            c = DynamicTensorDim.MaxDefinedDim(c, shapeScale[0]);
            shapeBias.DeclareRank(1);
            c = DynamicTensorDim.MaxDefinedDim(c, shapeBias[0]);
            if (!shapeInput.hasRank)
                return PartialTensor.Create(dataType);

            Logger.AssertIsTrue(!shapeInput.hasRank || shapeInput.rank >= 2, "RankError: incorrect rank, expecting at least {0}, got {1}", 2, shapeInput.rank);
            shapeScale.DeclareRank(1);

            var shapeOut = new DynamicTensorShape(shapeInput);
            shapeOut[1] = DynamicTensorDim.MaxDefinedDim(shapeOut[1], c);
            return PartialTensor.Create(dataType, shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.InstanceNormalization(X as Tensor<float>, ctx.storage.GetTensor(inputs[1]) as Tensor<float>, ctx.storage.GetTensor(inputs[2]) as Tensor<float>, O, epsilon);
        }
    }

    /// <summary>
    /// Represents an `LayerNormalization` normalization layer. This computes the mean variance on the last dimension of the input tensor and normalizes it according to `scale` and `bias` tensors.
    /// </summary>
    [Operator(category = "Normalization")]
    [Inputs(names = new[] { "input", "scale", "bias" })]
    partial class LayerNormalization : Layer
    {
        public float epsilon;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor scale, PartialTensor bias, float epsilon)
        {
            var dataType = input.dataType;
            var shapeInput = input.shape;
            var shapeScale = scale.shape;
            var shapeBias = bias.shape;

            if (!shapeInput.hasRank)
                return PartialTensor.Create(dataType, DynamicTensorShape.DynamicRank);

            Logger.AssertIsTrue(shapeInput.rank >= 1, "RankError: incorrect rank, expecting at least {0}, got {1}", 1, shapeInput.rank);

            shapeScale.DeclareRank(1);
            shapeBias.DeclareRank(1);

            var shape = new DynamicTensorShape(shapeInput);
            shape[-1] = DynamicTensorDim.MaxDefinedDim(shape[-1], DynamicTensorDim.MaxDefinedDim(shapeScale[0], shapeBias[0]));
            return PartialTensor.Create(dataType, shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.LayerNormalization(X as Tensor<float>, ctx.storage.GetTensor(inputs[1]) as Tensor<float>, ctx.storage.GetTensor(inputs[2]) as Tensor<float>, O, epsilon);
        }
    }

    /// <summary>
    /// Represents a `RMSNormalization` normalization layer. This computes the mean square variance on the last dimension of the input tensor and normalizes it according to `scale` tensor.
    /// </summary>
    [Operator(category = "Normalization")]
    [Inputs(names = new[] { "input", "scale" })]
    partial class RMSNormalization : Layer
    {
        public float epsilon;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor scale, float epsilon)
        {
            var dataType = input.dataType;
            var shapeInput = input.shape;
            var shapeScale = scale.shape;

            if (!shapeInput.hasRank)
                return PartialTensor.Create(dataType, DynamicTensorShape.DynamicRank);

            Logger.AssertIsTrue(shapeInput.rank >= 1, "RankError: incorrect rank, expecting at least {0}, got {1}", 1, shapeInput.rank);

            shapeScale.DeclareRank(1);

            var shape = new DynamicTensorShape(shapeInput);
            shape[-1] = DynamicTensorDim.MaxDefinedDim(shape[-1], shapeScale[0]);
            return PartialTensor.Create(dataType, shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.RMSNormalization(X as Tensor<float>, ctx.storage.GetTensor(inputs[1]) as Tensor<float>, O, epsilon);
        }
    }

    /// <summary>
    /// Represents an `BatchNormalization` normalization layer. This computes the mean variance on the second dimension of the input tensor and normalizes it according to `scale` and `bias` tensors.
    /// </summary>
    [Operator(category = "Normalization")]
    [Inputs(names = new[] { "input", "scale", "bias", "mean", "variance" })]
    partial class BatchNormalization : Layer
    {
        public float epsilon;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor scale, PartialTensor bias, PartialTensor mean, PartialTensor variance, float epsilon)
        {
            var dataType = input.dataType;
            var shapeInput = input.shape;
            var shapeScale = scale.shape;
            var shapeBias = bias.shape;
            var shapeMean = mean.shape;
            var shapeVar = variance.shape;

            if (!shapeInput.hasRank)
                return PartialTensor.Create(dataType, DynamicTensorShape.DynamicRank);

            Logger.AssertIsTrue(shapeInput.rank >= 1, "RankError: incorrect rank, expecting at least {0}, got {1}", 1, shapeInput.rank);

            shapeScale.DeclareRank(1);
            shapeBias.DeclareRank(1);
            shapeMean.DeclareRank(1);
            shapeVar.DeclareRank(1);

            var shape = new DynamicTensorShape(shapeInput);
            if (shapeInput.rank > 1)
                shape[1] = DynamicTensorDim.MaxDefinedDim(shape[1], DynamicTensorDim.MaxDefinedDim(shapeScale[0], DynamicTensorDim.MaxDefinedDim(shapeBias[0], DynamicTensorDim.MaxDefinedDim(shapeMean[0], shapeVar[0]))));
            return PartialTensor.Create(dataType, shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.BatchNormalization(X as Tensor<float>, ctx.storage.GetTensor(inputs[1]) as Tensor<float>, ctx.storage.GetTensor(inputs[2]) as Tensor<float>, ctx.storage.GetTensor(inputs[3]) as Tensor<float>, ctx.storage.GetTensor(inputs[4]) as Tensor<float>, O, epsilon);
        }
    }

    /// <summary>
    /// Represents an `LRN` local response normalization layer. This normalizes the input tensor over local input regions.
    /// </summary>
    [Operator(category = "Normalization")]
    partial class LRN : Layer
    {
        public float alpha;
        public float beta;
        public float bias;
        public int count;

        internal static PartialTensor InferPartial(PartialTensor input, float alpha, float beta, float bias, int count)
        {
            if (input.shape.hasRank)
                Logger.AssertIsTrue(input.shape.rank >= 2, "RankError: incorrect rank, expecting at least {0}, got {1}", 2, input.shape.rank);

            return PartialTensor.Create(input.dataType, input.shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;

            // pixel we don't know which dim to pin
            var outputBackendType = ctx.backend.backendType;
            if (outputBackendType == BackendType.GPUPixel)
                outputBackendType = BackendType.CPU;

            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, outputBackendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;

            // https://papers.nips.cc/paper/4824-imagenet-classification-with-deep-convolutional-neural-networks.pdf
            // Like the above paper but we divide the sum by size to follow onnx and pytorch implementation
            // ONNX https://github.com/onnx/onnx/blob/master/docs/Operators.md#LRN
            // PYTORCH https://github.com/pytorch/pytorch/blob/1465970a343e61f2f2b104859ca7f5d7e03f5d02/torch/nn/functional.py#L2069
            // Tensorflow doesn't do that and follow the paper to the letter https://github.com/tensorflow/tensorflow/blob/e6faa845c51bb69465146d93646947fd2ba53efa/tensorflow/python/kernel_tests/lrn_op_test.py#L53
            // However they bake the division to alpha when exporting to ONNX https://github.com/onnx/tensorflow-onnx/blob/7c37ccb97e0fd478ce093910c4a1411b18e44fd7/tf2onnx/onnx_opset/math.py

            ctx.backend.LocalResponseNormalization(X, O, count, bias, alpha, beta);
        }
    }
}
