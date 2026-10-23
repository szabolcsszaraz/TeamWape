
namespace Unity.InferenceEngine
{
    public static partial class Functional
    {
        /// <summary>
        /// Returns the `input` tensors concatenated along a dimension.
        /// </summary>
        /// <remarks>
        /// This operator concatenates a sequence of tensors along an existing dimension.
        /// All tensors must have the same shape except in the concatenation dimension.
        /// The output tensor has the same rank as the `input` tensors, with the concatenation dimension size equal to the sum of the `input` sizes.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new TensorShape(2, 3), new[] { 1, 2, 3, 4, 5, 6 });
        /// var b = Functional.Constant(new TensorShape(2, 2), new[] { 7, 8, 9, 10 });
        /// var result = Functional.Concat(new[] { a, b }, dim: 1);
        /// // Result shape: [2, 5] (concatenated along dimension 1)
        /// ]]></code>
        /// </example>
        /// <param name="tensors">The input tensors.</param>
        /// <param name="dim">The dimension along which to concatenate.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Concat(FunctionalTensor[] tensors, int dim = 0)
        {
            return FunctionalLayer.Concat(tensors, dim);
        }

        /// <summary>
        /// Returns the `input` tensor gathered along a dimension with `index` values.
        /// </summary>
        /// <remarks>
        /// This operator gathers values from the `input` tensor along a specified dimension using an index tensor.
        /// For each element in the `index` tensor, the corresponding value is gathered from the `input` at that index position.
        /// The output has the same shape as the `index` tensor.
        /// The `index` tensor must be integer type and have values within valid range for the specified dimension.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 10, 20, 30, 40 });
        /// var index = Functional.Constant(new[] { 0, 2, 1, 3 });
        /// var result = Functional.Gather(input, dim: 0, index);
        /// // Result: [10, 30, 20, 40] (values gathered at specified indices)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension along which to gather.</param>
        /// <param name="index">The indices tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Gather(this FunctionalTensor input, int dim, FunctionalTensor index)
        {
            DeclareType(DataType.Int, index);
            return FunctionalLayer.GatherElements(input, index, dim);
        }

        /// <summary>
        /// Returns the `input` tensor indexed along a dimension with entries in a 1D index tensor.
        /// </summary>
        /// <remarks>
        /// This operator selects elements from the `input` tensor along a specified dimension using a 1D index tensor.
        /// Unlike <see cref="Gather"/>, `IndexSelect` always uses a 1D index tensor and the output preserves the `input` shape except along the selection dimension.
        /// The `index` tensor must be integer type and contain valid indices for the specified dimension.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(3, 4), new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 });
        /// var index = Functional.Constant(new[] { 0, 2 }); // Select rows 0 and 2
        /// var result = Functional.IndexSelect(input, dim: 0, index);
        /// // Result shape: [2, 4] (selected rows)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension along which to select.</param>
        /// <param name="index">The indices tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor IndexSelect(this FunctionalTensor input, int dim, FunctionalTensor index)
        {
            DeclareType(DataType.Int, index);
            return FunctionalLayer.Gather(input, index, dim);
        }

        /// <summary>
        /// Returns the `input` tensor with a dimension moved from `source` to `destination`.
        /// </summary>
        /// <remarks>
        /// This operator moves a single dimension of the `input` tensor from one position to another.
        /// This operator doesn't copy the data. It only changes the dimension order.
        /// All other dimensions maintain their relative order in the output tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3, 4), new float[24]); // Shape: [2, 3, 4]
        /// var result = Functional.MoveDim(input, source: 1, destination: 0);
        /// // Result shape: [3, 2, 4] (dimension 1 moved to position 0)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="source">The dimension in the `input` tensor to move.</param>
        /// <param name="destination">The moved dimension in the output tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor MoveDim(this FunctionalTensor input, int source, int destination)
        {
            return MoveDim(input, new[] { source }, new[] { destination });
        }

