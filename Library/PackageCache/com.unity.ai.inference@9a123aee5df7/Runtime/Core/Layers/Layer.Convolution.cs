using System;

namespace Unity.InferenceEngine.Layers
{
    /// <summary>
    /// Options for auto padding in image layers.
    /// </summary>
    enum AutoPad
    {
        /// <summary>
        /// Use explicit padding.
        /// </summary>
        NotSet,
        /// <summary>
        /// Use no padding.
        /// </summary>
        Valid,
        /// <summary>
        /// Use equal or almost equal padding on both sides. When the padding is odd, add the extra value to the end.
        /// </summary>
        SameUpper,
        /// <summary>
        /// Use equal or almost equal padding on both sides. When the padding is odd, add the extra value to the start.
        /// </summary>
        SameLower,
    }

    /// <summary>
    /// Represents a `Conv` convolution layer, which applies a convolution filter to an input tensor.
    /// </summary>
    [Operator(category = "Convolution")]
    [Inputs(names = new[] { "X", "W", "B" })]
    partial class Conv : Layer
    {
        public AutoPad autoPad;
        public int[] dilations;
        public int group;
        public int[] pads;
        public int[] strides;
        public int[] kernelShape;
        public FusableActivation fusedActivation;

        internal static PartialTensor InferPartial(PartialTensor X, PartialTensor W, PartialTensor B, AutoPad autoPad, int[] dilations, int group, int[] pads, int[] strides, int[] kernelShape, FusableActivation fusedActivation)
        {
            var shapeX = X.shape;
            var shapeKernel = W.shape;
            for (var i = 0; kernelShape != null && i < kernelShape.Length; i++)
            {
                shapeKernel[i + 2] = DynamicTensorDim.MaxDefinedDim(shapeKernel[i + 2], DynamicTensorDim.Int(kernelShape[i]));
            }

            if (!shapeX.hasRank)
                return new PartialTensor<float>();

            Logger.AssertIsTrue(shapeX.rank - 2 <= 3, "RankError: incorrect number of spatial dimensions in Conv, expecting at most {0}, got {1}", 3, shapeX.rank - 2);
            shapeKernel.DeclareRank(shapeX.rank);

            var shapeOut = DynamicTensorShape.DynamicOfRank(shapeX.rank);

            shapeOut[0] = shapeX[0];
            shapeOut[1] = shapeKernel[0];

            var shapeBias = B?.shape ?? DynamicTensorShape.DynamicRank;
            shapeBias.DeclareRank(1);
            shapeOut[1] = DynamicTensorDim.MaxDefinedDim(shapeOut[1], shapeBias[0]);

            for (var i = 2; i < shapeOut.rank; i++)
            {
                var stride = strides == null ? 1 : strides[i - 2];
                var pad = pads == null || autoPad != AutoPad.NotSet ? 0 : pads[i - 2] + pads[i - 2 + (shapeX.rank - 2)];
                var dilation = dilations == null ? 1 : dilations[i - 2];
                var dimX = shapeX[i];
                var dimKernel = shapeKernel[i];
                if (dimKernel.isValue)
                    shapeOut[i] = dimX.Pool(dimKernel.value, stride, pad, dilation, ceilMode:false, autoPad);
                else if (dimKernel.isParam && (autoPad is AutoPad.SameLower || autoPad is AutoPad.SameUpper))
                    shapeOut[i] = dimX.Pool(0, stride, pad, dilation, false, autoPad);
                else
                    shapeOut[i] = DynamicTensorDim.Unknown;
            }

            return new PartialTensor<float>(shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var W = ctx.storage.GetTensor(inputs[1]) as Tensor<float>;
            var B = ctx.storage.GetTensor(inputs[2]) as Tensor<float>;

            var numSpatialDims = X.shape.rank - 2;
            Logger.AssertIsTrue(numSpatialDims <= 3, "RankError: incorrect number of spatial dimensions in Conv, expecting at most {0}, got {1}", 3, numSpatialDims);
            var stridesSpan = strides.AsSpan(0, numSpatialDims);
            var padsSpan = pads.AsSpan(0, 2 * numSpatialDims);
            var dilationsSpan = dilations.AsSpan(0, numSpatialDims);
            ShapeInference.UpdatePadForConvAutoPadding(X.shape, W.shape, stridesSpan, dilationsSpan, autoPad, padsSpan);
            var shapeO = ShapeInference.Conv(X.shape, W.shape, group, stridesSpan, padsSpan, dilationsSpan);

            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shapeO, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;

            ctx.backend.Conv(X, W, B, O, group, stridesSpan, padsSpan, dilationsSpan, fusedActivation);
        }
    }

