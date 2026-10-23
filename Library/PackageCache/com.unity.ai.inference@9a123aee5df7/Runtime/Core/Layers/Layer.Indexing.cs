using System;
using Unity.Collections;
using UnityEngine;

namespace Unity.InferenceEngine.Layers
{
    /// <summary>
    /// Options for the reduction operation to use in a scatter layer.
    /// </summary>
    enum ScatterReductionMode
    {
        /// <summary>
        /// Use no reduction.
        /// </summary>
        None,
        /// <summary>
        /// Use the addition operator when reducing.
        /// </summary>
        Add,
        /// <summary>
        /// Use the multiplication operator when reducing.
        /// </summary>
        Mul,
        /// <summary>
        /// Use the maximum operator when reducing.
        /// </summary>
        Max,
        /// <summary>
        /// Use the minimum operator when reducing.
        /// </summary>
        Min,
    }

    /// <summary>
    /// Represents an `ArgMax` layer. This computes the indices of the maximum elements of the input tensor along a given axis.
    /// </summary>
    [Operator(category = "Indexing")]
    partial class ArgMax : Layer
    {
        public int axis;
        public bool keepdims;
        public bool selectLastIndex;

        internal static PartialTensor InferPartial(PartialTensor input, int axis, bool keepdims, bool selectLastIndex)
        {
            return PartialTensor.ArgReduce(input, axis, keepdims);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var shapeO = X.shape.Reduce(axis, keepdims);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shapeO, DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            if (X is Tensor<int>)
                ctx.backend.ArgMax(X as Tensor<int>, O, axis, selectLastIndex);
            else
                ctx.backend.ArgMax(X as Tensor<float>, O, axis, selectLastIndex);
        }
    }

    /// <summary>
    /// Represents an `ArgMin` layer. This computes the indices of the minimum elements of the input tensor along a given axis.
    /// </summary>
    [Operator(category = "Indexing")]
    partial class ArgMin : Layer
    {
        public int axis;
        public bool keepdims;
        public bool selectLastIndex;

        internal static PartialTensor InferPartial(PartialTensor input, int axis, bool keepdims, bool selectLastIndex)
        {
            return PartialTensor.ArgReduce(input, axis, keepdims);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var shapeO = X.shape.Reduce(axis, keepdims);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shapeO, DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (O.shape.HasZeroDims())
                return;
            if (X is Tensor<int>)
                ctx.backend.ArgMin(X as Tensor<int>, O, axis, selectLastIndex);
            else
                ctx.backend.ArgMin(X as Tensor<float>, O, axis, selectLastIndex);
        }
    }

    /// <summary>
    /// Represents a `Gather` layer. This takes values from the input tensor indexed by the indices tensor along a given axis and concatenates them.
    /// </summary>
    [Operator(category = "Indexing")]
    [Inputs(names = new[] { "input", "indices" })]
    partial class Gather : Layer
    {
        public int axis;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor indices, int axis)
        {
            var dataType = input.dataType;
            if (dataType == DataType.Int && axis == 0 && input.isPartiallyKnown && indices.isPartiallyKnown && input.shape.rank == 1 && indices.shape.rank <= 1)
            {
                var tensorOut = new PartialTensor<int>(indices.shape);
                var inputInt = input as PartialTensor<int>;
                for (var i = 0; i < indices.length; i++)
                {
                    var index = indices.Get<int>(i);
                    if (index.isValue && index.value < 0)
                        index = PartialTensorElement<int>.Value(index.value + input.length);
                    tensorOut[i] = inputInt[index];
                }

                return tensorOut;
            }

            var shapeInput = input.shape;
            var shapeIndices = indices.shape;
            if (!shapeInput.hasRank)
                return PartialTensor.Create(dataType);

            Logger.AssertIsTrue(!shapeInput.hasRank || shapeInput.rank >= 1, "RankError: incorrect rank, expecting at least {0}, got {1}", 1, shapeInput.rank);

            if (!shapeIndices.hasRank)
                return PartialTensor.Create(dataType);

            var axisX = shapeInput.Axis(axis);

            var shapeOut = DynamicTensorShape.DynamicOfRank(shapeInput.rank - 1 + shapeIndices.rank);

            for (var i = 0; i < shapeOut.rank; i++)
            {
                if (i < axisX)
                    shapeOut[i] = shapeInput[i];
                else if (i < axisX + shapeIndices.rank)
                    shapeOut[i] = shapeIndices[i - axisX];
                else
                    shapeOut[i] = shapeInput[i - shapeOut.rank];
            }

            return PartialTensor.Create(dataType, shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var indices = ctx.storage.GetTensor(inputs[1]) as Tensor<int>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.Gather(X.shape, indices.shape, axis), X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Gather(X, indices, O, axis);
        }
    }