        /// <summary>
        /// Returns the `input` tensor with multiple dimensions moved from `source` to `destination`.
        /// </summary>
        /// <remarks>
        /// This operator moves multiple dimensions of the `input` tensor from source positions to destination positions.
        /// The `source` and `destination` arrays must have the same length.
        /// This operation doesn't copy the data. It only changes the dimension order.
        /// Dimensions not specified in `source` maintain their relative order in the output.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3, 4, 5), new float[120]); // Shape: [2, 3, 4, 5]
        /// var result = Functional.MoveDim(input, source: new[] { 0, 2 }, destination: new[] { 2, 0 });
        /// // Result shape: [4, 3, 2, 5] (dimensions 0 and 2 swapped)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="source">The dimensions in the `input` tensor to move.</param>
        /// <param name="destination">The moved dimensions in the output tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor MoveDim(this FunctionalTensor input, int[] source, int[] destination)
        {
            return FunctionalLayer.MoveDim(input, source, destination);
        }

        /// <summary>
        /// Returns the `input` tensor narrowed along a dimension.
        /// </summary>
        /// <remarks>
        /// This operator returns a new tensor that is a narrowed version of the `input` tensor along a specified dimension.
        /// The narrowing starts at the given `start` index and continues for `length` elements.
        /// All other dimensions remain unchanged.
        /// Equivalent to slicing the tensor along a single dimension.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }); // Shape: [8]
        /// var result = Functional.Narrow(input, dim: 0, start: 2, length: 4);
        /// // Result: [3, 4, 5, 6] (elements from index 2 to 5)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension along which to narrow.</param>
        /// <param name="start">The start index along the dimension.</param>
        /// <param name="length">The number of elements along the dimension.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Narrow(this FunctionalTensor input, int dim, int start, int length)
        {
            return FunctionalLayer.Narrow(input, Constant(dim), Constant(start), Constant(length));
        }

        /// <summary>
        /// Returns the `input` tensor narrowed along a dimension.
        /// </summary>
        /// <remarks>
        /// This operator returns a new tensor that is a narrowed version of the `input` tensor along a specified dimension.
        /// The narrowing starts at the given `start` index and continues for `length` elements.
        /// This overload accepts functional tensors for `start` and `length`, allowing dynamic slicing determined at runtime.
        /// The `start` and `length` tensors must be integer type scalars.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }); // Shape: [8]
        /// var start = Functional.Constant(2);
        /// var length = Functional.Constant(4);
        /// var result = Functional.Narrow(input, dim: 0, start, length);
        /// // Result: [3, 4, 5, 6] (dynamically determined slice)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension along which to narrow.</param>
        /// <param name="start">The functional start index along the dimension.</param>
        /// <param name="length">The functional number of elements along the dimension.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Narrow(this FunctionalTensor input, int dim, FunctionalTensor start, FunctionalTensor length)
        {
            DeclareType(DataType.Int, start, length);
            return FunctionalLayer.Narrow(input, Constant(dim), start, length);
        }

        /// <summary>
        /// Returns the indices of the `input` tensor with values not equal to zero.
        /// </summary>
        /// <remarks>
        /// This operator returns a tensor containing the indices of all non-zero elements in the `input` tensor.
        /// The output has shape `[N, rank]` where `N` is the number of non-zero elements and `rank` is the number of dimensions.
        /// Each row in the output represents the multi-dimensional index of a non-zero element.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 0, 2, 0, 4, 5, 0 });
        /// var result = Functional.NonZero(input);
        /// // Result: [[1], [3], [4]] (indices of non-zero values 2, 4, 5)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor NonZero(FunctionalTensor input)
        {
            // TODO support asTuple
            return Transpose(FunctionalLayer.NonZero(input), 0, 1);
        }

        /// <summary>
        /// Returns the `input` tensor padded with size determined by the `pad` array and values determined by the `mode`.
        /// </summary>
        /// <remarks>
        /// This operator pads the `input` tensor along specified dimensions according to the given `mode`.
        /// The `pad` array specifies padding amounts starting from the last dimension: `[pad_w_lower, pad_w_upper, pad_h_lower, pad_h_upper, ...]`.
        /// Available modes: `constant` (zero padding), `reflect` (reflect values at boundaries), `replicate` (repeat edge values), `circular` (wrap around).
        /// For constant padding with non-zero values, use the other `Pad` overloads.
        /// Not all dimensions need to be padded; unspecified dimensions remain unchanged.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 2, 3), new[] { 1, 2, 3, 4, 5, 6 });
        /// var result = Functional.Pad(input, pad: new[] { 1, 1, 1, 1 }, mode: "constant");
        /// // Pads last two dimensions by 1 on each side with zeros
        /// // Result shape: [1, 4, 5]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="pad">The padding lower and upper sizes starting from the final dimension (`pad_w_lower`, `pad_w_upper`, `pad_h_lower`, `pad_h_upper`, ...), not all dimensions need to be padded.</param>
        /// <param name="mode">The mode to use for sampling values, should be `constant`, `reflect`, `replicate` or `circular`, for constant padding with non zero values use one of the other `Pad` methods.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Pad(this FunctionalTensor input, int[] pad, string mode)
        {
            var padMode = mode switch
            {
                "constant" => Layers.PadMode.Constant,
                "reflect" => Layers.PadMode.Reflect,
                "replicate" => Layers.PadMode.Edge,
                "circular" => Layers.PadMode.Wrap,
                _ => Layers.PadMode.Constant
            };
            var axes = new int[pad.Length / 2];
            var pads = new int[pad.Length];
            for (var i = 0; i < axes.Length; i++)
            {
                axes[i] = -i - 1;
                pads[i] = pad[2 * i];
                pads[i + axes.Length] = pad[2 * i + 1];
            }
            return FunctionalLayer.Pad(input, Constant(pads), null, Constant(axes), padMode);
        }

