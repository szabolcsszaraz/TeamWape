using System;

namespace Unity.InferenceEngine
{
    public static partial class Functional
    {
        /// <summary>
        /// Returns the result of a 1D convolution of the `input` with the `weight` and `bias` tensors.
        /// </summary>
        /// <remarks>
        /// This operation applies a 1D convolution over the `input` tensor.
        /// The `input` must have rank `3` with shape `[batch, channels, width]`. The `weight` must have rank `3` with shape `[out_channels, in_channels/groups, kernel_width]`.
        /// The `bias` (if provided) must have rank `1` with shape `[out_channels]`.
        /// Promotes all tensors to float type if necessary.
        /// The `groups` parameter allows for grouped convolutions to divide input and output channels into independent groups.
        /// `dilation` controls spacing between kernel elements. The standard convolution is `dilation=1`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 3, 10), new float[30]); // Shape: [1, 3, 10]
        /// var weight = Functional.Constant(new TensorShape(16, 3, 3), new float[144]); // 16 filters, 3 input channels, kernel size 3
        /// var bias = Functional.Constant(new float[16]); // 16 output channels
        /// var result = Functional.Conv1D(input, weight, bias, stride: 1, padding: 1);
        /// // Result shape: [1, 16, 10] (with padding=1, output width preserved)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="weight">The weight tensor.</param>
        /// <param name="bias">The optional bias tensor.</param>
        /// <param name="stride">The stride value for each spatial dimension of the filter.</param>
        /// <param name="padding">The lower and upper padding values for each spatial dimension of the filter.</param>
        /// <param name="dilation">The dilation value of each spatial dimension of the filter.</param>
        /// <param name="groups">The number of groups to divide input channels and output channels into.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Conv1D(FunctionalTensor input, FunctionalTensor weight, FunctionalTensor bias, int stride = 1, int padding = 0, int dilation = 1, int groups = 1)
        {
            // TODO add auto padding
            DeclareRank(input, 3);
            DeclareRank(weight, 3);
            if (bias != null)
                DeclareRank(bias, 1);
            input = input.Float();
            weight = weight.Float();
            bias = bias?.Float();
            var strides = new[] { stride };
            var pads = new[] { padding, padding };
            var dilations = new[] { dilation };
            return FunctionalLayer.Conv(input, weight, bias, Layers.AutoPad.NotSet, dilations, groups, pads, strides, null, Layers.FusableActivation.None);
        }

        /// <summary>
        /// Returns the result of a 2D convolution of the `input` with the `weight` and `bias` tensors.
        /// </summary>
        /// <remarks>
        /// This operation applies a 2D convolution over the `input` tensor.
        /// The `input` must have rank `4` with shape `[batch, channels, height, width]`. The `weight` must have rank `4` with shape `[out_channels, in_channels/groups, kernel_height, kernel_width]`.
        /// The `bias` (if provided) must have rank `1` with shape `[out_channels]`.
        /// Promotes all tensors to float type if necessary.
        /// This is the standard convolution used in Convolutional Neural Networks (`CNN`) for image processing. `stride`, `padding`, `dilation`, and `groups` apply uniformly to both spatial dimensions.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 3, 32, 32), new float[3072]); // Shape: [1, 3, 32, 32]
        /// var weight = Functional.Constant(new TensorShape(64, 3, 3, 3), new float[1728]); // 64 filters, 3x3 kernel
        /// var bias = Functional.Constant(new float[64]);
        /// var result = Functional.Conv2D(input, weight, bias, stride: 1, padding: 1);
        /// // Result shape: [1, 64, 32, 32] (with padding=1, spatial dims preserved)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="weight">The weight tensor.</param>
        /// <param name="bias">The optional bias tensor.</param>
        /// <param name="stride">The stride value for each spatial dimension of the filter.</param>
        /// <param name="padding">The lower and upper padding values for each spatial dimension of the filter.</param>
        /// <param name="dilation">The dilation value of each spatial dimension of the filter.</param>
        /// <param name="groups">The number of groups to divide input channels and output channels into.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Conv2D(FunctionalTensor input, FunctionalTensor weight, FunctionalTensor bias, int stride = 1, int padding = 0, int dilation = 1, int groups = 1)
        {
            // TODO add auto padding
            DeclareRank(input, 4);
            DeclareRank(weight, 4);
            if (bias != null)
                DeclareRank(bias, 1);
            input = input.Float();
            weight = weight.Float();
            bias = bias?.Float();
            var strides = new[] { stride, stride };
            var pads = new[] { padding, padding, padding, padding };
            var dilations = new[] { dilation, dilation };
            return FunctionalLayer.Conv(input, weight, bias, Layers.AutoPad.NotSet, dilations, groups, pads, strides, null, Layers.FusableActivation.None);
        }

