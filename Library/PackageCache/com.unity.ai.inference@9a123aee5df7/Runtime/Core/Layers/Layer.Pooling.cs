using System;
using UnityEngine;

namespace Unity.InferenceEngine.Layers
{
    /// <summary>
    /// Represents an `AveragePool` pooling layer. This calculates an output tensor by pooling the mean values of the input tensor across its spatial dimensions according to the given pool and stride values.
    /// </summary>
    [Operator(category = "Pooling")]
    partial class AveragePool : Layer
    {
        public int[] kernelShape;
        public int[] strides;
        public int[] pads;
        public AutoPad autopad;

        internal static PartialTensor InferPartial(PartialTensor input, int[] kernelShape, int[] strides, int[] pads, AutoPad autopad)
        {
            return PartialTensor.LocalPool(input, kernelShape, strides, pads, autopad);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            ShapeInference.UpdatePadForPoolAutoPadding(X.shape, kernelShape, strides, pads, false, autopad);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.ApplyPool(X.shape, kernelShape, strides, pads), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.AveragePool(X, O, kernelShape, strides, pads);
        }
    }

    /// <summary>
    /// Represents a `GlobalAveragePool` pooling layer. This calculates an output tensor by pooling the mean values of the input tensor across all of its spatial dimensions. The spatial dimensions of the output are size 1.
    /// </summary>
    [Operator(category = "Pooling")]
    partial class GlobalAveragePool : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return PartialTensor.GlobalPool(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.GlobalPool(X.shape), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.GlobalAveragePool(X, O);
        }
    }

    /// <summary>
    /// Represents a `GlobalMaxPool` pooling layer. This calculates an output tensor by pooling the maximum values of the input tensor across all of its spatial dimensions. The spatial dimensions of the output are size 1.
    /// </summary>
    [Operator(category = "Pooling")]
    partial class GlobalMaxPool : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return PartialTensor.GlobalPool(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.GlobalPool(X.shape), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.GlobalMaxPool(X, O);
        }
    }

    /// <summary>
    /// Represents a `MaxPool` pooling layer. This calculates an output tensor by pooling the maximum values of the input tensor across its spatial dimensions according to the given pool and stride values.
    /// </summary>
    [Operator(category = "Pooling")]
    partial class MaxPool : Layer
    {
        public int[] kernelShape;
        public int[] strides;
        public int[] pads;
        public AutoPad autopad;

        internal static PartialTensor InferPartial(PartialTensor input, int[] kernelShape, int[] strides, int[] pads, AutoPad autopad)
        {
            return PartialTensor.LocalPool(input, kernelShape, strides, pads, autopad);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            ShapeInference.UpdatePadForPoolAutoPadding(X.shape, kernelShape, strides, pads, false, autopad);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.ApplyPool(X.shape, kernelShape, strides, pads), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.MaxPool(X, O, kernelShape, strides, pads);
        }
    }
}