        /// <summary>
        /// Returns the `input` tensor padded with size determined by the `pad` array and a constant value.
        /// </summary>
        /// <remarks>
        /// This operator pads the `input` tensor with a specified integer constant value.
        /// The `pad` array specifies padding amounts starting from the last dimension: `[pad_w_lower, pad_w_upper, pad_h_lower, pad_h_upper, ...]`.
        /// Fills all padded regions with the specified constant `value`.
        /// If the `input` is a float tensor, the `value` is automatically converted to float.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1, 2, 3 });
        /// var result = Functional.Pad(input, pad: new[] { 1, 2 }, value: -1);
        /// // Result: [-1, 1, 2, 3, -1, -1] (padded with -1)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="pad">The padding lower and upper sizes starting from the final dimension (`pad_w_lower`, `pad_w_upper`, `pad_h_lower`, `pad_h_upper`, ...), not all dimensions need to be padded.</param>
        /// <param name="value">The constant value to use for padding.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Pad(this FunctionalTensor input, int[] pad, int value)
        {
            if (input.dataType == DataType.Float)
                return Pad(input, pad, (float)value);
            var axes = new int[pad.Length / 2];
            var pads = new int[pad.Length];
            for (var i = 0; i < axes.Length; i++)
            {
                axes[i] = -i - 1;
                pads[i] = pad[2 * i];
                pads[i + axes.Length] = pad[2 * i + 1];
            }
            return FunctionalLayer.Pad(input, Constant(pads), Constant(value), Constant(axes), Layers.PadMode.Constant);
        }

        /// <summary>
        /// Returns the `input` tensor padded with size determined by the `pad` array and a constant value.
        /// </summary>
        /// <remarks>
        /// This operator pads the `input` tensor with a specified float constant `value`.
        /// The `pad` array specifies padding amounts starting from the last dimension: `[pad_w_lower, pad_w_upper, pad_h_lower, pad_h_upper, ...]`.
        /// Fills all padded regions with the specified constant `value`.
        /// The `input` tensor must be float type.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, 2.0f, 3.0f });
        /// var result = Functional.Pad(input, pad: new[] { 1, 2 }, value: -1.5f);
        /// // Result: [-1.5, 1.0, 2.0, 3.0, -1.5, -1.5] (padded with -1.5)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="pad">The padding lower and upper sizes starting from the final dimension (`pad_w_lower`, `pad_w_upper`, `pad_h_lower`, `pad_h_upper`, ...), not all dimensions need to be padded.</param>
        /// <param name="value">The constant value to use for padding.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Pad(this FunctionalTensor input, int[] pad, float value)
        {
            DeclareType(DataType.Float, input);
            var axes = new int[pad.Length / 2];
            var pads = new int[pad.Length];
            for (var i = 0; i < axes.Length; i++)
            {
                axes[i] = -i - 1;
                pads[i] = pad[2 * i];
                pads[i + axes.Length] = pad[2 * i + 1];
            }
            return FunctionalLayer.Pad(input, Constant(pads), Constant(value), Constant(axes), Layers.PadMode.Constant);
        }