    /// <summary>
    /// Represents a `ConvTranspose` transpose convolution layer, which applies a convolution filter to an input tensor.
    /// </summary>
    [Operator(category = "Convolution")]
    [Inputs(names = new[] { "input", "kernel", "bias" })]
    partial class ConvTranspose : Layer
    {
        public AutoPad autoPad;
        public int[] dilations;
        public int group;
        public int[] outputPadding;
        public int[] pads;
        public int[] strides;
        public int[] kernelShape;
        public FusableActivation fusedActivation;

        internal static PartialTensor InferPartial(PartialTensor X, PartialTensor W, PartialTensor B, AutoPad autoPad, int[] dilations, int group, int[] outputPadding, int[] pads, int[] strides, int[] kernelShape, FusableActivation fusedActivation)
        {
            var shapeX = X.shape;
            var shapeKernel = W.shape;
            for (var i = 0; kernelShape != null && i < kernelShape.Length; i++)
            {
                shapeKernel[i + 2] = DynamicTensorDim.MaxDefinedDim(shapeKernel[i + 2], DynamicTensorDim.Int(kernelShape[i]));
            }

            if (!shapeX.hasRank)
                return new PartialTensor<float>();

            shapeKernel.DeclareRank(shapeX.rank);

            var shapeOut = DynamicTensorShape.Ones(shapeX.rank);

            shapeOut[0] = shapeX[0];
            shapeOut[1] = shapeKernel[1] * group;

            var shapeBias = B?.shape ?? DynamicTensorShape.DynamicRank;
            shapeBias.DeclareRank(1);
            shapeOut[1] = DynamicTensorDim.MaxDefinedDim(shapeOut[1], shapeBias[0]);

            for (var i = 2; i < shapeOut.rank; i++)
            {
                var stride = strides == null ? 1 : strides[i - 2];
                var pad = pads == null || autoPad != AutoPad.NotSet ? 0 : pads[i - 2] + pads[i - 2 + (shapeX.rank - 2)];
                var dilation = dilations == null ? 1 : dilations[i - 2];
                var outputPad = outputPadding == null ? 0 : outputPadding[i - 2];
                var dimX = shapeX[i];
                var dimKernel = shapeKernel[i];
                if (autoPad == AutoPad.NotSet)
                    shapeOut[i] = stride * (dimX - 1) + outputPad + (dimKernel - 1) * dilation + 1 - pad;
                else
                    shapeOut[i] = dimX * stride;

                // TODO interpret ONNX output_shape and handle it with the autopad
            }

            return new PartialTensor<float>(shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var W = ctx.storage.GetTensor(inputs[1]) as Tensor<float>;
            var B = ctx.storage.GetTensor(inputs[2]) as Tensor<float>;

            var numSpatialDims = X.shape.rank - 2;
            var stridesSpan = strides.AsSpan(0, numSpatialDims);
            var padsSpan = pads.AsSpan(0, 2 * numSpatialDims);
            var dilationsSpan = dilations.AsSpan(0, numSpatialDims);
            var outputPaddingSpan = outputPadding.AsSpan(0, numSpatialDims);

            ShapeInference.UpdatePadForConvTransAutoPadding(X.shape, W.shape, stridesSpan, dilationsSpan, autoPad, outputPaddingSpan, padsSpan);
            var shapeO = ShapeInference.ConvTranspose(X.shape, W.shape, group, stridesSpan, padsSpan, dilationsSpan, outputPaddingSpan);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shapeO, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.ConvTranspose(X, W, B, O, group, stridesSpan, padsSpan, dilationsSpan, outputPaddingSpan, fusedActivation);
        }
    }
}
