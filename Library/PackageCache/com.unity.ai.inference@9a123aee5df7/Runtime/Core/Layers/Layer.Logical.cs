using System;

namespace Unity.InferenceEngine.Layers
{
    /// <summary>
    /// Represents an element-wise `And` logical operation layer: f(a, b) = a &amp; b.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Logical")]
    [Inputs(names = new[] { "a", "b" })]
    partial class And : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, (x, y) =>
            {
                if (x == y)
                    return x;
                if (x == PartialTensorElement<int>.One)
                    return y;
                if (y == PartialTensorElement<int>.One)
                    return x;
                if (x == PartialTensorElement<int>.Zero || y == PartialTensorElement<int>.Zero)
                    return PartialTensorElement<int>.Zero;
                return PartialTensorElement<int>.Unknown;
            });
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]) as Tensor<int>;
            var B = ctx.storage.GetTensor(inputs[1]) as Tensor<int>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], A.shape.Broadcast(B.shape), DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.And(A, B, O);
        }
    }

    /// <summary>
    /// Represents a `Compress` logical layer that selects slices of an input tensor along a given axis according to a condition tensor.
    /// If you don't provide an axis, the layer flattens the input tensor.
    /// </summary>
    [Operator(category = "Logical")]
    [Inputs(names = new[] { "input", "condition" })]
    partial class Compress : Layer
    {
        public bool hasAxis;
        public int axis;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor condition, bool hasAxis, int axis)
        {
            var dataType = input.dataType;
            var shapeInput = input.shape;
            var isZero = shapeInput.Length() * condition.shape.Length() == 0;
            if (!hasAxis)
                return PartialTensor.Create(dataType, new DynamicTensorShape(isZero ? DynamicTensorDim.Zero : DynamicTensorDim.Unknown));

            var shapeOut = shapeInput;
            shapeOut[axis] = isZero ? DynamicTensorDim.Zero : DynamicTensorDim.Unknown;
            return PartialTensor.Create(dataType, shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var isTempX = !hasAxis;
            if (isTempX)
            {
                var flattenedShape = new TensorShape(X.shape.length);
                var tempX = ctx.storage.AllocateTensor(flattenedShape, X.dataType, ctx.backend.backendType);
                ctx.backend.Reshape(X, tempX);
                X = tempX;
            }

            var condition = ctx.storage.GetTensor(inputs[1]) as Tensor<int>;
            var numCondition = condition.shape.length;

            var indices = ctx.storage.AllocateTensor(condition.shape, DataType.Int, BackendType.CPU) as Tensor<int>;
            CPUTensorData.Pin(indices);

            var numIndices = 0;
            for (var i = 0; i < numCondition; i++)
            {
                if (condition[i] == 0)
                    continue;
                indices[numIndices] = i;
                numIndices++;
            }

            var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.Compress(X.shape, numIndices, axis), X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
            {
                if (isTempX)
                    ctx.storage.Dispose(X);
                ctx.storage.Dispose(indices);
                return;
            }
            ctx.backend.CompressWithIndices(X, indices, O, numIndices, axis);
            if (isTempX)
                ctx.storage.Dispose(X);
            ctx.storage.Dispose(indices);
        }
    }

    /// <summary>
    /// Represents an element-wise `Equal` logical operation layer: f(a, b) = 1 if a == b, otherwise f(x) = 0.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Logical")]
    [Inputs(names = new[] { "a", "b" })]
    partial class Equal : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            if (a is PartialTensor<int>)
                return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, PartialTensorElement<int>.Eq);
            else
                return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<float>, PartialTensorElement<float>.Eq);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], A.shape.Broadcast(B.shape), DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            if (A is Tensor<int>)
                ctx.backend.Equal(A as Tensor<int>, B as Tensor<int>, O);
            else
                ctx.backend.Equal(A as Tensor<float>, B as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Greater` logical operation layer: f(a, b) = 1 if a > b, otherwise f(x) = 0.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Logical")]
    [Inputs(names = new[] { "a", "b" })]
    partial class Greater : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            if (a is PartialTensor<int>)
                return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, PartialTensorElement<int>.Gt);
            else
                return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<float>, PartialTensorElement<float>.Gt);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], A.shape.Broadcast(B.shape), DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            if (A is Tensor<int>)
                ctx.backend.Greater(A as Tensor<int>, B as Tensor<int>, O);
            else
                ctx.backend.Greater(A as Tensor<float>, B as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `GreaterOrEqual` logical operation layer: f(a, b) = 1 if a >= b, otherwise f(a,b) = 0.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Logical")]
    [Inputs(names = new[] { "a", "b" })]
    partial class GreaterOrEqual : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            if (a is PartialTensor<int>)
                return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, PartialTensorElement<int>.Ge);
            else
                return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<float>, PartialTensorElement<float>.Ge);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], A.shape.Broadcast(B.shape), DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            if (A is Tensor<int>)
                ctx.backend.GreaterOrEqual(A as Tensor<int>, B as Tensor<int>, O);
            else
                ctx.backend.GreaterOrEqual(A as Tensor<float>, B as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `IsInf` logical layer: f(x) = 1 elementwise if x is +Inf and detectPositive, or x is -Inf and `detectNegative` is true. Otherwise f(x) = 0.
    /// </summary>
    [Operator(category = "Logical")]
    partial class IsInf : Layer
    {
        public bool detectNegative;
        public bool detectPositive;

        internal static PartialTensor InferPartial(PartialTensor input, bool detectNegative, bool detectPositive)
        {
            return new PartialTensor<int>(input.shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], A.shape, DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.IsInf(A, O, detectNegative, detectPositive);
        }
    }

    /// <summary>
    /// Represents an element-wise `IsNaN` logical layer: f(x) = 1 if x is NaN, otherwise f(x) = 0.
    /// </summary>
    [Operator(category = "Logical")]
    partial class IsNaN : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return new PartialTensor<int>(input.shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], A.shape, DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.IsNaN(A, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Less` logical operation layer: f(a, b) = 1 if a &lt; b, otherwise f(x) = 0.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Logical")]
    [Inputs(names = new[] { "a", "b" })]
    partial class Less : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            if (a is PartialTensor<int>)
                return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, PartialTensorElement<int>.Lt);
            else
                return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<float>, PartialTensorElement<float>.Lt);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], A.shape.Broadcast(B.shape), DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            if (A is Tensor<int>)
                ctx.backend.Less(A as Tensor<int>, B as Tensor<int>, O);
            else
                ctx.backend.Less(A as Tensor<float>, B as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `LessOrEqual` logical operation layer: f(a, b) = 1 if a &lt;= b, otherwise f(a,b) = 0.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Logical")]
    [Inputs(names = new[] { "a", "b" })]
    partial class LessOrEqual : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            if (a is PartialTensor<int>)
                return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, PartialTensorElement<int>.Le);
            else
                return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<float>, PartialTensorElement<float>.Le);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], A.shape.Broadcast(B.shape), DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            if (A is Tensor<int>)
                ctx.backend.LessOrEqual(A as Tensor<int>, B as Tensor<int>, O);
            else
                ctx.backend.LessOrEqual(A as Tensor<float>, B as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Not` logical layer: f(x) = ~x.
    /// </summary>
    [Operator(category = "Logical")]
    partial class Not : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return PartialTensor.Unary(input as PartialTensor<int>, x =>
            {
                if (x == PartialTensorElement<int>.One)
                    return PartialTensorElement<int>.Zero;
                if (x == PartialTensorElement<int>.Zero)
                    return PartialTensorElement<int>.One;
                return PartialTensorElement<int>.Unknown;
            });
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]) as Tensor<int>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], A.shape, DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Not(A, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `NotEqual` logical operation layer: f(a, b) = 1 if a != b, otherwise f(x) = 0.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Logical")]
    [Inputs(names = new[] { "a", "b" })]
    partial class NotEqual : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            if (a is PartialTensor<int>)
                return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, PartialTensorElement<int>.Ne);
            else
                return PartialTensor.Broadcast(a as PartialTensor<float>, b as PartialTensor<float>, PartialTensorElement<float>.Ne);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]);
            var B = ctx.storage.GetTensor(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], A.shape.Broadcast(B.shape), DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            if (A is Tensor<int>)
                ctx.backend.NotEqual(A as Tensor<int>, B as Tensor<int>, O);
            else
                ctx.backend.NotEqual(A as Tensor<float>, B as Tensor<float>, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Or` logical operation layer: f(a, b) = a | b.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Logical")]
    [Inputs(names = new[] { "a", "b" })]
    partial class Or : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, (x, y) =>
            {
                if (x == PartialTensorElement<int>.Zero)
                    return y;
                if (y == PartialTensorElement<int>.Zero)
                    return x;
                if (x == y)
                    return x;
                if (x == PartialTensorElement<int>.One || y == PartialTensorElement<int>.One)
                    return PartialTensorElement<int>.One;
                return PartialTensorElement<int>.Unknown;
            });
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]) as Tensor<int>;
            var B = ctx.storage.GetTensor(inputs[1]) as Tensor<int>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], A.shape.Broadcast(B.shape), DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Or(A, B, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Xor` logical operation layer: f(a, b) = a ^ b.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Logical")]
    [Inputs(names = new[] { "a", "b" })]
    partial class Xor : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            return PartialTensor.Broadcast(a as PartialTensor<int>, b as PartialTensor<int>, (x, y) =>
            {
                if (x == y)
                    return PartialTensorElement<int>.Zero;
                if (x == PartialTensorElement<int>.One && y == PartialTensorElement<int>.Zero)
                    return PartialTensorElement<int>.One;
                if (x == PartialTensorElement<int>.Zero && y == PartialTensorElement<int>.One)
                    return PartialTensorElement<int>.One;
                return PartialTensorElement<int>.Unknown;
            });
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var A = ctx.storage.GetTensor(inputs[0]) as Tensor<int>;
            var B = ctx.storage.GetTensor(inputs[1]) as Tensor<int>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], A.shape.Broadcast(B.shape), DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Xor(A, B, O);
        }
    }

    /// <summary>
    /// Represents an element-wise `Where` logical operation layer: f(condition, a, b) = a if `condition`, otherwise f(condition, a, b) = b.
    ///
    /// This supports numpy-style broadcasting of input tensors.
    /// </summary>
    [Operator(category = "Logical")]
    [Inputs(names = new[] { "condition", "input1", "input2" })]
    partial class Where : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor condition, PartialTensor input1, PartialTensor input2)
        {
            var shapeOut = condition.shape.Broadcast(input1.shape.Broadcast(input2.shape));
            var tensorOut = PartialTensor.Create(input1.dataType, shapeOut);

            if (shapeOut.IsStatic() && shapeOut.rank <= 1 && condition.isPartiallyKnown && input1.isPartiallyKnown && input2.isPartiallyKnown)
            {
                for (var i = 0; i < tensorOut.length; i++)
                {
                    var c = condition.Get<int>(i % condition.length);
                    if (c == PartialTensorElement<int>.One)
                        tensorOut.CopyElement(i, input1, i % input1.length);
                    else if (c == PartialTensorElement<int>.Zero)
                        tensorOut.CopyElement(i, input2, i % input2.length);
                }
            }

            return tensorOut;
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var C = ctx.storage.GetTensor(inputs[0]) as Tensor<int>;
            var A = ctx.storage.GetTensor(inputs[1]);
            var B = ctx.storage.GetTensor(inputs[2]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], A.shape.Broadcast(B.shape.Broadcast(C.shape)), A.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Where(C, A, B, O);
        }
    }
}