        /// <summary>
        /// Returns the `input` tensor with permuted dimensions.
        /// </summary>
        /// <remarks>
        /// This operator reorders the dimensions of the `input` tensor according to the specified permutation.
        /// The `dims` array specifies the new order of dimensions, where `dims[i]` indicates which input dimension becomes output dimension `i`.
        /// All dimensions must be included exactly once in the permutation.
        /// This operator doesn't copy the data. It only changes the dimension order.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3, 4), new float[24]); // Shape: [2, 3, 4]
        /// var result = Functional.Permute(input, dims: new[] { 2, 0, 1 });
        /// // Result shape: [4, 2, 3] (dimensions reordered)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dims">The dimensions of the `input` tensor to use in the permuted output tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Permute(this FunctionalTensor input, int[] dims)
        {
            return FunctionalLayer.Transpose(input, dims);
        }

        /// <summary>
        /// Returns the `input` tensor elements reshaped.
        /// </summary>
        /// <remarks>
        /// This operator returns a tensor with the same data as the `input` but with a new shape.
        /// The total number of elements must remain the same.
        /// One dimension can be specified as `-1`, which will be automatically inferred from the other dimensions and total element count.
        /// The elements are read in row-major (C-contiguous) order and placed in the new shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1, 2, 3, 4, 5, 6 }); // Shape: [6]
        /// var result = Functional.Reshape(input, shape: new[] { 2, 3 });
        /// // Result shape: [2, 3] with same 6 elements
        ///
        /// var result2 = Functional.Reshape(input, shape: new[] { 3, -1 });
        /// // Result shape: [3, 2] (second dimension inferred)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="shape">The shape of the output tensor. A negative value is inferred from the others.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Reshape(this FunctionalTensor input, int[] shape)
        {
            return FunctionalLayer.Reshape(input, Constant(shape), false);
        }

        /// <summary>
        /// Returns the `input` tensor sliced along a dimension at an index.
        /// </summary>
        /// <remarks>
        /// This operator selects a single slice from the `input` tensor along a specified dimension at a given index.
        /// The selected dimension is removed from the output, reducing the rank by `1`.
        /// For example, selecting from a `[2, 3, 4]` tensor along dimension `1` at index `0` produces a `[2, 4]` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1, 2, 3, 4, 5, 6 });
        /// var result = Functional.Select(input, dim: 1, index: 1);
        /// // Result: [2, 5] (selects column 1 from each row)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension along which to select.</param>
        /// <param name="index">The index along the dimension to select.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Select(this FunctionalTensor input, int dim, int index)
        {
            return FunctionalLayer.Select(input, Constant(dim), Constant(index));
        }

        /// <summary>
        /// Returns the `input` tensor sliced along a dimension at an index.
        /// </summary>
        /// <remarks>
        /// This operator selects a single slice from the `input` tensor along a specified dimension at a given index.
        /// This overload accepts a functional tensor for the `index`, allowing dynamic selection determined at runtime.
        /// The selected dimension is removed from the output, reducing the rank by `1`.
        /// The `index` tensor must be integer type and a scalar.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1, 2, 3, 4, 5, 6 });
        /// var index = Functional.Constant(1);
        /// var result = Functional.Select(input, dim: 1, index);
        /// // Result: [2, 5] (dynamically selects column 1)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension along which to select.</param>
        /// <param name="index">The functional index along the dimension to select.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Select(this FunctionalTensor input, int dim, FunctionalTensor index)
        {
            DeclareType(DataType.Int, index);
            return FunctionalLayer.Select(input, Constant(dim), index);
        }

        /// <summary>
        /// Returns a copy of the `input` with the elements replaced by those from `src` given by the `index` along a dimension.
        /// </summary>
        /// <remarks>
        /// This operator creates a copy of the `input` tensor with specified elements replaced by values from a source tensor.
        /// The `index` tensor determines which elements to replace along the specified dimension.
        /// For each element in the `src` tensor, the corresponding `index` value determines where to write it in the output.
        /// The `index` tensor must be integer type and have the same shape as the `src` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1, 2, 3, 4, 5 });
        /// var index = Functional.Constant(new[] { 0, 2, 4 });
        /// var src = Functional.Constant(new[] { 10, 20, 30 });
        /// var result = Functional.Scatter(input, dim: 0, index, src);
        /// // Result: [10, 2, 20, 4, 30] (values at indices 0, 2, 4 replaced)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension along which to scatter.</param>
        /// <param name="index">The index tensor.</param>
        /// <param name="src">The source tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Scatter(FunctionalTensor input, int dim, FunctionalTensor index, FunctionalTensor src)
        {
            // TODO add reduction
            DeclareType(DataType.Int, index);
            return FunctionalLayer.ScatterElements(input, index, src, dim, Layers.ScatterReductionMode.None);
        }

