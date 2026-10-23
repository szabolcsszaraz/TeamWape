using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.InferenceEngine.Layers
{
    /// <summary>
    /// Options for the padding values for `Pad`.
    /// </summary>
    enum PadMode
    {
        /// <summary>
        /// Use a constant value for the padded data.
        /// </summary>
        Constant,
        /// <summary>
        /// Use the reflection of the values of the input tensor mirrored on the first and last values along the axis. The edge values appear once in the output tensor.
        /// </summary>
        Reflect,
        /// <summary>
        /// Use the edge values of the input tensor.
        /// </summary>
        Edge,
        /// <summary>
        /// Use the reflection of the values of the input tensor mirrored half a step outside the first and last values along the axis. The edge values appear twice in the output tensor.
        /// </summary>
        Symmetric,
        /// <summary>
        /// Wrap the values of the input tensor like a torus for the padded data.
        /// </summary>
        Wrap,
    }

    /// <summary>
    /// Options for the scaling mode to use for `Resize`.
    /// </summary>
    enum ScaleMode
    {
        /// <summary>
        /// Use the size tensor directly for the shape of the output tensor.
        /// </summary>
        Sizes,
        /// <summary>
        /// Use the scales tensor to multiply the shape of the input tensor to calculate the shape of the output tensor.
        /// </summary>
        Scales
    }

    /// <summary>
    /// Options for the interpolation mode to use for `Resize` and `GridSample`
    /// </summary>
    enum InterpolationMode
    {
        /// <summary>
        /// Use the nearest element to the calculated coordinate. The exact behaviour depends on `nearestMode`.
        /// </summary>
        Nearest,
        /// <summary>
        /// Use a linear sampling of the surrounding elements to the calculated coordinate.
        /// </summary>
        Linear,
        /// <summary>
        /// Use a cubic sampling of the surrounding elements to the calculated coordinate.
        /// </summary>
        Cubic // Not actually implemented. Currently maps to Linear. See SENTIS-556, SENTIS-1352
    }

    /// <summary>
    /// Options for how to sample the nearest element in `Resize` when using `InterpolationMode.NearestMode`.
    /// </summary>
    enum NearestMode
    {
        /// <summary>
        /// Use rounding to the nearest integer coordinate. If the fractional part equals 0.5 then round down.
        /// </summary>
        RoundPreferFloor,
        /// <summary>
        /// Use rounding to the nearest integer coordinate. If the fractional part equals 0.5 then round up.
        /// </summary>
        RoundPreferCeil,
        /// <summary>
        /// Use rounding down to the next integer coordinate less than or equal to the input coordinate.
        /// </summary>
        Floor,
        /// <summary>
        /// Use rounding up to the next integer coordinate greater than or equal to the input coordinate.
        /// </summary>
        Ceil
    }

    /// <summary>
    /// Padding mode for outside grid values.
    /// </summary>
    enum PaddingMode
    {
        /// <summary>
        /// Use 0 for out-of-bound grid locations.
        /// </summary>
        Zeros,
        /// <summary>
        /// Use border value for out-of-bound grid locations.
        /// </summary>
        Border,
        /// <summary>
        /// Use values at locations reflected by the border for out-of-bound grid locations. Distant values are reflected multiple times until in bounds.
        /// </summary>
        Reflection
    }

    /// <summary>
    /// Options for how to transform between the coordinate in the output tensor and the coordinate in the input tensor in `Resize`.
    /// </summary>
    enum CoordTransformMode
    {
        /// <summary>
        /// Use shifting by half a pixel before and after scaling.
        /// </summary>
        HalfPixel,
        /// <summary>
        /// Use shifting by half a pixel before and after scaling if the output length is greater than 1, otherwise use 0.
        /// </summary>
        PytorchHalfPixel,
        /// <summary>
        /// Use scaling by `length - 1` so that corner pixels align.
        /// </summary>
        AlignCorners,
        /// <summary>
        /// Use direct scaling of coordinates by the scaling factor.
        /// </summary>
        Asymmetric,
    }

    /// <summary>
    /// Options for which part of the input matrix to retain in `Trilu`.
    /// </summary>
    enum TriluMode
    {
        /// <summary>
        /// Use retaining of the lower part of the input matrix.
        /// </summary>
        Lower = 0,
        /// <summary>
        /// Use retaining of the upper part of the input matrix.
        /// </summary>
        Upper = 1,
    }

    /// <summary>
    /// Options for the ordering of the elements in `DepthToSpace`.
    /// </summary>
    enum DepthToSpaceMode
    {
        /// <summary>
        /// Use depth, column, row ordering where the data is arranged (by * blocksize * channels) + (bx * channels) + c.
        /// </summary>
        DepthColumnRow,
        /// <summary>
        /// Use column, row, depth ordering where the data is arranged (c * blocksize * blocksize) + (by * blocksize) + bx.
        /// </summary>
        ColumnRowDepth,
    }

    /// <summary>
    /// Represents an `AsStrided` layer. The output tensor with a given size filled with values from the input tensor with given stride and offsets.
    /// </summary>
    [Operator(category = "Transformation")]
    [Inputs(names = new[] { "input", "shape", "strides", "offset" }, inputCPURead = new[] { 1, 2, 3 })]
    partial class AsStrided : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor shape, PartialTensor strides, PartialTensor offset)
        {
            if (!shape.isPartiallyKnown)
                return PartialTensor.Create(input.dataType, DynamicTensorShape.DynamicRank);
            var outShape = DynamicTensorShape.DynamicOfRank(shape.length);
            for (var i = 0; i < shape.length; i++)
                outShape[i] = (DynamicTensorDim)shape.Get<int>(i);
            return PartialTensor.Create(input.dataType, outShape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var shape = ctx.storage.GetInts(inputs[1]);
            var strides = ctx.storage.GetInts(inputs[2]);
            var offset = ctx.storage.GetInt(inputs[3]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], new TensorShape(shape), X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.AsStrided(X, O, strides, offset);
        }
    }

    /// <summary>
    /// Represents an element-wise `Cast` layer: f(x) = (float)x or f(x) = (int)x depending on the value of `toType`.
    /// </summary>
    [Operator(category = "Transformation")]
    partial class Cast : Layer
    {
        public DataType toType;

        internal static PartialTensor InferPartial(PartialTensor input, DataType toType)
        {
            if (toType == DataType.Float)
                return input.Cast<float>();
            return input.Cast<int>();
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, toType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;

            if (X.dataType == O.dataType)
                ctx.backend.MemCopy(X, O);
            else if (X.dataType == DataType.Int && O.dataType == DataType.Float)
                ctx.backend.Cast(X as Tensor<int>, O as Tensor<float>);
            else if (X.dataType == DataType.Float && O.dataType == DataType.Int)
                ctx.backend.Cast(X as Tensor<float>, O as Tensor<int>);
            else if (X.dataType == DataType.Short && O.dataType == DataType.Float)
                ctx.backend.Cast(X as Tensor<short>, O as Tensor<float>);
            else
                throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Represents an element-wise `CastLike` layer: f(x) = (float)x or f(x) = (int)x depending on the data type of the targetType tensor.
    /// </summary>
    [Operator(category = "Transformation")]
    [Inputs(names = new[] { "input", "targetType" }, inputNoDataDependency = new[] { 1 })]
    partial class CastLike : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor targetType)
        {
            return PartialTensor.Create(targetType.dataType, input.shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var dataType = ctx.storage.GetDataType(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;

            if (X.dataType == dataType)
                ctx.backend.MemCopy(X, O);
            else if (X.dataType == DataType.Int && dataType == DataType.Float)
                ctx.backend.Cast(X as Tensor<int>, O as Tensor<float>);
            else if (X.dataType == DataType.Float && dataType == DataType.Int)
                ctx.backend.Cast(X as Tensor<float>, O as Tensor<int>);
            else if (X.dataType == DataType.Short && dataType == DataType.Float)
                ctx.backend.Cast(X as Tensor<short>, O as Tensor<float>);
            else
                throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Represents a `Concat` concatenation layer. The layer computes the output tensor by concatenating the input tensors along a given axis.
    /// </summary>
    [Operator(category = "Transformation")]
    [Inputs(isVariadic = true)]
    partial class Concat : Layer
    {
        public int axis;

        internal static PartialTensor InferPartial(PartialTensor[] inputTensors, int axis)
        {
            Logger.AssertIsTrue(inputTensors.Length > 0, "Concat.InputError: can't broadcast shapes array of size 0");

            var dataType = inputTensors[0].dataType;

            var rank = DynamicTensorDim.Unknown;
            foreach (var tensorInput in inputTensors)
            {
                if (tensorInput.shape.hasRank)
                    rank = DynamicTensorDim.MaxDefinedDim(rank, DynamicTensorDim.Int(tensorInput.shape.rank));
            }

            if (rank.isUnknown)
                return PartialTensor.Create(dataType, DynamicTensorShape.DynamicRank);

            foreach (var tensorInput in inputTensors)
                tensorInput.shape.DeclareRank(rank.value);

            var shapeOut = DynamicTensorShape.DynamicOfRank(rank.value);
            var axisOut = shapeOut.Axis(axis);

            for (var i = 0; i < shapeOut.rank; i++)
            {
                if (i == axisOut)
                {
                    shapeOut[i] = DynamicTensorDim.Zero;
                    foreach (var tensorInput in inputTensors)
                    {
                        shapeOut[i] += tensorInput.shape[i];
                    }
                }
                else
                {
                    shapeOut[i] = DynamicTensorDim.Unknown;
                    foreach (var tensorInput in inputTensors)
                    {
                        shapeOut[i] = DynamicTensorDim.MaxDefinedDim(shapeOut[i], tensorInput.shape[i]);
                    }
                }
            }

            var tensorOut = PartialTensor.Create(dataType, shapeOut);

            if (shapeOut.rank != 1 || !tensorOut.isPartiallyKnown)
                return tensorOut;

            var index = 0;
            foreach (var X in inputTensors)
            {
                for (var i = 0; i < X.length; i++)
                {
                    tensorOut.CopyElement(index++, X, i);
                }
            }

            return tensorOut;
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var shapeO = ctx.storage.GetTensorShape(inputs[0]);
            for (var i = 1; i < inputs.Length; i++)
            {
                var shape = ctx.storage.GetTensorShape(inputs[i]);
                shapeO = shapeO.Concat(shape, axis);
            }
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shapeO, ctx.storage.GetDataType(inputs[0]), ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            // this is necessary for not propagating NaN values
            if (ctx.backend.backendType == BackendType.GPUPixel)
                ctx.backend.MemClear(O);
            var start = 0;
            for (var i = 0; i < inputs.Length; i++)
            {
                var X = ctx.storage.GetTensor(inputs[i]);
                var length = X.shape[axis];
                if (length == 0)
                    continue;
                ctx.backend.SliceSet(X, O, axis, start, 1);
                start += length;
            }
        }
    }

    /// <summary>
    /// Represents a `DepthToSpace` layer. The layer computes the output tensor by permuting data from depth into blocks of spatial data.
    /// </summary>
    [Operator(category = "Transformation")]
    partial class DepthToSpace : Layer
    {
        public int blocksize;
        public DepthToSpaceMode mode;

        internal static PartialTensor InferPartial(PartialTensor input, int blocksize, DepthToSpaceMode mode)
        {
            var shapeInput = input.shape;
            shapeInput.DeclareRank(4);
            return PartialTensor.Create(input.dataType, new DynamicTensorShape(shapeInput[0], shapeInput[1] / (blocksize * blocksize), shapeInput[2] * blocksize, shapeInput[3] * blocksize));
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.DepthToSpace(X.shape, blocksize), X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.DepthToSpace(X, O, blocksize, mode);
        }
    }

    /// <summary>
    /// Represents a `Diagonal` layer. The layer computes the output tensor by appending the diagonal elements with respect to dim1 and dim2 to the other dimensions.
    /// </summary>
    [Operator(category = "Transformation")]
    partial class Diagonal : Layer
    {
        public int offset;
        public int dim1;
        public int dim2;

        internal static PartialTensor InferPartial(PartialTensor input, int offset, int dim1, int dim2)
        {
            if (input.shape.isRankDynamic)
                return PartialTensor.Create(input.dataType, DynamicTensorShape.DynamicRank);
            var outShape = DynamicTensorShape.DynamicOfRank(input.shape.rank - 1);
            dim1 = dim1 < 0 ? dim1 + input.shape.rank : dim1;
            dim2 = dim2 < 0 ? dim2 + input.shape.rank : dim2;
            var j = 0;
            for (var i = 0; i < input.shape.rank; i++)
            {
                if (i == dim1 || i == dim2)
                    continue;
                outShape[j] = input.shape[i];
                j++;
            }
            var size1 = input.shape[dim1];
            var size2 = input.shape[dim2];
            if (size1.isValue && size2.isValue)
                outShape[-1] = DynamicTensorDim.Int(Mathf.Max(0, Mathf.Min(size1.value + Mathf.Min(0, offset), size2.value - Mathf.Max(0, offset))));
            return PartialTensor.Create(input.dataType, outShape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            Span<int> strides = stackalloc int[TensorShape.maxRank];
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape.Diagonal(offset, dim1, dim2, ref strides, out var storageOffset), X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.AsStrided(X, O, strides.Slice(TensorShape.maxRank - O.shape.rank), storageOffset);
        }
    }

    /// <summary>
    /// Represents an `Expand` layer. The layer computes the output tensor by broadcasting the input tensor into a given shape.
    /// </summary>
    [Operator(category = "Transformation")]
    [Inputs(names = new[] { "input", "shape" }, inputCPURead = new[] { 1 })]
    partial class Expand : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor shape)
        {
            return PartialTensor.Create(input.dataType, DynamicTensorShape.FromPartialTensor(shape as PartialTensor<int>).Broadcast(input.shape));
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var shape = new TensorShape(ctx.storage.GetInts(inputs[1]));
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape.Broadcast(shape), X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Expand(X, O);
        }
    }

    /// <summary>
    /// Represents a `Flatten` layer. The layer computes the output tensor by reshaping the input tensor into a 2D matrix according to the given axis.
    /// </summary>
    [Operator(category = "Transformation")]
    partial class Flatten : Layer
    {
        public int axis;

        internal static PartialTensor InferPartial(PartialTensor input, int axis)
        {
            var shapeInput = input.shape;
            if (!shapeInput.hasRank)
            {
                if (axis == 0)
                    return input.Reshape(new DynamicTensorShape(DynamicTensorDim.One, shapeInput.Length()));
                return input.Reshape(DynamicTensorShape.DynamicOfRank(2));
            }

            var axisX = axis >= 0 ? axis : shapeInput.rank + axis;

            var shapeOut = DynamicTensorShape.Ones(2);
            for (var i = 0; i < axisX; i++)
            {
                shapeOut[0] *= shapeInput[i];
            }
            for (var i = axisX; i < shapeInput.rank; i++)
            {
                shapeOut[1] *= shapeInput[i];
            }

            return input.Reshape(shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var shape = X.shape.Flatten(axis);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Reshape(X, O);
        }
    }

    /// <summary>
    /// Represents a `GridSample` layer. The layer computes the output tensor by sampling the input tensor with coordinates given by the grid tensor.
    /// </summary>
    [Operator(category = "Transformation")]
    [Inputs(names = new[] { "input", "grid" })]
    partial class GridSample : Layer
    {
        public InterpolationMode mode;
        public PaddingMode paddingMode;
        public bool alignCorners;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor grid, InterpolationMode interpolationMode, PaddingMode paddingMode, bool alignCorners)
        {
            var outShape = DynamicTensorShape.DynamicRank;

            if (input.shape.hasRank)
                outShape.DeclareRank(input.shape.rank);
            if (grid.shape.hasRank)
                outShape.DeclareRank(grid.shape.rank);

            for (var i = 0; i < (outShape.hasRank ? outShape.rank : 0); i++)
            {
                outShape[i] = i switch
                {
                    0 => DynamicTensorDim.MaxDefinedDim(input.shape[0], grid.shape[0]),
                    1 => input.shape[i],
                    _ => grid.shape[i - 1]
                };
            }

            return PartialTensor.Create(input.dataType, outShape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var grid = ctx.storage.GetTensor(inputs[1]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.GridSample(X.shape, grid.shape), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.GridSample(X, grid, O, mode, paddingMode, alignCorners);
        }
    }

    /// <summary>
    /// Represents an `Identity` layer. The output tensor is a copy of the input tensor.
    /// </summary>
    [Operator(category = "Transformation")]
    partial class Identity : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input)
        {
            return input;
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.MemCopy(X, O);
        }
    }

    /// <summary>
    /// Represents a `MoveDim` layer. The layer computes the output tensor by moving the dimensions of input at the positions in source to the positions in destination.
    ///
    /// Other dimensions of input that are not explicitly moved remain in their original order and appear at the positions not specified in destination.
    /// </summary>
    [Operator(category = "Transformation")]
    partial class MoveDim : Layer
    {
        public int[] source;
        public int[] destination;

        internal static PartialTensor InferPartial(PartialTensor input, int[] source, int[] destination)
        {
            var shapeInput = input.shape;

            if (!shapeInput.hasRank)
                return PartialTensor.Create(input.dataType);

            var shapeOut = DynamicTensorShape.DynamicOfRank(shapeInput.rank);

            // move given dims
            uint srcAxesBitMask = 0;
            uint dstAxesBitMask = 0;
            for (var i = 0; i < source.Length; i++)
            {
                var srcAxis = shapeInput.Axis(source[i]);
                var dstAxis = shapeInput.Axis(destination[i]);
                Logger.AssertIsTrue(((srcAxesBitMask >> srcAxis) & 1U) == 0, "MoveDim.ValueError: source dims may not repeat");
                Logger.AssertIsTrue(((dstAxesBitMask >> dstAxis) & 1U) == 0, "MoveDim.ValueError: destination dims may not repeat");
                srcAxesBitMask |= 1U << srcAxis;
                dstAxesBitMask |= 1U << dstAxis;
                shapeOut[dstAxis] = shapeInput[srcAxis];
            }

            // fill remaining dims in order
            for (int srcAxis = 0, dstAxis = 0; srcAxis < shapeInput.rank; srcAxis++)
            {
                if (((srcAxesBitMask >> srcAxis) & 1U) != 0)
                    continue;
                while (((dstAxesBitMask >> dstAxis) & 1U) != 0)
                    dstAxis++;
                srcAxesBitMask |= 1U << srcAxis;
                dstAxesBitMask |= 1U << dstAxis;
                shapeOut[dstAxis] = shapeInput[srcAxis];
                dstAxis++;
            }

            return PartialTensor.Create(input.dataType, shapeOut);
        }

        internal override unsafe void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);

            Span<int> permutations = stackalloc int[X.shape.rank];
            ShapeInference.MoveDim(X.shape, source, destination, ref permutations);

            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape.Transpose(permutations), X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Transpose(X, O, permutations);
        }
    }

    /// <summary>
    /// Represents a `Narrow` layer. The layer calculates the output tensor by slicing the input tensor along a given dim with a given start and length.
    /// </summary>
    [Operator(category = "Transformation")]
    [Inputs(names = new[] { "input", "dim", "start", "length" }, inputCPURead = new[] { 1, 2, 3 })]
    partial class Narrow : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor dim, PartialTensor start, PartialTensor length)
        {
            if (!dim.IsStatic())
                return PartialTensor.Create(input.dataType, DynamicTensorShape.DynamicOfRank(input.shape.rank));

            var dimValue = dim.Get<int>(0).value;

            var outShape = input.shape;
            outShape[dimValue] = DynamicTensorDim.Unknown;

            if (start.IsStatic() && length.IsStatic() && input.shape[dimValue].isValue)
            {
                var dimSize = input.shape[dimValue].value;
                var startValue = (start.Get<int>(0).value + dimSize) % dimSize;
                var end = Mathf.Min(startValue + length.Get<int>(0).value, dimSize);
                var lengthValue = end - startValue;
                outShape[dimValue] = DynamicTensorDim.FromInt(lengthValue);

                if (input.isPartiallyKnown)
                {
                    var tensorOut = PartialTensor.Create(input.dataType, outShape);
                    for (var i = 0; i < lengthValue; i++)
                        tensorOut.CopyElement(i, input, startValue + i);
                    return tensorOut;
                }
            }

            return PartialTensor.Create(input.dataType, outShape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var dim = ctx.storage.GetInt(inputs[1]);
            var start = ctx.storage.GetInt(inputs[2]);
            var length = ctx.storage.GetInt(inputs[3]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.MoveDim(X.shape, ref dim, ref start, ref length), X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Split(X, O, dim, start);
        }
    }

    /// <summary>
    /// Represents a `Pad` layer. The layer calculates the output tensor by adding padding to the input tensor according to the given padding values and mode.
    /// </summary>
    [Operator(category = "Transformation")]
    [Inputs(names = new[] { "data", "pads", "constantValue", "axes" }, inputCPURead = new[] { 1, 2, 3 })]
    partial class Pad : Layer
    {
        public PadMode padMode;

        internal static PartialTensor InferPartial(PartialTensor data, PartialTensor pads, PartialTensor constantValue, PartialTensor axes, PadMode padMode)
        {
            var shapeData = data.shape;
            var shapePads = pads.shape;
            if (shapePads.hasRank)
            {
                Logger.AssertIsTrue(shapePads.rank == 1, "Pad.ValueError: pads must be rank 1");
                Logger.AssertIsTrue(!shapePads[0].isValue || shapePads[0].value % 2 == 0, "Pad.ValueError: length of pads must divide by 2");
            }

            if (axes == null)
            {
                shapeData.DeclareRank(shapePads[0] / 2);
                axes = shapeData.hasRank ? PartialTensor.Range(0, shapeData.rank) : new PartialTensor<int>(DynamicTensorShape.DynamicOfRank(1));
            }

            if (!axes.isPartiallyKnown)
                return PartialTensor.Create(data.dataType, DynamicTensorShape.DynamicOfRankLike(shapeData));

            Logger.AssertIsTrue(!shapePads[0].isValue || shapePads[0].value == axes.length * 2, "Pad.ValueError: length of pads must be twice the length of the axes");

            var shapeOut = new DynamicTensorShape(shapeData);

            for (var i = 0; i < axes.length; i++)
            {
                if (!axes.Get<int>(i).isValue)
                    continue;
                var axis = axes.Get<int>(i).value;
                var dimPad = pads.Get<int>(i) + pads.Get<int>(i + axes.length);
                shapeOut[axis] = (DynamicTensorDim)((PartialTensorElement<int>)shapeData[axis] + dimPad);
            }

            return PartialTensor.Create(data.dataType, shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var pad = ctx.storage.GetInts(inputs[1]);
            var axes = ctx.storage.GetInts(inputs[3], null);

            Span<int> pads = stackalloc int[2 * X.shape.rank];
            if (axes != null)
            {
                for (var i = 0; i < axes.Length; i++)
                {
                    var axis = X.shape.Axis(axes[i]);
                    pads[axis] = pad[i];
                    pads[axis + X.shape.rank] = pad[i + axes.Length];
                }
            }
            else
            {
                pad.CopyTo(pads);
            }

            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape.Pad(pads), X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (padMode != PadMode.Constant)
            {
                Assert.IsFalse(X.shape.HasZeroDims(), "ValueError: zero dimensions input for Pad operator is not supported");
                if (X.dataType == DataType.Float)
                    ctx.backend.Pad(X as Tensor<float>, O as Tensor<float>, pads, padMode, 0);
                else
                    ctx.backend.Pad(X as Tensor<int>, O as Tensor<int>, pads, padMode, 0);
                return;
            }

            if (X.dataType == DataType.Float)
            {
                var constantValue = ctx.storage.GetFloat(inputs[2]);
                if (X.shape.HasZeroDims())
                    ctx.backend.MemSet(O as Tensor<float>, constantValue);
                else
                    ctx.backend.Pad(X as Tensor<float>, O as Tensor<float>, pads, padMode, constantValue);
            }
            else
            {
                var constantValue = ctx.storage.GetInt(inputs[2]);
                if (X.shape.HasZeroDims())
                    ctx.backend.MemSet(O as Tensor<int>, constantValue);
                else
                    ctx.backend.Pad(X as Tensor<int>, O as Tensor<int>, pads, padMode, constantValue);
            }
        }
    }

    /// <summary>
    /// Represents a `Reshape` layer. The layer calculates the output tensor by copying the data from the input tensor and using a given shape. The data from the input tensor is unchanged.
    ///
    /// Only one of the elements of the shape can be -1. The layer infers the size of this dimension from the remaining dimensions and the length of the input tensor.
    /// </summary>
    [Operator(category = "Transformation")]
    [Inputs(names = new[] { "input", "shape" }, inputCPURead = new[] { 1 })]
    partial class Reshape : Layer
    {
        public bool allowZero;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor shape, bool allowZero)
        {
            var shapeInput = input.shape;
            shape.shape.DeclareRank(1);

            if (!shape.isPartiallyKnown)
            {
                if (shape.shape[0].isValue)
                    return input.Reshape(DynamicTensorShape.DynamicOfRank(shape.shape[0].value));
                return input.Reshape(DynamicTensorShape.DynamicRank);
            }

            var shapeOut = DynamicTensorShape.DynamicOfRank(shape.length);

            var containsMinusOne = false;

            for (var i = 0; i < shapeOut.rank; i++)
            {
                if (shape.Get<int>(i).Equals(-1))
                    containsMinusOne = true;
            }

            for (var i = 0; i < shapeOut.rank; i++)
            {
                var shapeDim = shape.Get<int>(i);
                if (shapeDim.isUnknown)
                    continue;

                var dim = (DynamicTensorDim)shapeDim;
                if (shapeDim.isParam)
                {
                    if (allowZero || (shapeInput.hasRank && i >= shapeInput.rank) || shapeInput[i] == dim)
                        shapeOut[i] = dim;
                    else if (containsMinusOne)
                    {
                        for (var j = 0; j < shapeInput.rank; j++)
                        {
                            if (shapeInput[j] == dim)
                            {
                                shapeOut[i] = dim;
                                break;
                            }
                        }
                    }
                    continue;
                }

                if (shapeDim.value > 0)
                    shapeOut[i] = dim;
                else if (shapeDim.value == 0)
                    shapeOut[i] = allowZero ? DynamicTensorDim.Zero : shapeInput[i];
            }

            return input.Reshape(shapeOut, !containsMinusOne);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var shape = X.shape.Reshape(ctx.storage.GetInts(inputs[1]), allowZero);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Reshape(X, O);
        }
    }

    /// <summary>
    /// Represents a `Resize` layer. The layer calculates the output tensor by resampling the input tensor along the spatial dimensions to a given shape.
    /// </summary>
    [Operator(category = "Transformation")]
    [Inputs(names = new[] { "input", "scalesOrSizes" }, inputCPURead = new[] { 1 })]
    partial class Resize : Layer
    {
        public ScaleMode scaleMode;
        public CoordTransformMode coordTransformMode;
        public InterpolationMode mode;
        public NearestMode nearestMode;
        public int[] axes;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor scalesOrSizes, ScaleMode scaleMode, CoordTransformMode coordTransformMode, InterpolationMode mode, NearestMode nearestMode, int[] axes)
        {
            var dataType = input.dataType;
            var shapeInput = input.shape;

            scalesOrSizes.shape.DeclareRank(1);
            if (axes == null)
                shapeInput.DeclareRank(scalesOrSizes.shape[0]);
            var shapeOut = new DynamicTensorShape(shapeInput);
            if (shapeOut.hasRank)
            {
                if (axes == null)
                {
                    for (var i = 0; i < shapeOut.rank; i++)
                        shapeOut[i] = scaleMode == ScaleMode.Sizes ? (DynamicTensorDim)scalesOrSizes.Get<int>(i) : shapeInput[i].Resize(scalesOrSizes.Get<float>(i));
                }
                else
                {
                    for (var i = 0; i < axes.Length; i++)
                    {
                        var axis = shapeOut.Axis(axes[i]);
                        shapeOut[axis] = scaleMode == ScaleMode.Sizes ? (DynamicTensorDim)scalesOrSizes.Get<int>(i) : shapeInput[axis].Resize(scalesOrSizes.Get<float>(i));
                    }
                }
            }

            return PartialTensor.Create(dataType, shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            Span<float> s = stackalloc float[X.shape.rank];
            for (var i = 0; i < s.Length; i++)
                s[i] = 1f;

            if (scaleMode == ScaleMode.Sizes)
            {
                var sizes = ctx.storage.GetInts(inputs[1]);

                if (axes != null)
                {
                    for (var i = 0; i < axes.Length; i++)
                    {
                        var axis = X.shape.Axis(axes[i]);
                        s[axis] = sizes[i] / (float)X.shape[axis];
                    }
                }
                else
                {
                    for (var i = 0; i < X.shape.rank; i++)
                        s[i] = sizes[i] / (float)X.shape[i];
                }

                var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.Resize(X.shape, s), DataType.Float, ctx.backend.backendType) as Tensor<float>;
                if (O.shape.HasZeroDims())
                    return;
                ctx.backend.Resize(X, O, s, mode, nearestMode, coordTransformMode);
            }
            else
            {
                var scales = ctx.storage.GetFloats(inputs[1]);
                if (axes != null)
                {
                    for (var i = 0; i < axes.Length; i++)
                    {
                        var axis = X.shape.Axis(axes[i]);
                        s[axis] = scales[i];
                    }
                }
                else
                {
                    for (var i = 0; i < X.shape.rank; i++)
                        s[i] = scales[i];
                }
                var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.Resize(X.shape, s), DataType.Float, ctx.backend.backendType) as Tensor<float>;
                if (O.shape.HasZeroDims())
                    return;
                ctx.backend.Resize(X, O, s, mode, nearestMode, coordTransformMode);
            }
        }
    }

    /// <summary>
    /// Represents a `Select` layer. The layer calculates the output tensor by slicing the input tensor along a given dim with a given index, the sliced dim is removed from the output.
    /// </summary>
    [Operator(category = "Transformation")]
    [Inputs(names = new[] { "input", "dim", "selectIndex" }, inputCPURead = new[] { 1, 2 })]
    partial class Select : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor dim, PartialTensor selectIndex)
        {
            var outShape = input.shape;
            if (!dim.IsStatic())
            {
                outShape = input.shape.hasRank ? DynamicTensorShape.DynamicOfRank(input.shape.rank - 1) : DynamicTensorShape.DynamicRank;
                return PartialTensor.Create(input.dataType, outShape);
            }

            var axis = dim.Get<int>(0).value;
            outShape[axis] = DynamicTensorDim.One;
            outShape = outShape.Squeeze(axis);
            var tensorOut = PartialTensor.Create(input.dataType, outShape);

            if (axis == 0 && input.isPartiallyKnown && selectIndex.IsStatic())
            {
                var index = selectIndex.Get<int>(0).value;
                index = index < 0 ? index + input.shape.Length().value : index;
                tensorOut.CopyElement(0, input, index);
            }

            return tensorOut;
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var dim = ctx.storage.GetInt(inputs[1]);
            var selectIndex = ctx.storage.GetInt(inputs[2]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape.Reduce(dim, false), X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            var unsqueezed = ctx.storage.AllocateTensor(X.shape.Reduce(dim, true), X.dataType, ctx.backend.backendType);
            dim = X.shape.Axis(dim);
            selectIndex = selectIndex < 0 ? selectIndex + X.shape[dim] : selectIndex;
            ctx.backend.Split(X, unsqueezed, dim, selectIndex);
            ctx.backend.Reshape(unsqueezed, O);
            ctx.storage.Dispose(unsqueezed);
        }
    }

    /// <summary>
    /// Represents a `Slice` layer. The layer calculates the output tensor by slicing the input tensor along given axes with given starts, ends and steps.
    /// </summary>
    [Operator(category = "Transformation")]
    [Inputs(names = new[] { "input", "starts", "ends", "axes", "steps" }, inputCPURead = new[] { 1, 2, 3, 4 })]
    partial class Slice : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor starts, PartialTensor ends, PartialTensor axes, PartialTensor steps)
        {
            if (!input.shape.hasRank)
                return PartialTensor.Create(input.dataType);

            axes ??= PartialTensor.Range(0, input.shape.rank);
            steps ??= PartialTensor.Ones(starts.shape);

            if (input.isPartiallyKnown && input.shape.rank == 1 && starts.Get<int>(0).isValue && ends.Get<int>(0).isValue && steps.Get<int>(0).isValue)
            {
                var dim = input.shape[0].value;
                var start = starts.Get<int>(0).value;
                var end = ends.Get<int>(0).value;
                var step = steps.Get<int>(0).value;

                var clampAdjustDirection = step < 0 ? -1 : 0;

                start = start < 0 ? dim + start : start;
                start = Mathf.Clamp(start, 0, dim + clampAdjustDirection);

                end = end < 0 ? dim + end : end;
                end = Mathf.Clamp(end, clampAdjustDirection, dim);

                var length = (int)Math.Ceiling((end - start) / (double)step);
                length = Mathf.Max(length, 0);

                var tensorOut = PartialTensor.Create(input.dataType, new DynamicTensorShape(length));

                for (var i = 0; i < length; i++)
                {
                    tensorOut.CopyElement(i, input, start + i * step);
                }

                return tensorOut;
            }

            if (!axes.isPartiallyKnown)
                return PartialTensor.Create(input.dataType, DynamicTensorShape.DynamicOfRank(input.shape.rank));

            var shapeOut = new DynamicTensorShape(input.shape);

            for (var i = 0; i < axes.length; i++)
            {
                var axisElement = axes.Get<int>(i);
                if (!axisElement.isValue)
                {
                    shapeOut = DynamicTensorShape.DynamicOfRank(input.shape.rank);
                    continue;
                }
                var axis = shapeOut.Axis(axisElement.value);
                shapeOut[axis] = input.shape[axis].Slice(starts.Get<int>(i), ends.Get<int>(i), steps.Get<int>(i));
            }

            return PartialTensor.Create(input.dataType, shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var starts = ctx.storage.GetInts(inputs[1]);
            var ends = ctx.storage.GetInts(inputs[2]);
            var axes = ctx.storage.GetInts(inputs[3], null);
            var steps = ctx.storage.GetInts(inputs[4], null);
            var numAxes = starts.Length;
            Span<int> startsSpan = stackalloc int[numAxes];
            Span<int> endsSpan = stackalloc int[numAxes];
            Span<int> axesSpan = stackalloc int[numAxes];
            Span<int> stepsSpan = stackalloc int[numAxes];
            ShapeInference.Slice(X.shape, starts, ends, axes, steps, ref startsSpan, ref endsSpan, ref axesSpan, ref stepsSpan);
            var shapeOut = X.shape.Slice(startsSpan, endsSpan, axesSpan, stepsSpan);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shapeOut, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Slice(X, O, startsSpan, axesSpan, stepsSpan);
        }
    }

    [Operator(category = "Transformation")]
    [Inputs(names = new[] { "input", "values", "starts", "ends", "axes", "steps" }, inputCPURead = new[] { 2, 3, 4, 5 })]
    partial class SliceSet : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor values, PartialTensor starts, PartialTensor ends, PartialTensor axes, PartialTensor steps)
        {
            return PartialTensor.Create(input.dataType, input.shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var values = ctx.storage.GetTensor(inputs[1]);
            var starts = ctx.storage.GetInts(inputs[2]);
            var ends = ctx.storage.GetInts(inputs[3]);
            var axes = ctx.storage.GetInts(inputs[4], null);
            var steps = ctx.storage.GetInts(inputs[5], null);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            var numAxes = starts.Length;
            Span<int> startsSpan = stackalloc int[numAxes];
            Span<int> endsSpan = stackalloc int[numAxes];
            Span<int> axesSpan = stackalloc int[numAxes];
            Span<int> stepsSpan = stackalloc int[numAxes];
            for (var i = 0; i < numAxes; i++)
            {
                var axis = axes == null ? i : X.shape.Axis(axes[i]);
                var start = starts[i];
                var end = ends[i];
                var step = steps == null ? 1 : steps[i];

                stepsSpan[i] = step;
                axesSpan[i] = axis;

                var dim = X.shape[axis];
                var clampAdjustDirection = step < 0 ? -1 : 0;

                start = start < 0 ? dim + start : start;
                start = Mathf.Clamp(start, 0, dim + clampAdjustDirection);

                end = end < 0 ? dim + end : end;
                end = Mathf.Clamp(end, clampAdjustDirection, dim);

                startsSpan[i] = dim == 0 ? 0 : start;
                endsSpan[i] = dim == 0 ? 0 : end;
            }
            var slicedShape = X.shape.Slice(startsSpan, endsSpan, axesSpan, stepsSpan);
            Logger.AssertIsTrue(slicedShape.Broadcast(values.shape) == slicedShape, "SliceSet.InputError: values shape must be broadcastable to sliced shape, {0} {1}", values.shape, slicedShape);
            if (slicedShape != values.shape)
            {
                // broadcast values
                var broadcastValues = ctx.storage.AllocateTensor(slicedShape, values.dataType, ctx.backend.backendType);
                ctx.backend.Expand(values, broadcastValues);
                ctx.backend.SliceSet(X, broadcastValues, O, startsSpan, axesSpan, stepsSpan);
                ctx.storage.Dispose(broadcastValues);
            }
            else
            {
                ctx.backend.SliceSet(X, values, O, startsSpan, axesSpan, stepsSpan);
            }
        }
    }

    /// <summary>
    /// Represents a `SpaceToDepth` layer. The layer computes the output tensor by permuting data from blocks of spatial data into depth.
    /// </summary>
    [Operator(category = "Transformation")]
    partial class SpaceToDepth : Layer
    {
        public int blocksize;

        internal static PartialTensor InferPartial(PartialTensor input, int blocksize)
        {
            var shapeInput = input.shape;
            shapeInput.DeclareRank(4);
            return PartialTensor.Create(input.dataType, new DynamicTensorShape(shapeInput[0], shapeInput[1] * (blocksize * blocksize), shapeInput[2] / blocksize, shapeInput[3] / blocksize));
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.SpaceToDepth(X.shape, blocksize), X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.SpaceToDepth(X, O, blocksize);
        }
    }

    /// <summary>
    /// Represents a `Split` layer. The layer computes the output tensors by splitting the input tensor along a single given axis.
    /// </summary>
    [Operator(category = "Transformation")]
    [Inputs(names = new[] { "input", "split" }, inputCPURead = new[] { 1 })]
    [Outputs(isVariadic = true)]
    partial class Split : Layer
    {
        public int axis;
        public int numOutputs;

        internal override int OutputCount => numOutputs;

        internal static PartialTensor[] InferPartial(PartialTensor input, PartialTensor split, int axis, int numOutputs)
        {
            var partialSplit = split as PartialTensor<int>;
            if (partialSplit == null)
            {
                partialSplit = new PartialTensor<int>(new DynamicTensorShape(numOutputs));

                var dim = input.shape[axis];
                if (dim.isParam && numOutputs == 1)
                {
                    partialSplit[0] = PartialTensorElement<int>.Param(dim.param);
                }
                else if (dim.isValue)
                {
                    Logger.AssertIsTrue(numOutputs >= 1, "Split.InputError: numOutputs must be positive if split tensor is null");
                    var splitLength = Mathf.CeilToInt(dim.value / (float)numOutputs);
                    for (var i = 0; i < numOutputs - 1; i++)
                    {
                        partialSplit[i] = PartialTensorElement<int>.Value(splitLength);
                    }

                    // final split length is the (possible smaller) remainder along the axis
                    var lastSplitLength = dim.value - (splitLength * (numOutputs - 1));
                    Logger.AssertIsTrue(lastSplitLength >= 0, "Split.InputError: split axis too small for numOutputs");
                    partialSplit[numOutputs - 1] = PartialTensorElement<int>.Value(lastSplitLength);
                }
            }
            else
            {
                if (numOutputs == 0)
                    numOutputs = partialSplit.length;
            }

            var outputTensors = new PartialTensor[numOutputs];
            for (var i = 0; i < outputTensors.Length; i++)
            {
                var outputShape = new DynamicTensorShape(input.shape);
                outputShape[axis] = (DynamicTensorDim)partialSplit[i];
                outputTensors[i] = PartialTensor.Create(input.dataType, outputShape);
            }

            return outputTensors;
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);

            var dim = X.shape[axis];
            var equalSplitLength = 0;
            var split = ctx.storage.GetInts(inputs[1], null);
            if (split == null)
            {
                // if splits are not given calculate even split length
                equalSplitLength = (int)Math.Ceiling(dim / (double)numOutputs);
            }
            var start = 0;
            for (var i = 0; i < outputs.Length; i++)
            {
                var end = start + (split != null ? split[i] : equalSplitLength);
                end = Math.Min(end, dim);
                var O = ctx.storage.AllocateTensorAndStore(outputs[i], X.shape.Split(axis, start, end), X.dataType, ctx.backend.backendType);
                if (!O.shape.HasZeroDims())
                    ctx.backend.Split(X, O, X.shape.Axis(axis), start);
                start = end;
            }
        }
    }

    /// <summary>
    /// Represents a `Squeeze` layer. The layer computes the output tensor by reshaping the input tensor by removing dimensions of size 1.
    /// </summary>
    [Operator(category = "Transformation")]
    [Inputs(names = new[] { "input", "axes" }, inputCPURead = new[] { 1 })]
    partial class Squeeze : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor axes)
        {
            if (axes != null)
            {
                if (!axes.isPartiallyKnown)
                    return input.Reshape(DynamicTensorShape.DynamicRank);
                if (!axes.IsStatic())
                    return input.Reshape(DynamicTensorShape.DynamicOfRank(input.shape.rank - axes.length));
                return input.Reshape(input.shape.Squeeze(axes as PartialTensor<int>));
            }

            return input.Reshape(input.shape.Squeeze());
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var axes = ctx.storage.GetInts(inputs[1], null);
            var shape = axes != null ? X.shape.Squeeze(axes) : X.shape.Squeeze();
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Reshape(X, O); // TODO<tensordata> refcount tensordata
        }
    }

    /// <summary>
    /// Represents a `Tile` layer. The layer computes the output tensor by repeating the input layer a given number of times along each axis.
    /// </summary>
    [Operator(category = "Transformation")]
    [Inputs(names = new[] { "input", "repeats" }, inputCPURead = new[] { 1 })]
    partial class Tile : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor repeats)
        {
            var dataType = input.dataType;
            var shapeInput = input.shape;
            repeats.shape.DeclareRank(1);

            if (!repeats.isPartiallyKnown)
            {
                if (repeats.shape[0].isValue && !shapeInput.hasRank)
                    shapeInput = DynamicTensorShape.DynamicOfRank(repeats.shape[0].value);
                if (shapeInput.hasRank)
                    Logger.AssertIsFalse(repeats.shape[0] != shapeInput.rank, "Tile.InputError: repeats value must be equal to input rank");
                return PartialTensor.Create(dataType, DynamicTensorShape.DynamicOfRankLike(shapeInput));
            }

            shapeInput.DeclareRank(repeats.length);

            var shapeOut = new DynamicTensorShape(shapeInput);
            for (var i = 0; i < shapeOut.rank; i++)
            {
                shapeOut[i] *= (DynamicTensorDim)repeats.Get<int>(i);
            }
            return PartialTensor.Create(dataType, shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var repeats = ctx.storage.GetInts(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape.Tile(repeats), X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Tile(X, O, repeats);
        }
    }

    /// <summary>
    /// Represents a `Transpose` layer. The layer computes the output tensor by permuting the axes and data of the input tensor according to the given permutations.
    /// </summary>
    [Operator(category = "Transformation")]
    partial class Transpose : Layer
    {
        public int[] permutations;

        internal static PartialTensor InferPartial(PartialTensor input, int[] permutations)
        {
            var shapeInput = input.shape;
            if (permutations != null)
                shapeInput.DeclareRank(permutations.Length);

            if (!shapeInput.hasRank)
                return PartialTensor.Create(input.dataType);

            var shapeOut = DynamicTensorShape.DynamicOfRank(shapeInput.rank);

            if (permutations == null || permutations.Length == 0)
            {
                // reverse axes
                for (var i = 0; i < shapeInput.rank; i++)
                {
                    shapeOut[i] = shapeInput[shapeInput.rank - 1 - i];
                }
            }
            else
            {
                uint axesBitMask = 0;
                for (var i = 0; i < permutations.Length; i++)
                {
                    var axis = shapeInput.Axis(permutations[i]);
                    Logger.AssertIsTrue(((axesBitMask >> axis) & 1U) == 0, "Transpose.ValueError: permutation must be a permutation of the axis (0, rank-1)");
                    axesBitMask |= 1U << axis;
                    shapeOut[i] = shapeInput[axis];
                }
            }

            return PartialTensor.Create(input.dataType, shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            if (permutations == null)
            {
                var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape.Transpose(), X.dataType, ctx.backend.backendType);
                if (O.shape.HasZeroDims())
                    return;
                ctx.backend.Transpose(X, O);
                return;
            }
            else
            {
                var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape.Transpose(permutations), X.dataType, ctx.backend.backendType);
                if (O.shape.HasZeroDims())
                    return;
                ctx.backend.Transpose(X, O, permutations);
                return;
            }
        }
    }

    /// <summary>
    /// Represents a `Trilu` layer. The layer computes the output tensor by retaining the upper or lower triangular values from an input matrix or matrix batch and setting the other values to zero.
    /// </summary>
    [Operator(category = "Transformation")]
    [Inputs(names = new[] { "input", "k" }, inputCPURead = new[] { 1 })]
    partial class Trilu : Layer
    {
        public TriluMode mode;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor k, TriluMode mode)
        {
            return PartialTensor.Create(input.dataType, input.shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var k = ctx.storage.GetInt(inputs[1]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (mode == TriluMode.Upper)
                ctx.backend.Triu(X, O, k);
            else
                ctx.backend.Tril(X, O, k);
        }
    }

    /// <summary>
    /// Represents an `Unsqueeze` layer. The layer computes the output tensor by reshaping the input tensor by adding dimensions of size 1 at the given axes.
    /// </summary>
    [Operator(category = "Transformation")]
    [Inputs(names = new[] { "input", "axes" }, inputCPURead = new[] { 1 })]
    partial class Unsqueeze : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor axes)
        {
            return input.Reshape(input.shape.Unsqueeze(axes as PartialTensor<int>));
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var axes = ctx.storage.GetInts(inputs[1]);
            var shape = X.shape.Unsqueeze(axes);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Reshape(X, O); // TODO<tensordata> refcount tensordata
        }
    }
}