        /// <summary>
        /// Returns the result of a 2D convolution of the `input` with the `weight` and `bias` tensors.
        /// </summary>
        /// <remarks>
        /// This operation applies a 2D convolution over the `input` tensor with per-dimension control.
        /// The `input` must have rank `4` with shape `[batch, channels, height, width]`. The `weight` must have rank `4` with shape `[out_channels, in_channels/groups, kernel_height, kernel_width]`.
        /// This overload allows specifying different `stride`, `padding`, and `dilation` values for height and width dimensions using tuples.
        /// Promotes all tensors to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 3, 32, 64), new float[6144]); // Shape: [1, 3, 32, 64]
        /// var weight = Functional.Constant(new TensorShape(64, 3, 3, 3), new float[1728]);
        /// var bias = Functional.Constant(new float[64]);
        /// var result = Functional.Conv2D(input, weight, bias, stride: (1, 2), padding: (1, 1), dilation: (1, 1));
        /// // Result shape: [1, 64, 32, 32] (stride 2 in width dimension)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="weight">The weight tensor.</param>
        /// <param name="bias">The optional bias tensor.</param>
        /// <param name="stride">The stride value for each spatial dimension of the filter.</param>
        /// <param name="padding">The lower and upper padding values for each spatial dimension of the filter.</param>
        /// <param name="dilation">The dilation value of each spatial dimension of the filter.</param>
        /// <param name="groups">The number of groups to divide input channels and output channels into.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Conv2D(FunctionalTensor input, FunctionalTensor weight, FunctionalTensor bias, (int, int) stride, (int, int) padding, (int, int) dilation, int groups = 1)
        {
            // TODO add auto padding
            DeclareRank(input, 4);
            DeclareRank(weight, 4);
            if (bias != null)
                DeclareRank(bias, 1);
            var strideArray = new[] { stride.Item1, stride.Item2 };
            var paddingArray = new[] { padding.Item1, padding.Item2, padding.Item1, padding.Item2 };
            var dilationArray = new[] { dilation.Item1, dilation.Item2 };
            input = input.Float();
            weight = weight.Float();
            bias = bias?.Float();
            return FunctionalLayer.Conv(input, weight, bias, Layers.AutoPad.NotSet, dilationArray, groups, paddingArray, strideArray, null, Layers.FusableActivation.None);
        }