        /// <summary>
        /// Returns a copy of the `input` with the elements replaced by those from `src` at a dimension and index.
        /// </summary>
        /// <remarks>
        /// This operator creates a copy of the `input` tensor with elements at a specific slice replaced by values from a source tensor.
        /// The `src` tensor is inserted at the specified `index` along the given dimension.
        /// The `src` tensor must have rank one less than the `input`, or have size `1` along the insertion dimension.
        /// This is the inverse operation of <see cref="Select"/> - it puts values back into a tensor at a specific slice.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1, 2, 3, 4, 5, 6 });
        /// var src = Functional.Constant(new[] { 10, 20 });
        /// var result = Functional.SelectScatter(input, src, dim: 1, index: 1);
        /// // Result: [[1, 10, 3], [4, 20, 6]] (column 1 replaced)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="src">The source tensor.</param>
        /// <param name="dim">The dimension along which to scatter.</param>
        /// <param name="index">The index at which to scatter along the dimension.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor SelectScatter(FunctionalTensor input, FunctionalTensor src, int dim, int index)
        {
            return FunctionalLayer.SliceSet(input, Unsqueeze(src, dim), Constant(new[] { index }), Constant(new[] { index + 1 }), Constant(new[] { dim }), null);
        }

        /// <summary>
        /// Returns a copy of the `input` with the elements replaced by those from `src` along a dimension with `start`, `end` and `step`.
        /// </summary>
        /// <remarks>
        /// This operator creates a copy of the `input` tensor with a slice of elements replaced by values from a source tensor.
        /// The replacement starts at the `start` index, ends before the `end` index, and proceeds with the given `step` size along the specified dimension.
        /// The `src` tensor must match the shape of the slice being replaced.
        /// Setting `end` to `int.MaxValue` replaces until the end of the dimension.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 0, 0, 0, 0, 0 });
        /// var src = Functional.Constant(new[] { 10, 20 });
        /// var result = Functional.SliceScatter(input, src, dim: 0, start: 1, end: 5, step: 2);
        /// // Result: [0, 10, 0, 20, 0] (elements at indices 1 and 3 replaced)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="src">The source tensor.</param>
        /// <param name="dim">The dimension along which to scatter.</param>
        /// <param name="start">The index of the first element to replace along the dimension.</param>
        /// <param name="end">The end index of the scatter along the dimension.</param>
        /// <param name="step">The step between the indices along the dimension.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor SliceScatter(FunctionalTensor input, FunctionalTensor src, int dim = 0, int start = 0, int end = int.MaxValue, int step = 1)
        {
            return FunctionalLayer.SliceSet(input, src, Constant(new[] { start }), Constant(new[] { end }), Constant(new[] { dim }), Constant(new[] { step }));
        }

        /// <summary>
        /// Returns a copy of the `input` with the elements updated by adding by those from `src` given by the `index` along a dimension.
        /// </summary>
        /// <remarks>
        /// This operator creates a copy of the `input` tensor with specified elements incremented by values from a source tensor.
        /// Unlike <see cref="Scatter"/> which replaces values, `ScatterAdd` adds the source values to the existing values at the indexed positions.
        /// The `index` tensor determines which elements to update along the specified dimension.
        /// The `index` tensor must be integer type and have the same shape as the `src` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1, 2, 3, 4, 5 });
        /// var index = Functional.Constant(new[] { 0, 2, 4 });
        /// var src = Functional.Constant(new[] { 10, 20, 30 });
        /// var result = Functional.ScatterAdd(input, dim: 0, index, src);
        /// // Result: [11, 2, 23, 4, 35] (values at indices 0, 2, 4 incremented)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension along which to scatter.</param>
        /// <param name="index">The index tensor.</param>
        /// <param name="src">The source tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ScatterAdd(FunctionalTensor input, int dim, FunctionalTensor index, FunctionalTensor src)
        {
            return FunctionalLayer.ScatterElements(input, index, src, dim, Layers.ScatterReductionMode.Add);
        }