    /// <summary>
    /// Represents a `GatherElements` layer. This takes values from the input tensor indexed by the `indices` tensor along a given axis.
    /// </summary>
    [Operator(category = "Indexing")]
    [Inputs(names = new[] { "input", "indices" })]
    partial class GatherElements : Layer
    {
        public int axis;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor indices, int axis)
        {
            var shapeInput = input.shape;
            var shapeIndices = indices.shape;
            if (shapeInput.hasRank)
                Logger.AssertIsTrue(shapeInput.rank >= 1, "RankError: incorrect rank, expecting at least {0}, got {1}", 1, shapeInput.rank);

            if (shapeInput.hasRank)
            {
                shapeInput.Axis(axis);
                shapeIndices.DeclareRank(shapeInput.rank);
            }

            return PartialTensor.Create(input.dataType, shapeIndices);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var indices = ctx.storage.GetTensor(inputs[1]) as Tensor<int>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], indices.shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.GatherElements(X, indices, O, axis);
        }
    }

    /// <summary>
    /// Represents a `GatherND` layer. This takes slices of values from the batched input tensor indexed by the `indices` tensor.
    /// </summary>
    [Operator(category = "Indexing")]
    [Inputs(names = new[] { "input", "indices" })]
    partial class GatherND : Layer
    {
        public int batchDims;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor indices, int batchDims)
        {
            var dataType = input.dataType;
            var shapeInput = input.shape;
            var shapeIndices = indices.shape;
            // from https://github.com/onnx/onnx/blob/main/docs/Operators.md#GatherND
            if (shapeInput.hasRank)
                Logger.AssertIsTrue(shapeInput.rank >= batchDims, "RankError: incorrect rank, expecting at least {0}, got {1}", batchDims, shapeInput.rank);
            if (shapeIndices.hasRank)
                Logger.AssertIsTrue(shapeIndices.rank >= batchDims, "RankError: incorrect rank, expecting at least {0}, got {1}", batchDims, shapeIndices.rank);

            if (!shapeInput.hasRank || !shapeIndices.hasRank || !shapeIndices[-1].isValue)
                return PartialTensor.Create(dataType);

            Logger.AssertIsTrue(batchDims + shapeIndices[-1].value <= shapeInput.rank, "GatherND.InputError: last indices dim too large");
            var shapeOut = DynamicTensorShape.DynamicOfRank(shapeInput.rank + shapeIndices.rank - shapeIndices[-1].value - 1 - batchDims);
            for (var i = 0; i < shapeOut.rank; i++)
            {
                if (i < batchDims)
                    shapeOut[i] = DynamicTensorDim.MaxDefinedDim(shapeInput[i], shapeIndices[i]);
                else if (i < shapeIndices.rank - 1)
                    shapeOut[i] = shapeIndices[i];
                else
                    shapeOut[i] = shapeInput[i - shapeOut.rank];
            }

            return PartialTensor.Create(dataType, shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var indices = ctx.storage.GetTensor(inputs[1]) as Tensor<int>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.GatherND(X.shape, indices.shape, batchDims), X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.GatherND(X, indices, O, batchDims);
        }
    }

    /// <summary>
    /// Represents a `NonZero` layer. This returns the indices of the elements of the input tensor that are not zero.
    /// </summary>
    [Operator(category = "Indexing")]
    partial class NonZero : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            var shapeInput = input.shape;
            var shape = !shapeInput.hasRank ? DynamicTensorShape.DynamicOfRank(2) : new DynamicTensorShape(DynamicTensorDim.Int(shapeInput.rank), DynamicTensorDim.Unknown);
            return new PartialTensor<int>(shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            // need to download, if gpucompute need to execute commandbuffer and flush.
            if (ctx.backend is GPUComputeBackend gpuBackend)
                gpuBackend.ExecuteCommandBufferAndClear();

            // pixel we don't know which dim to pin
            var outputBackendType = ctx.backend.backendType;
            if (outputBackendType == BackendType.GPUPixel)
                outputBackendType = BackendType.CPU;

            if (X is Tensor<int>)
            {
                // reduce(notequal(X, 0)) to get nbNonZeroIndices
                var arrayX = (X as Tensor<int>).DownloadToNativeArray();
                int nbNonZeroIndices = 0;
                for (int i = 0; i < X.shape.length; ++i)
                {
                    if (arrayX[i] != 0)
                        nbNonZeroIndices += 1;
                }

                // compact with condition mask?
                var O = ctx.storage.AllocateTensorAndStore(outputs[0], new TensorShape(X.shape.rank, nbNonZeroIndices), DataType.Int, outputBackendType) as Tensor<int>;
                if (O.shape.HasZeroDims())
                    return;
                var arrayO = new NativeArray<int>(O.shape.length, Allocator.Temp);

                int nonZeroIndicesIdx = 0;
                for (var it = new TensorNDIterator(X.shape); it.HasNext(); it.MoveNext())
                {
                    if (arrayX[it.index] != 0)
                    {
                        for (int i = 0; i < X.shape.rank; i++)
                            arrayO[i * nbNonZeroIndices + nonZeroIndicesIdx] = it[i];
                        nonZeroIndicesIdx++;
                    }
                }
                O.dataOnBackend.Upload(arrayO, arrayO.Length);
            }
            else
            {
                // reduce(notequal(X, 0)) to get nbNonZeroIndices
                var arrayX = (X as Tensor<float>).DownloadToNativeArray();
                int nbNonZeroIndices = 0;
                for (int i = 0; i < X.shape.length; ++i)
                {
                    if (arrayX[i] != 0)
                        nbNonZeroIndices += 1;
                }

                // compact with condition mask?
                var O = ctx.storage.AllocateTensorAndStore(outputs[0], new TensorShape(X.shape.rank, nbNonZeroIndices), DataType.Int, outputBackendType) as Tensor<int>;
                if (O.shape.HasZeroDims())
                    return;
                var arrayO = new NativeArray<int>(O.shape.length, Allocator.Temp);

                int nonZeroIndicesIdx = 0;
                for (var it = new TensorNDIterator(X.shape); it.HasNext(); it.MoveNext())
                {
                    if (arrayX[it.index] != 0)
                    {
                        for (int i = 0; i < X.shape.rank; i++)
                            arrayO[i * nbNonZeroIndices + nonZeroIndicesIdx] = it[i];
                        nonZeroIndicesIdx++;
                    }
                }
                O.dataOnBackend.Upload(arrayO, arrayO.Length);
            }
        }
    }

    /// <summary>
    /// Represents a `ScatterElements` layer. This copies the input tensor and updates values at indexes specified by the `indices` tensor with values specified by the `updates` tensor along a given axis.
    ///
    /// `ScatterElements` updates the values depending on the reduction mode used.
    /// </summary>
    [Operator(category = "Indexing")]
    [Inputs(names = new[] { "input", "indices", "updates" })]
    partial class ScatterElements : Layer
    {
        public int axis;
        public ScatterReductionMode reduction;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor indices, PartialTensor updates, int axis, ScatterReductionMode reduction)
        {
            var dataType = input.dataType;
            var shapeInput = input.shape;
            var shapeIndices = indices.shape;
            var shapeUpdates = updates.shape;

            if (!shapeInput.hasRank && !shapeIndices.hasRank && !shapeUpdates.hasRank)
                return PartialTensor.Create(dataType);

            if (!shapeInput.hasRank && shapeIndices.hasRank)
                shapeInput = DynamicTensorShape.DynamicOfRank(shapeIndices.rank);

            if (!shapeInput.hasRank && shapeUpdates.hasRank)
                shapeInput = DynamicTensorShape.DynamicOfRank(shapeUpdates.rank);

            if (shapeInput.hasRank)
                Logger.AssertIsTrue(shapeInput.rank >= 1, "RankError: incorrect rank, expecting at least {0}, got {1}", 1, shapeInput.rank);

            shapeIndices.DeclareRank(shapeInput.rank);
            shapeUpdates.DeclareRank(shapeInput.rank);

            // throw error if axis incorrect
            shapeInput.Axis(axis);

            // throw error if indices and updates don't match
            for (var i = 0; i < shapeIndices.rank; i++)
            {
                DynamicTensorDim.MaxDefinedDim(shapeIndices[i], shapeUpdates[i]);
            }

            return PartialTensor.Create(dataType, shapeInput);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            var indices = ctx.storage.GetTensor(inputs[1]) as Tensor<int>;
            var updates = ctx.storage.GetTensor(inputs[2]);
            Logger.AssertIsTrue(indices.shape == updates.shape, "ScatterElements.InputError indices and updates must have same shape");
            if (indices.shape.HasZeroDims())
                ctx.backend.MemCopy(X, O);
            else
                ctx.backend.ScatterElements(X, indices, updates, O, axis, reduction);
        }
    }

    /// <summary>
    /// Represents a `ScatterND` layer. This copies the input tensor and updates values at indexes specified by the `indices` tensor with values specified by the `updates` tensor.
    ///
    /// `ScatterND` updates the values depending on the reduction mode used.
    /// </summary>
    [Operator(category = "Indexing")]
    [Inputs(names = new[] { "input", "indices", "updates" })]
    partial class ScatterND : Layer
    {
        public ScatterReductionMode reduction;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor indices, PartialTensor updates, ScatterReductionMode reduction)
        {
            var dataType = input.dataType;
            var shapeInput = input.shape;
            var shapeIndices = indices.shape;
            var shapeUpdates = updates.shape;

            if (shapeIndices.hasRank)
                Logger.AssertIsTrue(shapeIndices.rank >= 1, "RankError: incorrect rank, expecting at least {0}, got {1}", shapeIndices.rank, 1);

            if (shapeIndices.hasRank && shapeUpdates.hasRank && shapeIndices[-1].isValue)
                shapeInput.DeclareRank(shapeUpdates.rank - (shapeIndices.rank - shapeIndices[-1].value - 1));

            if (shapeInput.hasRank)
                Logger.AssertIsTrue(shapeInput.rank >= 1, "RankError: incorrect rank, expecting at least {0}, got {1}", 1, shapeInput.rank);

            return PartialTensor.Create(dataType, shapeInput);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            var indices = ctx.storage.GetTensor(inputs[1]) as Tensor<int>;
            if (indices.shape.HasZeroDims())
            {
                ctx.backend.MemCopy(X, O);
                return;
            }

            if (X is Tensor<int>)
                ctx.backend.ScatterND(X as Tensor<int>, ctx.storage.GetTensor(inputs[1]) as Tensor<int>, ctx.storage.GetTensor(inputs[2]) as Tensor<int>, O as Tensor<int>, reduction);
            else
                ctx.backend.ScatterND(X as Tensor<float>, ctx.storage.GetTensor(inputs[1]) as Tensor<int>, ctx.storage.GetTensor(inputs[2]) as Tensor<float>, O as Tensor<float>, reduction);
        }
    }

    /// <summary>
    /// Represents a `TopK` layer. This calculates the top-K largest or smallest elements of an input tensor along a given axis.
    ///
    /// This layer calculates both the values tensor of the top-K elements and the indices tensor of the top-K elements as outputs.
    /// </summary>
    [Operator(category = "Indexing")]
    [Inputs(names = new[] { "input", "k" }, inputCPURead = new[] { 1 })]
    [Outputs(names = new[] { "values", "indices" })]
    partial class TopK : Layer
    {
        public int axis;
        public bool largest;
        public bool sorted;

        internal override int OutputCount => 2;

        internal static PartialTensor[] InferPartial(PartialTensor input, PartialTensor k, int axis, bool largest, bool sorted)
        {
            var dataType = input.dataType;
            var shapeInput = input.shape;
            if (!shapeInput.hasRank)
                return new[] { PartialTensor.Create(dataType), new PartialTensor<int>() };

            var shapeK = k.shape;
            shapeK.DeclareRank(1);
            Logger.AssertIsFalse(shapeK[0] != 1, "TopK.InputError: k must be a single value");

            var shapeOut = new DynamicTensorShape(shapeInput);

            var axisX = shapeInput.Axis(axis);

            shapeOut[axisX] = (DynamicTensorDim)k.Get<int>(0);
            return new[] { PartialTensor.Create(dataType, shapeOut), new PartialTensor<int>(shapeOut) };
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var k = ctx.storage.GetInt(inputs[1]);
            var outputShape = new TensorShape(X.shape);
            outputShape[axis] = k;

            var values = ctx.storage.AllocateTensorAndStore(outputs[0], outputShape, X.dataType, ctx.backend.backendType);
            var indices = ctx.storage.AllocateTensorAndStore(outputs[1], outputShape, DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (outputShape.HasZeroDims())
                return;
            if (X is Tensor<int>)
                ctx.backend.TopK(X as Tensor<int>, values as Tensor<int>, indices, k, axis, largest);
            else
                ctx.backend.TopK(X as Tensor<float>, values as Tensor<float>, indices, k, axis, largest);
        }
    }
}