        /// <summary>
        /// Returns the result of a 3D convolution of the `input` with the `weight` and `bias` tensors.
        /// </summary>
        /// <remarks>
        /// This operation applies a 3D convolution over the `input` tensor.
        /// The `input` must have rank `5` with shape `[batch, channels, depth, height, width]`. The `weight` must have rank `5` with shape `[out_channels, in_channels/groups, kernel_depth, kernel_height, kernel_width]`.
        /// The `bias` (if provided) must have rank `1` with shape `[out_channels]`.
        /// Promotes all tensors to float type if necessary.
        /// 3D convolutions are used for video processing and volumetric data. `stride`, `padding`, `dilation`, and `groups` apply uniformly to all spatial dimensions.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 3, 16, 32, 32), new float[49152]); // Shape: [1, 3, 16, 32, 32]
        /// var weight = Functional.Constant(new TensorShape(64, 3, 3, 3, 3), new float[5184]); // 64 filters, 3x3x3 kernel
        /// var bias = Functional.Constant(new float[64]);
        /// var result = Functional.Conv3D(input, weight, bias, stride: 1, padding: 1);
        /// // Result shape: [1, 64, 16, 32, 32] (with padding=1, spatial dims preserved)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="weight">The weight tensor.</param>
        /// <param name="bias">The optional bias tensor.</param>
        /// <param name="stride">The stride value for each spatial dimension of the filter.</param>
        /// <param name="padding">The lower and upper padding values for each spatial dimension of the filter.</param>
        /// <param name="dilation">The dilation value of each spatial dimension of the filter.</param>
        /// <param name="groups">The number of groups to divide input channels and output channels into.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Conv3D(FunctionalTensor input, FunctionalTensor weight, FunctionalTensor bias, int stride = 1, int padding = 0, int dilation = 1, int groups = 1)
        {
            // TODO add auto padding
            DeclareRank(input, 5);
            DeclareRank(weight, 5);
            if (bias != null)
                DeclareRank(bias, 1);
            input = input.Float();
            weight = weight.Float();
            bias = bias?.Float();
            var strides = new[] { stride, stride, stride };
            var pads = new[] { padding, padding, padding, padding, padding, padding };
            var dilations = new[] { dilation, dilation, dilation };
            return FunctionalLayer.Conv(input, weight, bias, Layers.AutoPad.NotSet, dilations, groups, pads, strides, null, Layers.FusableActivation.None);
        }

        /// <summary>
        /// Returns the result of a 3D convolution of the `input` with the `weight` and `bias` tensors.
        /// </summary>
        /// <remarks>
        /// This operation applies a 3D convolution over the `input` tensor with per-dimension control.
        /// The `input` must have rank `5` with shape `[batch, channels, depth, height, width]`. The `weight` must have rank `5` with shape `[out_channels, in_channels/groups, kernel_depth, kernel_height, kernel_width]`.
        /// This overload allows specifying different `stride`, `padding`, and `dilation` values for each spatial dimension using tuples.
        /// Promotes all tensors to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 3, 16, 32, 32), new float[49152]); // Shape: [1, 3, 16, 32, 32]
        /// var weight = Functional.Constant(new TensorShape(64, 3, 3, 3, 3), new float[5184]);
        /// var bias = Functional.Constant(new float[64]);
        /// var result = Functional.Conv3D(input, weight, bias, stride: (2, 1, 1), padding: (1, 1, 1), dilation: (1, 1, 1));
        /// // Result shape: [1, 64, 8, 32, 32] (stride 2 in depth dimension)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="weight">The weight tensor.</param>
        /// <param name="bias">The optional bias tensor.</param>
        /// <param name="stride">The stride value for each spatial dimension of the filter.</param>
        /// <param name="padding">The lower and upper padding values for each spatial dimension of the filter.</param>
        /// <param name="dilation">The dilation value of each spatial dimension of the filter.</param>
        /// <param name="groups">The number of groups to divide input channels and output channels into.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Conv3D(FunctionalTensor input, FunctionalTensor weight, FunctionalTensor bias, (int, int, int) stride, (int, int, int) padding, (int, int, int) dilation, int groups = 1)
        {
            // TODO add auto padding
            DeclareRank(input, 5);
            DeclareRank(weight, 5);
            if (bias != null)
                DeclareRank(bias, 1);
            var strideArray = new[] { stride.Item1, stride.Item2, stride.Item3 };
            var paddingArray = new[] { padding.Item1, padding.Item2, padding.Item3, padding.Item1, padding.Item2, padding.Item3 };
            var dilationArray = new[] { dilation.Item1, dilation.Item2, dilation.Item3 };
            input = input.Float();
            weight = weight.Float();
            bias = bias?.Float();
            return FunctionalLayer.Conv(input, weight, bias, Layers.AutoPad.NotSet, dilationArray, groups, paddingArray, strideArray, null, Layers.FusableActivation.None);
        }