        /// <summary>
        /// Returns an array of tensors by splitting the `input` into sections along a dimension.
        /// </summary>
        /// <remarks>
        /// This operator splits the `input` tensor into multiple tensors along a specified dimension.
        /// The `sections` array specifies the size of each output tensor along the split dimension.
        /// The sum of section sizes must equal the size of the `input` along the split dimension.
        /// All other dimensions remain unchanged in the output tensors.
        /// This is the inverse operation of <see cref="Concat"/>.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1, 2, 3, 4, 5, 6 }); // Shape: [6]
        /// var result = Functional.Split(input, sections: new[] { 2, 3, 1 }, dim: 0);
        /// // result[0]: [1, 2] (length 2)
        /// // result[1]: [3, 4, 5] (length 3)
        /// // result[2]: [6] (length 1)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="sections">The length of each section along the dimension.</param>
        /// <param name="dim">The dimension along which to split.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor[] Split(this FunctionalTensor input, int[] sections, int dim = 0)
        {
            var dataTypes = new DataType[sections.Length];
            for (var i = 0; i < dataTypes.Length; i++)
                dataTypes[i] = input.dataType;
            return FunctionalLayer.Split(input, Constant(sections), dim, sections.Length);
        }

        /// <summary>
        /// Returns the `input` tensor with all dimensions of size `1` removed.
        /// </summary>
        /// <remarks>
        /// This operator removes all dimensions of size `1` from the `input` tensor.
        /// The resulting tensor has the same data but with a reduced rank.
        /// For example, a tensor of shape `[1, 3, 1, 5]` becomes `[3, 5]` after squeezing.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 3, 1, 4), new float[12]); // Shape: [1, 3, 1, 4]
        /// var result = Functional.Squeeze(input);
        /// // Result shape: [3, 4] (singleton dimensions removed)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Squeeze(this FunctionalTensor input)
        {
            return FunctionalLayer.Squeeze(input, null);
        }

        /// <summary>
        /// Returns the `input` tensor with all specified dimensions of size `1` removed.
        /// </summary>
        /// <remarks>
        /// This operator removes only the specified dimensions of size `1` from the `input` tensor.
        /// Each specified dimension must have size `1`, otherwise an error occurs.
        /// The resulting tensor has the same data but with a reduced rank.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 3, 1, 4), new float[12]); // Shape: [1, 3, 1, 4]
        /// var result = Functional.Squeeze(input, dim: new[] { 0, 2 });
        /// // Result shape: [3, 4] (dimensions 0 and 2 removed)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimensions of size 1 to remove.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Squeeze(this FunctionalTensor input, int[] dim)
        {
            return FunctionalLayer.Squeeze(input, Constant(dim));
        }

        /// <summary>
        /// Returns the `input` tensors concatenated along a new dimension.
        /// </summary>
        /// <remarks>
        /// This operator stacks a sequence of tensors along a new dimension.
        /// All input tensors must have the same shape.
        /// The output rank is one greater than the `input` tensors, with the new dimension inserted at the specified position.
        /// Unlike <see cref="Concat"/> which joins along an existing dimension, `Stack` creates a new dimension.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 1, 2, 3 }); // Shape: [3]
        /// var b = Functional.Constant(new[] { 4, 5, 6 }); // Shape: [3]
        /// var result = Functional.Stack(new[] { a, b }, dim: 0);
        /// // Result shape: [2, 3], values: [[1, 2, 3], [4, 5, 6]]
        /// ]]></code>
        /// </example>
        /// <param name="tensors">The input tensors.</param>
        /// <param name="dim">The dimension along which to stack.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Stack(FunctionalTensor[] tensors, int dim = 0)
        {
            // TODO add properly
            var unsqueezedTensors = new FunctionalTensor[tensors.Length];
            for (var i = 0; i < unsqueezedTensors.Length; i++)
                unsqueezedTensors[i] = Unsqueeze(tensors[i], dim);
            return Concat(unsqueezedTensors, dim);
        }

        /// <summary>
        /// Returns a tensor with the elements of `input` at `index` positions.
        /// </summary>
        /// <remarks>
        /// This operator treats the `input` tensor as a flattened 1D array and returns elements at specified `index` positions.
        /// The `input` is first raveled (flattened) into a 1D tensor, then elements are gathered using the `index` tensor.
        /// The `index` tensor must be integer type and contain valid indices for the flattened `input`.
        /// The output has the same shape as the `index` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1, 2, 3, 4, 5, 6 });
        /// var index = Functional.Constant(new[] { 0, 3, 5 });
        /// var result = Functional.Take(input, index);
        /// // Result: [1, 4, 6] (elements at flattened indices 0, 3, 5)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="index">The index tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Take(this FunctionalTensor input, FunctionalTensor index)
        {
            return Gather(Ravel(input), 0, index);
        }

