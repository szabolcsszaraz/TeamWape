using System;
using UnityEngine;

namespace Unity.InferenceEngine.Layers
{
    /// <summary>
    /// Takes two 1D int tensors representing shapes and returns an int tensor representing the shape when you broadcast these shapes together with numpy-style broadcasting.
    ///
    /// e.g. BroadcastArgs([1, 1, 2, 3], [5, 2, 1]) returns [1, 5, 2, 3]
    /// </summary>
    [Operator(category = "Dimension")]
    [Inputs(names = new[] { "a", "b" }, inputCPURead = new[] { 0, 1 })]
    partial class BroadcastArgs : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor a, PartialTensor b)
        {
            var shapeOut = DynamicTensorShape.DynamicOfRank(1);
            if (a.shape.IsStatic() && b.shape.IsStatic())
                shapeOut[0] = DynamicTensorDim.Int(Mathf.Max(a.shape[0].ToInt(), b.shape[0].ToInt()));
            var tensorOut = new PartialTensor<int>(shapeOut);
            if (tensorOut.isPartiallyKnown)
            {
                for (var i = 0; i < tensorOut.length; i++)
                    tensorOut[i] = PartialTensorElement<int>.Value(1);
                for (var i = 0; i < a.length; i++)
                    tensorOut[tensorOut.length - 1 - i] = (PartialTensorElement<int>)DynamicTensorDim.Broadcast((DynamicTensorDim)(a as PartialTensor<int>)[a.length - 1 - i], (DynamicTensorDim)tensorOut[tensorOut.length - 1 - i]);
                for (var i = 0; i < b.length; i++)
                    tensorOut[tensorOut.length - 1 - i] = (PartialTensorElement<int>)DynamicTensorDim.Broadcast((DynamicTensorDim)(b as PartialTensor<int>)[b.length - 1 - i], (DynamicTensorDim)tensorOut[tensorOut.length - 1 - i]);
            }

            return tensorOut;
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var a = ctx.storage.GetInts(inputs[0]);
            var b = ctx.storage.GetInts(inputs[1]);
            var shape = new TensorShape(a).Broadcast(new TensorShape(b));
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], new TensorShape(shape.rank), DataType.Int, BackendType.CPU) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;

            ctx.cpuBackend.SetShape(O, shape);
        }
    }

    /// <summary>
    /// Represents a `Shape` layer. This computes the shape of an input tensor as a 1D `Tensor<int>`.
    /// </summary>
    [Operator(category = "Dimension")]
    [Inputs(names = new[] { "input" }, inputNoDataDependency = new[] { 0 })]
    partial class Shape : Layer
    {
        public int start;
        public int end;

        internal static PartialTensor InferPartial(PartialTensor input, int start, int end)
        {
            if (start == end)
                return new PartialTensor<int>(new DynamicTensorShape(DynamicTensorDim.Zero));

            var shapeInput = input.shape;

            if (!shapeInput.hasRank)
                return new PartialTensor<int>(DynamicTensorShape.DynamicOfRank(1));

            var startX = start < 0 ? start + shapeInput.rank : start;
            var endX = end < 0 ? end + shapeInput.rank : end;
            startX = Mathf.Clamp(startX, 0, shapeInput.rank);
            endX = Mathf.Clamp(endX, 0, shapeInput.rank);

            Logger.AssertIsTrue(endX >= startX, "PartialTensorFromSymbolicShape.InputError: start value cannot be greater than end value for shape slicing");

            var tensorOut = new PartialTensor<int>(new DynamicTensorShape(endX - startX));
            for (var i = startX; i < endX; i++)
            {
                tensorOut[i - startX] = (PartialTensorElement<int>)shapeInput[i];
            }

            return tensorOut;
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var shapeX = ctx.storage.GetTensorShape(inputs[0]);
            var startX = start < 0 ? start + shapeX.rank : start;
            var endX = end < 0 ? end + shapeX.rank : end;
            startX = Mathf.Clamp(startX, 0, shapeX.rank);
            endX = Mathf.Clamp(endX, 0, shapeX.rank);

            Logger.AssertIsTrue(endX >= startX, "Shape.InputError: start value cannot be greater than end value for shape slicing");
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], new TensorShape(endX - startX), DataType.Int, BackendType.CPU) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;

            var shape = TensorShape.Ones(endX - startX);
            for (var i = startX; i < endX; i++)
                shape[i - startX] = shapeX[i];
            ctx.cpuBackend.SetShape(O, shape);
        }
    }

    /// <summary>
    /// Represents a `Size` layer. This computes the number of elements of an input tensor as a scalar `Tensor&lt;int&gt;`.
    /// </summary>
    [Operator(category = "Dimension")]
    [Inputs(names = new[] { "input" }, inputNoDataDependency = new[] { 0 })]
    partial class Size : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return new PartialTensor<int>(new DynamicTensorShape())
            {
                [0] = (PartialTensorElement<int>)input.shape.Length()
            };
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var shapeX = ctx.storage.GetTensorShape(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], new TensorShape(), DataType.Int, BackendType.CPU) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;

            ctx.cpuBackend.MemSet(O, shapeX.length);
        }
    }
}
