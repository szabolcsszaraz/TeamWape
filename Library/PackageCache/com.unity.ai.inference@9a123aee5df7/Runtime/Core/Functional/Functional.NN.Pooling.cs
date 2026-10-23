using System;

namespace Unity.InferenceEngine
{
    public static partial class Functional
    {
        /// <summary>
        /// Returns the result of a 1D average pooling of the `input`.
        /// </summary>
        /// <remarks>
        /// This operation applies 1D average pooling to the `input` tensor by computing the average of elements within a sliding window.
        /// The `input` tensor must have rank `3` with shape `[batch, channels, width]`.
        /// The kernel slides over the spatial dimension with the specified `stride`, and applies `padding` symmetrically at both ends.
        /// The default `stride` is the kernel size (non-overlapping windows).
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 1, 6), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f }); // Shape: [1, 1, 6]
        /// var result = Functional.AvgPool1D(input, kernelSize: 2, stride: 2);
        /// // Result shape: [1, 1, 3]
        /// // Values: [[[1.5, 3.5, 5.5]]] (averages of [1,2], [3,4], [5,6])
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="kernelSize">The size of the kernel.</param>
        /// <param name="stride">The optional stride of the pooling. The default value is the kernel size.</param>
        /// <param name="padding">The amount of padding on the spatial dimensions of the input.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor AvgPool1D(FunctionalTensor input, int kernelSize, int? stride = null, int padding = 0)
        {
            // TODO add auto padding, ceil_mode, count_include_pad
            DeclareRank(input, 3);
            input = input.Float();
            var s = stride ?? kernelSize;
            var kernelArray = new[] { kernelSize };
            var strideArray = new[] { s };
            var paddingArray = new[] { padding, padding };
            return FunctionalLayer.AveragePool(input, kernelArray, strideArray, paddingArray, Layers.AutoPad.NotSet);
        }

        /// <summary>
        /// Returns the result of a 2D average pooling of the `input`.
        /// </summary>
        /// <remarks>
        /// This operation applies 2D average pooling to the `input` tensor by computing the average of elements within a sliding 2D window.
        /// The `input` tensor must have rank `4` with shape `[batch, channels, height, width]`.
        /// The kernel slides over the spatial dimensions with the specified `stride`, and applies `padding` symmetrically on all sides.
        /// The default `stride` is the kernel size (non-overlapping windows).
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 1, 4, 4), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f, 13.0f, 14.0f, 15.0f, 16.0f }); // Shape: [1, 1, 4, 4]
        /// var result = Functional.AvgPool2D(input, kernelSize: 2, stride: 2);
        /// // Result shape: [1, 1, 2, 2]
        /// // Values: [[[[3.5, 5.5], [11.5, 13.5]]]] (average of each 2x2 block)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="kernelSize">The size of the kernel.</param>
        /// <param name="stride">The optional stride of the pooling. The default value is the kernel size.</param>
        /// <param name="padding">The amount of padding on the spatial dimensions of the input.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor AvgPool2D(FunctionalTensor input, int kernelSize, int? stride = null, int padding = 0)
        {
            // TODO add auto padding, ceil_mode, count_include_pad
            DeclareRank(input, 4);
            input = input.Float();
            var s = stride ?? kernelSize;
            var kernelArray = new[] { kernelSize, kernelSize };
            var strideArray = new[] { s, s };
            var paddingArray = new[] { padding, padding, padding, padding };
            return FunctionalLayer.AveragePool(input, kernelArray, strideArray, paddingArray, Layers.AutoPad.NotSet);
        }

        /// <summary>
        /// Returns the result of a 2D average pooling of the `input`.
        /// </summary>
        /// <remarks>
        /// This operation applies 2D average pooling to the `input` tensor by computing the average of elements within a sliding 2D window.
        /// The `input` tensor must have rank `4` with shape `[batch, channels, height, width]`.
        /// This overload allows specifying different `kernelSize`, `stride`, and `padding` for height and width dimensions using tuples.
        /// The default `stride` is the kernel size for each dimension (non-overlapping windows).
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 1, 3, 4), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f }); // Shape: [1, 1, 3, 4]
        /// var result = Functional.AvgPool2D(input, kernelSize: (2, 2), stride: (1, 2));
        /// // Result shape: [1, 1, 2, 2]
        /// // Values: [[[[3.5, 5.5], [7.5, 9.5]]]]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="kernelSize">The size of the kernel.</param>
        /// <param name="stride">The optional stride of the pooling. The default value is the kernel size.</param>
        /// <param name="padding">The amount of padding on the spatial dimensions of the input.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor AvgPool2D(FunctionalTensor input, (int, int) kernelSize, (int, int)? stride = null, (int, int)? padding = null)
        {
            // TODO add auto padding, ceil_mode, count_include_pad
            DeclareRank(input, 4);
            input = input.Float();
            var kernelArray = new[] { kernelSize.Item1, kernelSize.Item2 };
            var strideArray = new[] { stride?.Item1 ?? kernelSize.Item1, stride?.Item2 ?? kernelSize.Item2 };
            var paddingArray = new[] { padding?.Item1 ?? 0, padding?.Item2 ?? 0, padding?.Item1 ?? 0, padding?.Item2 ?? 0 };
            return FunctionalLayer.AveragePool(input, kernelArray, strideArray, paddingArray, Layers.AutoPad.NotSet);
        }

        /// <summary>
        /// Returns the result of a 3D average pooling of the `input`.
        /// </summary>
        /// <remarks>
        /// This operation applies 3D average pooling to the `input` tensor by computing the average of elements within a sliding 3D window.
        /// The `input` tensor must have rank `5` with shape `[batch, channels, depth, height, width]`.
        /// The kernel slides over the spatial dimensions with the specified `stride`, and applies `padding` uniformly on all sides.
        /// The default `stride` is the kernel size (non-overlapping windows).
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Create a 3D tensor with shape [1, 1, 2, 2, 2] (batch=1, channels=1, 2x2x2 volume)
        /// var input = Functional.Constant(new TensorShape(1, 1, 2, 2, 2), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f });
        /// var result = Functional.AvgPool3D(input, kernelSize: 2, stride: 2);
        /// // Result shape: [1, 1, 1, 1, 1]
        /// // Values: [[[[[4.5]]]]] (average of all 8 elements)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="kernelSize">The size of the kernel.</param>
        /// <param name="stride">The optional stride of the pooling. The default value is the kernel size.</param>
        /// <param name="padding">The amount of padding on the spatial dimensions of the input.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor AvgPool3D(FunctionalTensor input, int kernelSize, int? stride = null, int padding = 0)
        {
            // TODO add auto padding, ceil_mode, count_include_pad
            DeclareRank(input, 5);
            input = input.Float();
            var s = stride ?? kernelSize;
            var kernelArray = new[] { kernelSize, kernelSize, kernelSize };
            var strideArray = new[] { s, s, s };
            var paddingArray = new[] { padding, padding, padding, padding, padding, padding };
            return FunctionalLayer.AveragePool(input, kernelArray, strideArray, paddingArray, Layers.AutoPad.NotSet);
        }

        /// <summary>
        /// Returns the result of a 3D average pooling of the `input`.
        /// </summary>
        /// <remarks>
        /// This operation applies 3D average pooling to the `input` tensor by computing the average of elements within a sliding 3D window.
        /// The `input` tensor must have rank `5` with shape `[batch, channels, depth, height, width]`.
        /// This overload allows specifying different `kernelSize`, `stride`, and `padding` for each spatial dimension using tuples.
        /// The default `stride` is the kernel size for each dimension (non-overlapping windows).
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Create a 3D tensor with shape [1, 1, 2, 2, 4]
        /// var input = Functional.Constant(new TensorShape(1, 1, 2, 2, 4), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f, 13.0f, 14.0f, 15.0f, 16.0f });
        /// var result = Functional.AvgPool3D(input, kernelSize: (2, 2, 2), stride: (2, 2, 2));
        /// // Result shape: [1, 1, 1, 1, 2]
        /// // Values: [[[[[7.5, 9.5]]]]]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="kernelSize">The size of the kernel.</param>
        /// <param name="stride">The optional stride of the pooling. The default value is the kernel size.</param>
        /// <param name="padding">The amount of padding on the spatial dimensions of the input.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor AvgPool3D(FunctionalTensor input, (int, int, int) kernelSize, (int, int, int)? stride = null, (int, int, int)? padding = null)
        {
            // TODO add auto padding, ceil_mode, count_include_pad
            DeclareRank(input, 5);
            input = input.Float();
            var kernelArray = new[] { kernelSize.Item1, kernelSize.Item2, kernelSize.Item3 };
            var strideArray = new[] { stride?.Item1 ?? kernelSize.Item1, stride?.Item2 ?? kernelSize.Item2, stride?.Item3 ?? kernelSize.Item3 };
            var paddingArray = new[] { padding?.Item1 ?? 0, padding?.Item2 ?? 0, padding?.Item3 ?? 0, padding?.Item1 ?? 0, padding?.Item2 ?? 0, padding?.Item3 ?? 0 };
            return FunctionalLayer.AveragePool(input, kernelArray, strideArray, paddingArray, Layers.AutoPad.NotSet);
        }

        /// <summary>
        /// Returns the result of a 1D maximum pooling of the `input`.
        /// </summary>
        /// <remarks>
        /// This operation applies 1D max pooling to the `input` tensor by computing the maximum value within a sliding window.
        /// The `input` tensor must have rank `3` with shape `[batch, channels, width]`.
        /// The kernel slides over the spatial dimension with the specified `stride`, and applies `padding` symmetrically at both ends.
        /// The default `stride` is the kernel size (non-overlapping windows).
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 1, 6), new[] { 1.0f, 3.0f, 2.0f, 5.0f, 4.0f, 6.0f }); // Shape: [1, 1, 6]
        /// var result = Functional.MaxPool1D(input, kernelSize: 2, stride: 2);
        /// // Result shape: [1, 1, 3]
        /// // Values: [[[3.0, 5.0, 6.0]]] (max values of [1,3], [2,5], [4,6])
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="kernelSize">The size of the kernel.</param>
        /// <param name="stride">The optional stride of the pooling. The default value is the kernel size.</param>
        /// <param name="padding">The amount of padding on the spatial dimensions of the input.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor MaxPool1D(FunctionalTensor input, int kernelSize, int? stride = null, int padding = 0)
        {
            // TODO add auto padding, ceil_mode, count_include_pad
            DeclareRank(input, 3);
            input = input.Float();
            var s = stride ?? kernelSize;
            var kernelArray = new[] { kernelSize };
            var strideArray = new[] { s };
            var paddingArray = new[] { padding, padding };
            return FunctionalLayer.MaxPool(input, kernelArray, strideArray, paddingArray, Layers.AutoPad.NotSet);
        }

        /// <summary>
        /// Returns the result of a 2D maximum pooling of the `input`.
        /// </summary>
        /// <remarks>
        /// This operation applies 2D max pooling to the `input` tensor by computing the maximum value within a sliding 2D window.
        /// The `input` tensor must have rank `4` with shape `[batch, channels, height, width]`.
        /// The kernel slides over the spatial dimensions with the specified `stride`, and applies `padding` uniformly on all sides.
        /// The default `stride` is the kernel size (non-overlapping windows).
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 1, 4, 4), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f, 13.0f, 14.0f, 15.0f, 16.0f }); // Shape: [1, 1, 4, 4]
        /// var result = Functional.MaxPool2D(input, kernelSize: 2, stride: 2);
        /// // Result shape: [1, 1, 2, 2]
        /// // Values: [[[[6.0, 8.0], [14.0, 16.0]]]] (max of each 2x2 block)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="kernelSize">The size of the kernel.</param>
        /// <param name="stride">The optional stride of the pooling. The default value is the kernel size.</param>
        /// <param name="padding">The amount of padding on the spatial dimensions of the input.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor MaxPool2D(FunctionalTensor input, int kernelSize, int? stride = null, int padding = 0)
        {
            // TODO add auto padding, ceil_mode, count_include_pad
            DeclareRank(input, 4);
            input = input.Float();
            var s = stride ?? kernelSize;
            var kernelArray = new[] { kernelSize, kernelSize };
            var strideArray = new[] { s, s };
            var paddingArray = new[] { padding, padding, padding, padding };
            return FunctionalLayer.MaxPool(input, kernelArray, strideArray, paddingArray, Layers.AutoPad.NotSet);
        }

        /// <summary>
        /// Returns the result of a 2D maximum pooling of the `input`.
        /// </summary>
        /// <remarks>
        /// This operation applies 2D max pooling to the `input` tensor by computing the maximum value within a sliding 2D window.
        /// The `input` tensor must have rank `4` with shape `[batch, channels, height, width]`.
        /// This overload allows specifying different `kernelSize`, `stride`, and `padding` for height and width dimensions using tuples.
        /// The default `stride` is the kernel size for each dimension (non-overlapping windows).
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 1, 3, 4), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f }); // Shape: [1, 1, 3, 4]
        /// var result = Functional.MaxPool2D(input, kernelSize: (2, 2), stride: (1, 2));
        /// // Result shape: [1, 1, 2, 2]
        /// // Values: [[[[6, 8], [10, 12]]]]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="kernelSize">The size of the kernel.</param>
        /// <param name="stride">The optional stride of the pooling. The default value is the kernel size.</param>
        /// <param name="padding">The amount of padding on the spatial dimensions of the input.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor MaxPool2D(FunctionalTensor input, (int, int) kernelSize, (int, int)? stride = null, (int, int)? padding = null)
        {
            // TODO add auto padding, ceil_mode, count_include_pad
            DeclareRank(input, 4);
            input = input.Float();
            var kernelArray = new[] { kernelSize.Item1, kernelSize.Item2 };
            var strideArray = new[] { stride?.Item1 ?? kernelSize.Item1, stride?.Item2 ?? kernelSize.Item2 };
            var paddingArray = new[] { padding?.Item1 ?? 0, padding?.Item2 ?? 0, padding?.Item1 ?? 0, padding?.Item2 ?? 0 };
            return FunctionalLayer.MaxPool(input, kernelArray, strideArray, paddingArray, Layers.AutoPad.NotSet);
        }

        /// <summary>
        /// Returns the result of a 3D maximum pooling of the `input`.
        /// </summary>
        /// <remarks>
        /// This operation applies 3D max pooling to the `input` tensor by computing the maximum value within a sliding 3D window.
        /// The `input` tensor must have rank `5` with shape `[batch, channels, depth, height, width]`.
        /// The kernel slides over the spatial dimensions with the specified `stride`, and applies `padding` uniformly on all sides.
        /// The default `stride` is the kernel size (non-overlapping windows).
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Create a 3D tensor with shape [1, 1, 2, 2, 2] (batch=1, channels=1, 2x2x2 volume)
        /// var input = Functional.Constant(new TensorShape(1, 1, 2, 2, 2), new[] { 1.0f, 2.0f,3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f });
        /// var result = Functional.MaxPool3D(input, kernelSize: 2, stride: 2);
        /// // Result shape: [1, 1, 1, 1, 1]
        /// // Values: [[[[[8.0]]]]] (max of all 8 elements)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="kernelSize">The size of the kernel.</param>
        /// <param name="stride">The optional stride of the pooling. The default value is the kernel size.</param>
        /// <param name="padding">The amount of padding on the spatial dimensions of the input.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor MaxPool3D(FunctionalTensor input, int kernelSize, int? stride = null, int padding = 0)
        {
            // TODO add auto padding, ceil_mode, count_include_pad
            DeclareRank(input, 5);
            input = input.Float();
            var s = stride ?? kernelSize;
            var kernelArray = new[] { kernelSize, kernelSize, kernelSize };
            var strideArray = new[] { s, s, s };
            var paddingArray = new[] { padding, padding, padding, padding, padding, padding };
            return FunctionalLayer.MaxPool(input, kernelArray, strideArray, paddingArray, Layers.AutoPad.NotSet);
        }

        /// <summary>
        /// Returns the result of a 3D maximum pooling of the `input`.
        /// </summary>
        /// <remarks>
        /// This operation applies 3D max pooling to the `input` tensor by computing the maximum value within a sliding 3D window.
        /// The `input` tensor must have rank `5` with shape `[batch, channels, depth, height, width]`.
        /// This overload allows specifying different `kernelSize`, `stride`, and `padding` for each spatial dimension using tuples.
        /// The default `stride` is the kernel size for each dimension (non-overlapping windows).
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Create a 3D tensor with shape [1, 1, 2, 2, 4]
        /// var input = Functional.Constant(new TensorShape(1, 1, 2, 2, 4), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f, 13.0f, 14.0f, 15.0f, 16.0f });
        /// var result = Functional.MaxPool3D(input, kernelSize: (2, 2, 2), stride: (2, 2, 2));
        /// // Result shape: [1, 1, 1, 1, 2]
        /// // Values: [[[[[14, 16]]]]]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="kernelSize">The size of the kernel.</param>
        /// <param name="stride">The optional stride of the pooling. The default value is the kernel size.</param>
        /// <param name="padding">The amount of padding on the spatial dimensions of the input.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor MaxPool3D(FunctionalTensor input, (int, int, int) kernelSize, (int, int, int)? stride = null, (int, int, int)? padding = null)
        {
            // TODO add auto padding, ceil_mode, count_include_pad
            DeclareRank(input, 5);
            input = input.Float();
            var strideArray = new[] { stride?.Item1 ?? kernelSize.Item1, stride?.Item2 ?? kernelSize.Item2, stride?.Item3 ?? kernelSize.Item3 };
            var paddingArray = new[] { padding?.Item1 ?? 0, padding?.Item2 ?? 0, padding?.Item3 ?? 0, padding?.Item1 ?? 0, padding?.Item2 ?? 0, padding?.Item3 ?? 0 };
            var kernelArray = new[] { kernelSize.Item1, kernelSize.Item2, kernelSize.Item3 };
            return FunctionalLayer.MaxPool(input, kernelArray, strideArray, paddingArray, Layers.AutoPad.NotSet);
        }
    }
}