        /// <summary>
        /// Returns the result of a 1D transposed convolution of the `input` with the `weight` and `bias` tensors.
        /// </summary>
        /// <remarks>
        /// This operation applies a 1D transposed convolution over the `input` tensor.
        /// The `input` must have rank `3` with shape `[batch, channels, width]`. The `weight` must have rank `3` with shape `[in_channels, out_channels, kernel_width]`.
        /// The `bias` (if provided) must have rank `1` with shape `[out_channels]`.
        /// Promotes all tensors to float type if necessary.
        /// `outputPadding` adds additional size to the output dimension.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 16, 10), new float[160]); // Shape: [1, 16, 10]
        /// var weight = Functional.Constant(new TensorShape(16, 3, 3), new float[144]); // 16 input channels, 3 output channels, kernel size 3
        /// var bias = Functional.Constant(new float[3]);
        /// var result = Functional.ConvTranspose1D(input, weight, bias, stride: 2, padding: 1);
        /// // Result shape: [1, 3, 19] (upsampled by stride 2)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="weight">The weight tensor.</param>
        /// <param name="bias">The optional bias tensor.</param>
        /// <param name="stride">The stride value for each spatial dimension of the filter.</param>
        /// <param name="padding">The lower and upper padding values for each spatial dimension of the filter.</param>
        /// <param name="outputPadding">The output padding value for each spatial dimension in the filter.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ConvTranspose1D(FunctionalTensor input, FunctionalTensor weight, FunctionalTensor bias, int stride = 1, int padding = 0, int outputPadding = 0)
        {
            // TODO add auto padding
            // TODO support groups, dilation
            DeclareRank(input, 3);
            DeclareRank(weight, 3);
            if (bias != null)
                DeclareRank(bias, 1);
            input = input.Float();
            weight = weight.Float();
            bias = bias?.Float();
            var dilations = new[] { 1 };
            var strides = new[] { stride };
            var pads = new[] { padding, padding };
            var outputPaddings = new[] { outputPadding };
            return FunctionalLayer.ConvTranspose(input, weight, bias, Layers.AutoPad.NotSet, dilations, 1, outputPaddings, pads, strides, null, Layers.FusableActivation.None);
        }

        /// <summary>
        /// Returns the result of a 2D transposed convolution of the `input` with the `weight` and `bias` tensors.
        /// </summary>
        /// <remarks>
        /// This operation applies a 2D transposed convolution over the `input` tensor.
        /// The `input` must have rank `4` with shape `[batch, channels, height, width]`. The `weight` must have rank `4` with shape `[in_channels, out_channels, kernel_height, kernel_width]`.
        /// The `bias` (if provided) must have rank `1` with shape `[out_channels]`.
        /// Promotes all tensors to float type if necessary.
        /// `stride`, `padding`, and `outputPadding` apply uniformly to both spatial dimensions.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 64, 16, 16), new float[16384]); // Shape: [1, 64, 16, 16]
        /// var weight = Functional.Constant(new TensorShape(64, 32, 4, 4), new float[32768]); // 64 in channels, 32 out channels
        /// var bias = Functional.Constant(new float[32]);
        /// var result = Functional.ConvTranspose2D(input, weight, bias, stride: 2, padding: 1);
        /// // Result shape: [1, 32, 32, 32] (upsampled by stride 2)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="weight">The weight tensor.</param>
        /// <param name="bias">The optional bias tensor.</param>
        /// <param name="stride">The stride value for each spatial dimension of the filter.</param>
        /// <param name="padding">The lower and upper padding values for each spatial dimension of the filter.</param>
        /// <param name="outputPadding">The output padding value for each spatial dimension in the filter.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ConvTranspose2D(FunctionalTensor input, FunctionalTensor weight, FunctionalTensor bias, int stride = 1, int padding = 0, int outputPadding = 0)
        {
            // TODO add auto padding
            // TODO support groups, dilation
            DeclareRank(input, 4);
            DeclareRank(weight, 4);
            if (bias != null)
                DeclareRank(bias, 1);
            input = input.Float();
            weight = weight.Float();
            bias = bias?.Float();
            var dilations = new[] { 1, 1 };
            var strides = new[] { stride, stride };
            var pads = new[] { padding, padding, padding, padding };
            var outputPaddings = new[] { outputPadding, outputPadding };
            return FunctionalLayer.ConvTranspose(input, weight, bias, Layers.AutoPad.NotSet, dilations, 1, outputPaddings, pads, strides, null, Layers.FusableActivation.None);
        }