        /// <summary>
        /// Returns the `input` tensor repeated on the `dims`.
        /// </summary>
        /// <remarks>
        /// This operator constructs a tensor by repeating the `input` tensor a specified number of times along each dimension.
        /// The `dims` array specifies how many times to repeat along each dimension.
        /// The output shape is `[input.shape[0] × dims[0], input.shape[1] × dims[1], ...]`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1, 2, 3, 4, 5, 6 });
        /// var result = Functional.Tile(input, dims: new[] { 2, 1 });
        /// // Result shape: [4, 3] (input repeated 2 times along dimension 0)
        ///
        /// var input2 = Functional.Constant(new TensorShape(2), new[] { 1, 2 });
        /// var result2 = Functional.Tile(input2, dims: new[] { 2, 3 });
        /// // Result: shape [6, 2] with values: [[1, 2, 1, 2, 1, 2], [1, 2, 1, 2, 1, 2]].
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dims">The number of times to repeat the `input` tensor along each dim.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Tile(this FunctionalTensor input, int[] dims)
        {
            // TODO deal with cases where dims.length != input.shape.rank
            return FunctionalLayer.Tile(input, Constant(dims));
        }

        /// <summary>
        /// Returns the `input` tensor with two dimensions swapped.
        /// </summary>
        /// <remarks>
        /// This operator swaps two dimensions of the `input` tensor.
        /// All other dimensions remain in their original positions.
        /// This operator doesn't copy the data. It only changes the dimension order.
        /// For matrices (rank `2` tensors), transposing dimensions `0` and `1` produces the standard matrix transpose.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3, 4), new float[24]); // Shape: [2, 3, 4]
        /// var result = Functional.Transpose(input, dim0: 0, dim1: 2);
        /// // Result shape: [4, 3, 2] (dimensions 0 and 2 swapped)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim0">The first dimension to swap.</param>
        /// <param name="dim1">The second dimension to swap.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Transpose(this FunctionalTensor input, int dim0, int dim1)
        {
            return MoveDim(input, new[] { dim0, dim1 }, new[] { dim1, dim0 });
        }

        /// <summary>
        /// Returns the `input` tensor with a new dimension of size `1` inserted.
        /// </summary>
        /// <remarks>
        /// This operator inserts a new dimension of size `1` at the specified position.
        /// The resulting tensor has the same data but with rank increased by `1`.
        /// This is the inverse operation of <see cref="Squeeze"/>.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(3, 4), new float[12]); // Shape: [3, 4]
        /// var result = Functional.Unsqueeze(input, dim: 0);
        /// // Result shape: [1, 3, 4] (new dimension inserted at position 0)
        /// var result2 = Functional.Unsqueeze(input, dim: 1);
        /// // Result shape: [3, 1, 4] (new dimension inserted at position 1)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension at which to insert a size 1 dimension in the output tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Unsqueeze(this FunctionalTensor input, int dim)
        {
            return FunctionalLayer.Unsqueeze(input, Constant(new[] { dim }));
        }

        /// <summary>
        /// Returns `condition ? input : other` element-wise.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise conditional selection between two tensors.
        /// For each element, if the `condition` is non-zero (true), the value from `input` is selected; otherwise, the value from `other` is selected.
        /// The `condition` tensor must be integer type where non-zero values represent true and zero represents false.
        /// Broadcasting rules apply if the tensors have different shapes.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var condition = Functional.Constant(new[] { 1, 0, 1, 0 });
        /// var input = Functional.Constant(new[] { 10, 20, 30, 40 });
        /// var other = Functional.Constant(new[] { 1, 2, 3, 4 });
        /// var result = Functional.Where(condition, input, other);
        /// // Result: [10, 2, 30, 4] (selects from input where condition is 1, other where 0)
        /// ]]></code>
        /// </example>
        /// <param name="condition">The condition tensor.</param>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Where(FunctionalTensor condition, FunctionalTensor input, FunctionalTensor other)
        {
            DeclareType(DataType.Int, condition);
            return FunctionalLayer.Where(condition, input, other);
        }
    }
}
