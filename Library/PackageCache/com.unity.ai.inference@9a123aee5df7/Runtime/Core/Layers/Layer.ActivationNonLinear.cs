using System;

namespace Unity.InferenceEngine.Layers
{
    /// <summary>
    /// Represents a `LogSoftmax` activation layer along an axis: f(x, axis) = log(Softmax(x, axis)).
    /// </summary>
    [Operator(category = "ActivationNonLinear")]
    partial class LogSoftmax : Layer
    {
        public int axis;

        internal static PartialTensor InferPartial(PartialTensor input, int axis)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.LogSoftmax(X, O, axis);
        }
    }

    /// <summary>
    /// Represents a `Softmax` activation layer along an axis: f(x, axis) = exp(X) / ReduceSum(exp(X), axis).
    /// </summary>
    [Operator(category = "ActivationNonLinear")]
    partial class Softmax : Layer
    {
        public int axis;

        internal static PartialTensor InferPartial(PartialTensor input, int axis)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Softmax(X, O, axis);
        }
    }
}