        /// <summary>
        /// Returns the result of a 2D transposed convolution of the `input` with the `weight` and `bias` tensors.
        /// </summary>
        /// <remarks>
        /// This operation applies a 2D transposed convolution over the `input` tensor with per-dimension control.
        /// The `input` must have rank `4` with shape `[batch, channels, height, width]`. The `weight` must have rank `4` with shape `[in_channels, out_channels, kernel_height, kernel_width]`.
        /// This overload allows specifying different `stride`, `padding`, and `outputPadding` values for height and width dimensions using tuples.
        /// Promotes all tensors to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 64, 16, 32), new float[32768]); // Shape: [1, 64, 16, 32]
        /// var weight = Functional.Constant(new TensorShape(64, 32, 4, 4), new float[32768]);
        /// var bias = Functional.Constant(new float[32]);
        /// var result = Functional.ConvTranspose2D(input, weight, bias, stride: (2, 2), padding: (1, 1), outputPadding: (0, 0));
        /// // Result shape: [1, 32, 32, 32] (stride 2 in height, 1 in width)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="weight">The weight tensor.</param>
        /// <param name="bias">The optional bias tensor.</param>
        /// <param name="stride">The stride value for each spatial dimension of the filter.</param>
        /// <param name="padding">The lower and upper padding values for each spatial dimension of the filter.</param>
        /// <param name="outputPadding">The output padding value for each spatial dimension in the filter.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ConvTranspose2D(FunctionalTensor input, FunctionalTensor weight, FunctionalTensor bias, (int, int) stride, (int, int) padding, (int, int) outputPadding)
        {
            // TODO add auto padding
            // TODO support groups, dilation
            DeclareRank(input, 4);
            DeclareRank(weight, 4);
            if (bias != null)
                DeclareRank(bias, 1);
            var dilations = new[] { 1, 1 };
            var strideArray = new[] { stride.Item1, stride.Item2 };
            var paddingArray = new[] { padding.Item1, padding.Item2, padding.Item1, padding.Item2 };
            var outputPaddingArray = new[] { outputPadding.Item1, outputPadding.Item2 };
            input = input.Float();
            weight = weight.Float();
            bias = bias?.Float();
            return FunctionalLayer.ConvTranspose(input, weight, bias, Layers.AutoPad.NotSet, dilations, 1, outputPaddingArray, paddingArray, strideArray, null, Layers.FusableActivation.None);
        }

