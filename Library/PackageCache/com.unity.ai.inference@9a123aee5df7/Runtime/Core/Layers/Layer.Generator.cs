using System;

namespace Unity.InferenceEngine.Layers
{
    /// <summary>
    /// Represents a `ConstantOfShape` layer. This generates a tensor with the shape given by the `input` tensor and filled with a given value.
    /// </summary>
    [Operator(category = "Generator")]
    [Inputs(names = new[] { "input" }, inputCPURead = new[] { 0 })]
    partial class ConstantOfShape : Layer
    {
        public DataType dataType;
        public float floatValue;
        public int intValue;

        internal static PartialTensor InferPartial(PartialTensor input, DataType dataType, float floatValue, int intValue)
        {
            var shape = DynamicTensorShape.FromPartialTensor(input as PartialTensor<int>);
            var tensorOut = PartialTensor.Create(dataType, shape);
            if (!tensorOut.isPartiallyKnown)
                return tensorOut;

            if (tensorOut is PartialTensor<int> tensorInt)
            {
                for (var i = 0; i < tensorInt.length; i++)
                    tensorInt[i] = PartialTensorElement<int>.Value(intValue);
            }
            else if (tensorOut is PartialTensor<float> tensorFloat)
            {
                for (var i = 0; i < tensorFloat.length; i++)
                    tensorFloat[i] = PartialTensorElement<float>.Value(floatValue);
            }

            return tensorOut;
        }

        internal override void Execute(ExecutionContext ctx)
        {
            TensorShape shape = new TensorShape(ctx.storage.GetInts(inputs[0]));
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shape, dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (dataType == DataType.Int)
                ctx.backend.MemSet(O as Tensor<int>, intValue);
            else
                ctx.backend.MemSet(O as Tensor<float>, floatValue);
            return;
        }
    }

    /// <summary>
    /// Represents a `OneHot` layer. This generates a one-hot tensor with a given `depth`, `indices` and `values`.
    /// </summary>
    [Operator(category = "Generator")]
    [Inputs(names = new[] { "indices", "depth", "values" }, inputCPURead = new[] { 1, 2 })]
    partial class OneHot : Layer
    {
        public int axis;
        public bool allowNegativeIndexes;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor depth, PartialTensor values, int axis, bool allowNegativeIndexes)
        {
            var shapeInput = input.shape;
            var dataType = values.dataType;
            if (!shapeInput.hasRank)
                return PartialTensor.Create(dataType);

            var shapeOut = shapeInput.Unsqueeze(axis);
            shapeOut[axis] = (DynamicTensorDim)(depth as PartialTensor<int>)[0];

            return PartialTensor.Create(dataType, shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var indices = ctx.storage.GetTensor(inputs[0]) as Tensor<int>;
            var depth = ctx.storage.GetInt(inputs[1]);
            var dataType = ctx.storage.GetDataType(inputs[2]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.OneHot(indices.shape, axis, depth), dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (dataType == DataType.Int)
            {
                var valuesi = ctx.storage.GetInts(inputs[2]);
                ctx.backend.OneHot(indices, O as Tensor<int>, axis, depth, valuesi[0], valuesi[1], allowNegativeIndexes);
            }
            else
            {
                var valuesf = ctx.storage.GetFloats(inputs[2]);
                ctx.backend.OneHot(indices, O as Tensor<float>, axis, depth, valuesf[0], valuesf[1], allowNegativeIndexes);
            }
        }
    }

    /// <summary>
    /// Represents a `Range` layer. This generates a 1D output tensor where the values form an arithmetic progression defined by the `start`, `limit` and `delta` scalar input tensors.
    /// </summary>
    [Operator(category = "Generator")]
    [Inputs(names = new[] { "start", "limit", "delta" }, inputCPURead = new[] { 0, 1, 2 })]
    partial class Range : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor start, PartialTensor limit, PartialTensor delta)
        {
            start.shape.DeclareRank(0);
            limit.shape.DeclareRank(0);
            delta.shape.DeclareRank(0);

            var shape = DynamicTensorShape.DynamicOfRank(1);

            if (start.dataType == DataType.Int && start.Get<int>() == PartialTensorElement<int>.Zero && delta.Get<int>() == PartialTensorElement<int>.One)
                shape[0] = (DynamicTensorDim)limit.Get<int>();

            if (start.dataType == DataType.Int && start.IsStatic() && limit.IsStatic() && delta.IsStatic())
                shape[0] = DynamicTensorDim.Int(ShapeInference.Range(start.Get<int>().value, limit.Get<int>().value, delta.Get<int>().value)[0]);

            if (start.dataType == DataType.Float && start.IsStatic() && limit.IsStatic() && delta.IsStatic())
                shape[0] = DynamicTensorDim.Int(ShapeInference.Range(start.Get<float>().value, limit.Get<float>().value, delta.Get<float>().value)[0]);

            return PartialTensor.Create(start.dataType, shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            if (ctx.storage.GetDataType(inputs[0]) == DataType.Int)
            {
                int starti = ctx.storage.GetInt(inputs[0]);
                int limiti = ctx.storage.GetInt(inputs[1]);
                int deltai = ctx.storage.GetInt(inputs[2]);
                var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.Range(starti, limiti, deltai), DataType.Int, ctx.backend.backendType) as Tensor<int>;
                if (O.shape.HasZeroDims())
                    return;
                ctx.backend.Range(O, starti, deltai);
            }
            else
            {
                float startf = ctx.storage.GetFloat(inputs[0]);
                float limitf = ctx.storage.GetFloat(inputs[1]);
                float deltaf = ctx.storage.GetFloat(inputs[2]);
                var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.Range(startf, limitf, deltaf), DataType.Float, ctx.backend.backendType) as Tensor<float>;
                if (O.shape.HasZeroDims())
                    return;
                ctx.backend.Range(O, startf, deltaf);
            }
        }
    }
}
