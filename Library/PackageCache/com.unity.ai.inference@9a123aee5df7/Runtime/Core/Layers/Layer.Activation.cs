using System;

namespace Unity.InferenceEngine.Layers
{
    /// <summary>
    /// Represents an element-wise `Celu` activation layer: f(x) = max(0, x) + min(0, alpha * (exp(x / alpha) - 1)).
    /// </summary>
    [Operator(category = "Activation")]
    partial class Celu : Layer
    {
        public float alpha;

        internal static PartialTensor InferPartial(PartialTensor input, float alpha)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Celu(X, O, alpha);
        }
    }

    /// <summary>
    /// Represents an element-wise `Elu` activation layer: f(x) = x if x >= 0, otherwise f(x) = alpha * (e^x - 1).
    /// </summary>
    [Operator(category = "Activation")]
    partial class Elu : Layer
    {
        public float alpha;

        internal static PartialTensor InferPartial(PartialTensor input, float alpha)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Elu(X as Tensor<float>, O, alpha);
        }
    }

    /// <summary>
    /// Represents an element-wise `Gelu` activation layer: f(x) = x / 2 * (1 + erf(x / sqrt(2))).
    /// </summary>
    [Operator(category = "Activation")]
    partial class Gelu : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Gelu(X, O);
        }
    }

    [Operator(category = "Activation")]
    partial class GeluFast : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.GeluFast(X, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Erf` activation layer: f(x) = erf(x).
    /// </summary>
    [Operator(category = "Activation")]
    partial class Erf : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Erf(X as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents a `Hardmax` activation layer along an axis: f(x, axis) = 1 if x is the first maximum value along the specified axis, otherwise f(x) = 0.
    /// </summary>
    [Operator(category = "Activation")]
    partial class Hardmax : Layer
    {
        public int axis;

        internal static PartialTensor InferPartial(PartialTensor input, int axis)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Hardmax(X as Tensor<float>, O, axis);
        }
    }

    /// <summary>
    /// Represents an element-wise `HardSigmoid` activation layer: f(x) = clamp(alpha * x + beta, 0, 1).
    /// </summary>
    [Operator(category = "Activation")]
    partial class HardSigmoid : Layer
    {
        public float alpha;
        public float beta;

        internal static PartialTensor InferPartial(PartialTensor input, float alpha, float beta)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.HardSigmoid(X, O, alpha, beta);
        }
    }

    /// <summary>
    /// Represents an element-wise `HardSwish` activation layer: f(x) = x * max(0, min(1, alpha * x + beta)) = x * HardSigmoid(x, alpha, beta), where alpha = 1/6 and beta = 0.5.
    /// </summary>
    [Operator(category = "Activation")]
    partial class HardSwish : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.HardSwish(X, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `HardTanh` activation layer: f(x) = minVal if x &lt; minVal. f(x) = maxVal if x &gt; maxVal. Otherwise f(x) = x.
    /// </summary>
    [Operator(category = "Activation")]
    partial class HardTanh : Layer
    {
        public float minVal;
        public float maxVal;

        internal static PartialTensor InferPartial(PartialTensor input, float minVal, float maxVal)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.HardTanh(X as Tensor<float>, O, minVal, maxVal);
        }
    }

    /// <summary>
    /// Represents an element-wise `LeakyRelu` activation layer: f(x) = x if x >= 0, otherwise f(x) = alpha * x.
    /// </summary>
    [Operator(category = "Activation")]
    partial class LeakyRelu : Layer
    {
        public float alpha;

        internal static PartialTensor InferPartial(PartialTensor input, float alpha)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.LeakyRelu(X, O, alpha);
        }
    }

    /// <summary>
    /// Represents an element-wise `Mish` activation layer: f(x) = x * tanh(softplus(x)).
    /// </summary>
    [Operator(category = "Activation")]
    partial class Mish : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Mish(X, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `PRelu` activation layer: f(x) = x if x >= 0, otherwise f(x) = slope * x.
    ///
    /// The slope tensor must be unidirectional broadcastable to x.
    /// </summary>
    [Operator(category = "Activation")]
    [Inputs(names = new[] { "input", "slope" })]
    partial class PRelu : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor slope)
        {
            var shapeInput = input.shape;
            var shapeSlope = slope.shape;
            if (!shapeInput.hasRank)
                return new PartialTensor<float>();

            if (!shapeSlope.hasRank)
                return new PartialTensor<float>(shapeInput);

            Logger.AssertIsTrue(shapeSlope.rank <= shapeInput.rank, "PRelu.InputError: slope shape must be unidirectional broadcastable to input");
            var numInitialDims = shapeInput.rank - shapeSlope.rank;
            var shapeOut = new DynamicTensorShape(shapeInput);

            for (var i = 0; i < shapeSlope.rank; i++)
            {
                if (shapeSlope[i] == 1)
                    continue;
                shapeOut[numInitialDims + i] = DynamicTensorDim.MaxDefinedDim(shapeOut[numInitialDims + i], shapeSlope[i]);
            }

            return new PartialTensor<float>(shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var slope = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.PRelu(X as Tensor<float>, slope as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Relu` activation layer: f(x) = max(0, x).
    /// </summary>
    [Operator(category = "Activation")]
    partial class Relu : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Relu(X as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Relu6` activation layer: f(x) = clamp(x, 0, 6).
    /// </summary>
    [Operator(category = "Activation")]
    partial class Relu6 : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Relu6(X, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Selu` activation layer: f(x) = gamma * x if x >= 0, otherwise f(x) = (alpha * e^x - alpha).
    /// </summary>
    [Operator(category = "Activation")]
    partial class Selu : Layer
    {
        public float alpha;
        public float gamma;

        internal static PartialTensor InferPartial(PartialTensor input, float alpha, float gamma)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Selu(X, O, alpha, gamma);
        }
    }

    /// <summary>
    /// Represents an element-wise `Sigmoid` activation layer: f(x) = 1/(1 + e^(-x)).
    /// </summary>
    [Operator(category = "Activation")]
    partial class Sigmoid : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Sigmoid(X as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Softplus` activation layer: f(x) = ln(e^x + 1).
    /// </summary>
    [Operator(category = "Activation")]
    partial class Softplus : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Softplus(X as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Softsign` activation layer: f(x) = x/(|x| + 1).
    /// </summary>
    [Operator(category = "Activation")]
    partial class Softsign : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Softsign(X as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Swish` activation layer. f(x) = sigmoid(alpha * x) * x = x / (1 + e^{-alpha * x}).
    /// </summary>
    [Operator(category = "Activation")]
    partial class Swish : Layer
    {
        public float alpha;

        internal static PartialTensor InferPartial(PartialTensor input, float alpha)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Swish(X as Tensor<float>, O, alpha);
        }
    }

    /// <summary>
    /// Represents an element-wise `Tanh` activation layer: f(x) = tanh(x).
    /// </summary>
    [Operator(category = "Activation")]
    partial class Tanh : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Tanh(X as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `ThresholdedRelu` activation layer: f(x) = x if x > alpha, otherwise f(x) = 0.
    /// </summary>
    [Operator(category = "Activation")]
    partial class ThresholdedRelu : Layer
    {
        public float alpha;

        internal static PartialTensor InferPartial(PartialTensor input, float alpha)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.ThresholdedRelu(X as Tensor<float>, O, alpha);
        }
    }
}