        /// <summary>
        /// Returns the result of a 3D transposed convolution of the `input` with the `weight` and `bias` tensors.
        /// </summary>
        /// <remarks>
        /// This operation applies a 3D transposed convolution over the `input` tensor.
        /// The `input` must have rank `5` with shape `[batch, channels, depth, height, width]`. The `weight` must have rank `5` with shape `[in_channels, out_channels, kernel_depth, kernel_height, kernel_width]`.
        /// The `bias` (if provided) must have rank `1` with shape `[out_channels]`.
        /// Promotes all tensors to float type if necessary.
        /// 3D transposed convolutions are used for volumetric data upsampling and video generation.
        /// `stride`, `padding`, and `outputPadding` apply uniformly to all spatial dimensions.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 64, 8, 16, 16), new float[131072]); // Shape: [1, 64, 8, 16, 16]
        /// var weight = Functional.Constant(new TensorShape(64, 32, 4, 4, 4), new float[131072]); // 64 in channels, 32 out channels
        /// var bias = Functional.Constant(new float[32]);
        /// var result = Functional.ConvTranspose3D(input, weight, bias, stride: 2, padding: 1);
        /// // Result shape: [1, 32, 16, 32, 32] (upsampled by stride 2)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="weight">The weight tensor.</param>
        /// <param name="bias">The optional bias tensor.</param>
        /// <param name="stride">The stride value for each spatial dimension of the filter.</param>
        /// <param name="padding">The lower and upper padding values for each spatial dimension of the filter.</param>
        /// <param name="outputPadding">The output padding value for each spatial dimension in the filter.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ConvTranspose3D(FunctionalTensor input, FunctionalTensor weight, FunctionalTensor bias, int stride = 1, int padding = 0, int outputPadding = 0)
        {
            // TODO add auto padding
            // TODO support groups, dilation
            DeclareRank(input, 5);
            DeclareRank(weight, 5);
            if (bias != null)
                DeclareRank(bias, 1);
            input = input.Float();
            weight = weight.Float();
            bias = bias?.Float();
            var dilations = new[] { 1, 1, 1 };
            var strides = new[] { stride, stride, stride };
            var pads = new[] { padding, padding, padding, padding, padding, padding };
            var outputPaddings = new[] { outputPadding, outputPadding, outputPadding };
            return FunctionalLayer.ConvTranspose(input, weight, bias, Layers.AutoPad.NotSet, dilations, 1, outputPaddings, pads, strides, null, Layers.FusableActivation.None);
        }

        /// <summary>
        /// Returns the result of a 3D transposed convolution of the `input` with the `weight` and `bias` tensors.
        /// </summary>
        /// <remarks>
        /// This operation applies a 3D transposed convolution over the `input` tensor with per-dimension control.
        /// The `input` must have rank `5` with shape `[batch, channels, depth, height, width]`. The `weight` must have rank `5` with shape `[in_channels, out_channels, kernel_depth, kernel_height, kernel_width]`.
        /// This overload allows specifying different `stride`, `padding`, and `outputPadding` values for each spatial dimension using tuples.
        /// Promotes all tensors to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 64, 8, 16, 16), new float[131072]); // Shape: [1, 64, 8, 16, 16]
        /// var weight = Functional.Constant(new TensorShape(64, 32, 4, 4, 4), new float[131072]);
        /// var bias = Functional.Constant(new float[32]);
        /// var result = Functional.ConvTranspose3D(input, weight, bias, stride: (2, 2, 1), padding: (1, 1, 1), outputPadding: (0, 0, 0));
        /// // Result shape: [1, 32, 16, 32, 16] (stride 2 in depth and height, 1 in width)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="weight">The weight tensor.</param>
        /// <param name="bias">The optional bias tensor.</param>
        /// <param name="stride">The stride value for each spatial dimension of the filter.</param>
        /// <param name="padding">The lower and upper padding values for each spatial dimension of the filter.</param>
        /// <param name="outputPadding">The output padding value for each spatial dimension in the filter.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ConvTranspose3D(FunctionalTensor input, FunctionalTensor weight, FunctionalTensor bias, (int, int, int) stride, (int, int, int) padding, (int, int, int) outputPadding)
        {
            // TODO add auto padding
            // TODO support groups, dilation
            DeclareRank(input, 5);
            DeclareRank(weight, 5);
            if (bias != null)
                DeclareRank(bias, 1);
            var dilations = new[] { 1, 1, 1 };
            var strideArray = new[] { stride.Item1, stride.Item2, stride.Item3 };
            var paddingArray = new[] { padding.Item1, padding.Item2, padding.Item3, padding.Item1, padding.Item2, padding.Item3 };
            var outputPaddingArray = new[] { outputPadding.Item1, outputPadding.Item2, outputPadding.Item3 };
            input = input.Float();
            weight = weight.Float();
            bias = bias?.Float();
            return FunctionalLayer.ConvTranspose(input, weight, bias, Layers.AutoPad.NotSet, dilations, 1, outputPaddingArray, paddingArray, strideArray, null, Layers.FusableActivation.None);
        }
    }
}
