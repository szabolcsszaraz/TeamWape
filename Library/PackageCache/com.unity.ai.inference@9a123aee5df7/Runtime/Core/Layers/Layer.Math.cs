using System;
using Unity.Mathematics;

namespace Unity.InferenceEngine.Layers
{
    /// <summary>
    /// Represents an element-wise `Abs` math layer: f(x) = |x|.
    /// </summary>
    [Operator(category = "Math")]
    partial class Abs : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (X is Tensor<int>)
                ctx.backend.Abs(X as Tensor<int>, O as Tensor<int>);
            else
                ctx.backend.Abs(X as Tensor<float>, O as Tensor<float>);
        }
    }

    /// <summary>
    /// Represents an element-wise `Add` math operation layer: f(a, b) = a + b.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "a", "b" })]
    partial class Add : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            if (a is PartialTensor<int>)
                return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, (x, y) => x + y);
            else
                return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<float>, (x, y) => x + y);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], TensorShapeHelper.BroadcastShape(A, B), A.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (A is Tensor<int>)
                ctx.backend.Add(A as Tensor<int>, B as Tensor<int>, O as Tensor<int>);
            else
                ctx.backend.Add(A as Tensor<float>, B as Tensor<float>, O as Tensor<float>);
        }
    }

    /// <summary>
    /// Represents an element-wise `Add` math operation layer: f(a, b) = a + b.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "y", "x" })]
    partial class Atan2 : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor y, PartialTensor x)
        {
            return PartialTensor.Create(x.dataType, x.shape.Broadcast(y.shape));
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var y = ctx.storage.GetTensor(inputs[0]);
            var x = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], TensorShapeHelper.BroadcastShape(x, y), x.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Atan2(y as Tensor<float>, x as Tensor<float>, O as Tensor<float>);
        }
    }

    /// <summary>
    /// Represents an element-wise `BitwiseAnd` math operation layer: f(a, b) = a &#38; b.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "a", "b" })]
    partial class BitwiseAnd : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, (x, y) => x & y);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]) as Tensor<int>;
            var B = ctx.storage.GetTensor(inputs[1]) as Tensor<int>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], TensorShapeHelper.BroadcastShape(A, B), DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.BitwiseAnd(A, B, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `BitwiseNot` math layer: f(x) = ~x.
    /// </summary>
    [Operator(category = "Math")]
    partial class BitwiseNot : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return PartialTensor.Unary(input as PartialTensor<int>, x => ~x);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<int>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.BitwiseNot(X, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `BitwiseOr` math operation layer: f(a, b) = a &#124; b.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "a", "b" })]
    partial class BitwiseOr : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, (x, y) => x | y);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]) as Tensor<int>;
            var B = ctx.storage.GetTensor(inputs[1]) as Tensor<int>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], TensorShapeHelper.BroadcastShape(A, B), DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.BitwiseOr(A, B, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `BitwiseXor` math operation layer: f(a, b) = a ^ b.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "a", "b" })]
    partial class BitwiseXor : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, (x, y) => x ^ y);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]) as Tensor<int>;
            var B = ctx.storage.GetTensor(inputs[1]) as Tensor<int>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], TensorShapeHelper.BroadcastShape(A, B), DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.BitwiseXor(A, B, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Ceil` math layer: f(x) = ceil(x).
    /// </summary>
    [Operator(category = "Math")]
    partial class Ceil : Layer
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
            ctx.backend.Ceil(X, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Clip` math layer: f(x, xmin, xmax) = min(max(x, xmin), xmax)
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "input", "min", "max" })]
    partial class Clip : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor min, PartialTensor max)
        {
            return PartialTensor.Create(input.dataType, input.shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var min = ctx.storage.GetTensor(inputs[1]);
            var max = ctx.storage.GetTensor(inputs[2]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (X is Tensor<int>)
            {
                ctx.backend.Clip(X as Tensor<int>, min as Tensor<int>, max as Tensor<int>, O as Tensor<int>);
            }
            else
            {
                ctx.backend.Clip(X as Tensor<float>, min as Tensor<float>, max as Tensor<float>, O as Tensor<float>);
            }
        }
    }

    /// <summary>
    /// Represents a `CumSum` math layer that performs the cumulative sum along a given axis.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "input", "axis" }, inputCPURead = new[] { 1 })]
    partial class CumSum : Layer
    {
        public bool reverse;
        public bool exclusive;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor axis, bool reverse, bool exclusive)
        {
            return PartialTensor.Create(input.dataType, input.shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            var axis = ctx.storage.GetInt(inputs[1]);
            if (X is Tensor<int>)
                ctx.backend.CumSum(X as Tensor<int>, O as Tensor<int>, axis, reverse, exclusive);
            else
                ctx.backend.CumSum(X as Tensor<float>, O as Tensor<float>, axis, reverse, exclusive);
        }
    }

    /// <summary>
    /// Represents a `Dense` math operation layer which performs a matrix multiplication operation: f(x, w, b) = X x W + B.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "input", "weights", "bias" })]
    partial class Dense : Layer
    {
        public FusableActivation fusedActivation;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor weights, PartialTensor bias, FusableActivation fusedActivation)
        {
            var shapeOut = input.shape.MatMul(weights.shape);
            if (shapeOut.hasRank)
                shapeOut[-1] = DynamicTensorDim.MaxDefinedDim(bias.shape[-1], shapeOut[-1]);
            return new PartialTensor<float>(shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var W = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape.MatMul(W.shape), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Dense(X as Tensor<float>, W as Tensor<float>, ctx.storage.GetTensor(inputs[2]) as Tensor<float>, O, fusedActivation);
        }
    }

    [Operator(category = "Math")]
    [Inputs(names = new[] { "input", "weights", "bias" })]
    partial class DenseBatched : Layer
    {
        public FusableActivation fusedActivation;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor weights, PartialTensor bias, FusableActivation fusedActivation)
        {
            var shapeOut = input.shape.MatMul(weights.shape);
            if (shapeOut.hasRank)
                shapeOut[-1] = DynamicTensorDim.MaxDefinedDim(bias.shape[-1], shapeOut[-1]);
            return new PartialTensor<float>(shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var W = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape.MatMul(W.shape), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.DenseBatched(X as Tensor<float>, W as Tensor<float>, ctx.storage.GetTensor(inputs[2]) as Tensor<float>, O, fusedActivation);
        }
    }

    /// <summary>
    /// Represents an element-wise `Div` math operation layer: f(a, b) = a / b.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "a", "b" })]
    partial class Div : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            if (a is PartialTensor<int>)
                return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, (x, y) => x / y);
            else
                return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<float>, (x, y) => x / y);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], TensorShapeHelper.BroadcastShape(A, B), A.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (A is Tensor<int>)
                ctx.backend.TruncDiv(A as Tensor<int>, B as Tensor<int>, O as Tensor<int>);
            else
                ctx.backend.Div(A as Tensor<float>, B as Tensor<float>, O as Tensor<float>);
        }
    }

    /// <summary>
    /// Represents an `Einsum` math operation layer.
    /// </summary>
    /// <description>
    /// The Einsum operator evaluates algebraic tensor operations on a sequence of tensors, using the Einstein summation convention. The equation string contains a comma-separated sequence of lower case letters. Each term corresponds to an operand tensor, and the characters within the terms correspond to operands dimensions.
    /// This sequence may be followed by "->" to separate the left and right hand side of the equation. If the equation contains "->" followed by the right-hand side, the explicit (not classical) form of the Einstein summation is performed, and the right-hand side indices indicate output tensor dimensions. In other cases, output indices are (implicitly) set to the alphabetically sorted sequence of indices appearing exactly once in the equation.
    /// When a dimension character is repeated in the left-hand side, it represents summation along the dimension.
    /// The equation may contain ellipsis ("...") to enable broadcasting. Ellipsis must indicate a fixed number of dimensions. Specifically, every occurrence of ellipsis in the equation must represent the same number of dimensions. The right-hand side may contain exactly one ellipsis. In implicit mode, the ellipsis dimensions are set to the beginning of the output. The equation string may contain space (U+0020) character.
    /// </description>
    [Operator(category = "Math")]
    [Inputs(isVariadic = true)]
    partial class Einsum : Layer
    {
        public string equation;

        TensorShape[] operandShapes;
        TensorIndex[] operandIndices;
        Tensor<float>[] operandTensors;

        void Initialize()
        {
            operandShapes = new TensorShape[inputs.Length];
            operandIndices = new TensorIndex[inputs.Length];
            operandTensors = new Tensor<float>[inputs.Length];
        }

        internal static PartialTensor InferPartial(PartialTensor[] inputs, string equation)
        {
            var operandIndices = new TensorIndex[inputs.Length];
            var inputShapes = new DynamicTensorShape[inputs.Length];
            for (var i = 0; i < inputShapes.Length; i++)
                inputShapes[i] = inputs[i].shape;
            var shape = EinsumHelper.ParseEquationStringShape(equation, inputShapes, ref operandIndices, out _, out _);
            return new PartialTensor<float>(shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            if (operandShapes is null)
                Initialize();
            for (int i = 0; i < inputs.Length; i++)
            {
                operandTensors[i] = ctx.storage.GetTensor(inputs[i]) as Tensor<float>;
                operandShapes[i] = operandTensors[i].shape;
            }
            EinsumHelper.ParseEquationString(equation, operandShapes, ref operandIndices, out var outputIndices, out var outputShape, out var sumIndices, out var sumShape, out var numIndices);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], outputShape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            if (inputs.Length > 2)
                CPUBackend.EinsumND(operandTensors, O, operandShapes, operandIndices, outputIndices, outputShape, sumIndices, sumShape, numIndices);
            else
                ctx.backend.Einsum(operandTensors, O, operandIndices, outputIndices, sumIndices, sumShape);
        }
    }

    /// <summary>
    /// Represents an element-wise `Exp` math layer: f(x) = e^{x}.
    /// </summary>
    [Operator(category = "Math")]
    partial class Exp : Layer
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
            ctx.backend.Exp(X, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Expm1` math layer: f(x) = e^{x} - 1.
    /// </summary>
    [Operator(category = "Math")]
    partial class Expm1 : Layer
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
            ctx.backend.Expm1(X, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Floor` math layer: f(x) = floor(x).
    /// </summary>
    [Operator(category = "Math")]
    partial class Floor : Layer
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
            ctx.backend.Floor(X, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `FloorDiv` math operation layer: f(a, b) = floor(a / b).
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "a", "b" })]
    partial class FloorDiv : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            if (a is PartialTensor<int>)
                return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, (x, y) =>
                {
                    if (y == PartialTensorElement<int>.Zero)
                        throw new DivideByZeroException();
                    if (x == PartialTensorElement<int>.Zero)
                        return x;
                    if (y == PartialTensorElement<int>.One)
                        return x;
                    if (x == y)
                        return PartialTensorElement<int>.One;
                    if (x.isValue && y.isValue)
                        return PartialTensorElement<int>.Value((x.value / y.value) - (((x.value ^ y.value) < 0 && (x.value % y.value) != 0) ? 1 : 0));
                    return PartialTensorElement<int>.Unknown;
                });
            else
                return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<float>, (x, y) =>
                {
                    if (y == PartialTensorElement<float>.Zero)
                        throw new DivideByZeroException();
                    if (x == PartialTensorElement<float>.Zero)
                        return x;
                    if (y == PartialTensorElement<float>.One)
                        return x;
                    if (x == y)
                        return PartialTensorElement<float>.One;
                    if (x.isValue && y.isValue)
                        return PartialTensorElement<float>.Value(math.floor(x.value / y.value));
                    return PartialTensorElement<float>.Unknown;
                });
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], TensorShapeHelper.BroadcastShape(A, B), A.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (A is Tensor<int>)
                ctx.backend.FloorDiv(A as Tensor<int>, B as Tensor<int>, O as Tensor<int>);
            else
                ctx.backend.FloorDiv(A as Tensor<float>, B as Tensor<float>, O as Tensor<float>);
        }
    }

    /// <summary>
    /// Represents an element-wise `Log` math layer: f(x) = log(x).
    /// </summary>
    [Operator(category = "Math")]
    partial class Log : Layer
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
            ctx.backend.Log(X, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Log10` math layer: f(x) = log10(x).
    /// </summary>
    [Operator(category = "Math")]
    partial class Log10 : Layer
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
            ctx.backend.Log10(X, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Log1p` math layer: f(x) = log(1 + x).
    /// </summary>
    [Operator(category = "Math")]
    partial class Log1p : Layer
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
            ctx.backend.Log1p(X, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Log2` math layer: f(x) = log2(x).
    /// </summary>
    [Operator(category = "Math")]
    partial class Log2 : Layer
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
            ctx.backend.Log2(X, O);
        }
    }

    /// <summary>
    /// Represents a `MatMul` math operation layer which performs a matrix multiplication operation: f(a, b) = a x b.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "input0", "input1" })]
    partial class MatMul : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input0, PartialTensor input1)
        {
            return PartialTensor.Create(input0.dataType, input0.shape.MatMul(input1.shape));
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], A.shape.MatMul(B.shape), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            if (A.shape.HasZeroDims() || B.shape.HasZeroDims())
                ctx.backend.MemSet(O, 0.0f);
            else
                ctx.backend.MatMul(A as Tensor<float>, B as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents a `MatMul2D` math operation layer which performs a matrix multiplication operation with optional transposes: f(a, b) = a' x b'.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "input0", "input1" })]
    partial class MatMul2D : Layer
    {
        public bool transposeA;
        public bool transposeB;

        internal static PartialTensor InferPartial(PartialTensor input0, PartialTensor input1, bool transposeA, bool transposeB)
        {
            var shapeA = input0.shape;
            var shapeB = input1.shape;

            shapeA.DeclareRank(2);
            shapeB.DeclareRank(2);

            var mulXDim = transposeA ? shapeA[0] : shapeA[1];
            var mulYDim = transposeB ? shapeB[1] : shapeB[0];
            Logger.AssertIsFalse(mulXDim != mulYDim, "MatMul2D.ValueError: failed, dims not equal");

            var shapeOut = new DynamicTensorShape(transposeA ? shapeA[1] : shapeA[0], transposeB ? shapeB[0] : shapeB[1]);
            return PartialTensor.Create(input0.dataType, shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.Gemm(A.shape, B.shape, transposeA, transposeB), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            if (A.shape.HasZeroDims() || B.shape.HasZeroDims())
                ctx.backend.MemSet(O, 0.0f);
            else
                ctx.backend.MatMul2D(A as Tensor<float>, B as Tensor<float>, O, transposeA, transposeB);
        }
    }

    /// <summary>
    /// Represents an element-wise `Min` math operation layer: f(a, b) = min(a, b).
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "a", "b" })]
    partial class Min : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            if (a is PartialTensor<int>)
                return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, PartialTensorElement<int>.Min);
            else
                return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<float>, PartialTensorElement<float>.Min);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], TensorShapeHelper.BroadcastShape(A, B), A.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (A is Tensor<int>)
                ctx.backend.Min(A as Tensor<int>, B as Tensor<int>, O as Tensor<int>);
            else
                ctx.backend.Min(A as Tensor<float>, B as Tensor<float>, O as Tensor<float>);
        }
    }

    /// <summary>
    /// Represents an element-wise `Max` math operation layer: f(a, b) = max(a, b).
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "a", "b" })]
    partial class Max : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            if (a is PartialTensor<int>)
                return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, PartialTensorElement<int>.Max);
            else
                return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<float>, PartialTensorElement<float>.Max);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], TensorShapeHelper.BroadcastShape(A, B), A.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (A is Tensor<int>)
                ctx.backend.Max(A as Tensor<int>, B as Tensor<int>, O as Tensor<int>);
            else
                ctx.backend.Max(A as Tensor<float>, B as Tensor<float>, O as Tensor<float>);
        }
    }

    /// <summary>
    /// Represents an element-wise `Max` math operation layer: f(a, b) = a % b.
    ///
    /// If fmod is false the sign of the remainder is the same as that of the divisor as in Python.
    ///
    /// If fmod is true the sign of the remainder is the same as that of the dividend as in C#.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "a", "b" })]
    partial class Mod : Layer
    {
        public bool fmod;

        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b, bool fmod)
        {
            if (fmod)
            {
                if (a is PartialTensor<int>)
                    return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, PartialTensorElement<int>.FMod);
                else
                    return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<float>, PartialTensorElement<float>.FMod);
            }
            if (a is PartialTensor<int>)
                return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, PartialTensorElement<int>.Mod);
            else
                return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<float>, PartialTensorElement<float>.Mod);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], TensorShapeHelper.BroadcastShape(A, B), A.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (!fmod)
            {
                if (A is Tensor<int>)
                    ctx.backend.Mod(A as Tensor<int>, B as Tensor<int>, O as Tensor<int>);
                else
                    ctx.backend.Mod(A as Tensor<float>, B as Tensor<float>, O as Tensor<float>);
            }
            else
            {
                if (A is Tensor<int>)
                    ctx.backend.FMod(A as Tensor<int>, B as Tensor<int>, O as Tensor<int>);
                else
                    ctx.backend.FMod(A as Tensor<float>, B as Tensor<float>, O as Tensor<float>);
            }
        }
    }

    /// <summary>
    /// Represents an element-wise `Mul` math operation layer: f(a, b) = a * b.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "a", "b" })]
    partial class Mul : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            if (a is PartialTensor<int>)
                return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, (x, y) => x * y);
            else
                return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<float>, (x, y) => x * y);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], TensorShapeHelper.BroadcastShape(A, B), A.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (A is Tensor<int>)
                ctx.backend.Mul(A as Tensor<int>, B as Tensor<int>, O as Tensor<int>);
            else
                ctx.backend.Mul(A as Tensor<float>, B as Tensor<float>, O as Tensor<float>);
        }
    }

    /// <summary>
    /// Represents an element-wise `Neg` math layer: f(x) = -x.
    /// </summary>
    [Operator(category = "Math")]
    partial class Neg : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            if (input is PartialTensor<int>)
                return PartialTensor.Unary(input as PartialTensor<int>, x => -x);
            return PartialTensor.Unary(input as PartialTensor<float>, x => -x);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (X is Tensor<int>)
                ctx.backend.Neg(X as Tensor<int>, O as Tensor<int>);
            else
                ctx.backend.Neg(X as Tensor<float>, O as Tensor<float>);
        }
    }

    /// <summary>
    /// Represents an element-wise `Pow` math operation layer: f(a, b) = pow(a, b).
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "a", "b" })]
    partial class Pow : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            if (a is PartialTensor<int>)
            {
                if (b is PartialTensor<int>)
                    return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, PartialTensorElement<int>.Pow);
                return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<float>, (_, _) => PartialTensorElement<float>.Unknown);
            }
            if (b is PartialTensor<int>)
                return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<int>, (_, _) => PartialTensorElement<float>.Unknown);
            return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<float>, PartialTensorElement<float>.Pow);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], TensorShapeHelper.BroadcastShape(A, B), A.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;

            if (A is Tensor<int>)
            {
                if (B is Tensor<int>)
                    ctx.backend.Pow(A as Tensor<int>, B as Tensor<int>, O as Tensor<int>);
                else
                    ctx.backend.Pow(A as Tensor<int>, B as Tensor<float>, O as Tensor<int>);
            }
            else
            {
                if (B is Tensor<int>)
                    ctx.backend.Pow(A as Tensor<float>, B as Tensor<int>, O as Tensor<float>);
                else
                    ctx.backend.Pow(A as Tensor<float>, B as Tensor<float>, O as Tensor<float>);
            }
        }
    }

    /// <summary>
    /// Represents an element-wise `Reciprocal` math layer: f(x) = 1 / x.
    /// </summary>
    [Operator(category = "Math")]
    partial class Reciprocal : Layer
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
            ctx.backend.Reciprocal(X as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Round` math layer: f(x) = round(x).
    /// </summary>
    [Operator(category = "Math")]
    partial class Round : Layer
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
            ctx.backend.Round(X as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Mad` math operation: multiplies and adds bias to a tensor: f(T, s, b) = s * T + b.
    /// </summary>
    [Operator(category = "Math")]
    partial class ScalarMad : Layer
    {
        public DataType dataType;
        public float sFloat;
        public float bFloat;
        public int sInt;
        public int bInt;

        internal static PartialTensor InferPartial(PartialTensor input, DataType dataType, float sFloat, float bFloat, int sInt, int bInt)
        {
            return PartialTensor.Create(input.dataType, input.shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (dataType == DataType.Float)
                ctx.backend.ScalarMad(X as Tensor<float>, O as Tensor<float>, sFloat, bFloat);
            else
                ctx.backend.ScalarMad(X as Tensor<int>, O as Tensor<int>, sInt, bInt);
        }
    }

    /// <summary>
    /// Represents an element-wise `Shrink` math layer: f(x) = x + bias if x &lt; lambd. f(x) = x - bias if x &gt; lambd. Otherwise f(x) = 0.
    /// </summary>
    [Operator(category = "Math")]
    partial class Shrink : Layer
    {
        public float bias;
        public float lambd;

        internal static PartialTensor InferPartial(PartialTensor input, float bias, float lambd)
        {
            return PartialTensor.Create(input.dataType, input.shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Shrink(X as Tensor<float>, O, bias, lambd);
        }
    }

    /// <summary>
    /// Represents an element-wise `Sign` math layer: f(x) = 1 if x > 0. f(x) = -1 if x &lt; 0. Otherwise f(x) = 0.
    /// </summary>
    [Operator(category = "Math")]
    partial class Sign : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            if (input is PartialTensor<int>)
                return PartialTensor.Unary(input as PartialTensor<int>, PartialTensorElement<int>.Sign);
            return PartialTensor.Unary(input as PartialTensor<float>, PartialTensorElement<float>.Sign);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (X is Tensor<int>)
                ctx.backend.Sign(X as Tensor<int>, O as Tensor<int>);
            else
                ctx.backend.Sign(X as Tensor<float>, O as Tensor<float>);
        }
    }

    /// <summary>
    /// Represents an element-wise `Rsqrt` math layer: f(x) = 1 / sqrt(x).
    /// </summary>
    [Operator(category = "Math")]
    partial class Rsqrt : Layer
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
            ctx.backend.Rsqrt(X as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Sqrt` math layer: f(x) = sqrt(x).
    /// </summary>
    [Operator(category = "Math")]
    partial class Sqrt : Layer
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
            ctx.backend.Sqrt(X as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Square` math layer: f(x) = x * x.
    /// </summary>
    [Operator(category = "Math")]
    partial class Square : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            if (input is PartialTensor<int>)
                return PartialTensor.Unary(input as PartialTensor<int>, x => x * x);
            return PartialTensor.Unary(input as PartialTensor<float>, x => x * x);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (X is Tensor<int>)
                ctx.backend.Square(X as Tensor<int>, O as Tensor<int>);
            else
                ctx.backend.Square(X as Tensor<float>, O as Tensor<float>);
        }
    }

    /// <summary>
    /// Represents an element-wise `Sub` math operation layer: f(a, b) = a - b.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "a", "b" })]
    partial class Sub : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            if (a is PartialTensor<int>)
                return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, (x, y) => x - y);
            else
                return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<float>, (x, y) => x - y);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], TensorShapeHelper.BroadcastShape(A, B), A.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (A is Tensor<int>)
                ctx.backend.Sub(A as Tensor<int>, B as Tensor<int>, O as Tensor<int>);
            else
                ctx.backend.Sub(A as Tensor<float>, B as Tensor<float>, O as Tensor<float>);
        }
    }

    /// <summary>
    /// Represents an element-wise `Trunc` math layer: f(x) = trunc(x).
    /// </summary>
    [Operator(category = "Math")]
    partial class Trunc : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return PartialTensor.Activation(input);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (X is Tensor<int>)
                ctx.backend.MemCopy(X as Tensor<int>, O as Tensor<int>);
            else
                ctx.backend.Trunc(X as Tensor<float>, O as Tensor<float>);
        }
    }

    /// <summary>
    /// Represents an element-wise `TruncDiv` math operation layer: f(a, b) = trunc(a / b).
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Math")]
    [Inputs(names = new[] { "a", "b" })]
    partial class TruncDiv : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            if (a is PartialTensor<int>)
                return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, (x, y) =>
                {
                    if (y == PartialTensorElement<int>.Zero)
                        throw new DivideByZeroException();
                    if (x == PartialTensorElement<int>.Zero)
                        return x;
                    if (y == PartialTensorElement<int>.One)
                        return x;
                    if (x == y)
                        return PartialTensorElement<int>.One;
                    if (x.isValue && y.isValue)
                        return PartialTensorElement<int>.Value(x.value / y.value);
                    return PartialTensorElement<int>.Unknown;
                });
            else
                return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<float>, (x, y) =>
                {
                    if (y == PartialTensorElement<float>.Zero)
                        throw new DivideByZeroException();
                    if (x == PartialTensorElement<float>.Zero)
                        return x;
                    if (y == PartialTensorElement<float>.One)
                        return x;
                    if (x == y)
                        return PartialTensorElement<float>.One;
                    if (x.isValue && y.isValue)
                        return PartialTensorElement<float>.Value(math.trunc(x.value / y.value));
                    return PartialTensorElement<float>.Unknown;
                });
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], TensorShapeHelper.BroadcastShape(A, B), A.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (A is Tensor<int>)
                ctx.backend.TruncDiv(A as Tensor<int>, B as Tensor<int>, O as Tensor<int>);
            else
                ctx.backend.TruncDiv(A as Tensor<float>, B as Tensor<float>, O as Tensor<float>);
        }
    }
}
