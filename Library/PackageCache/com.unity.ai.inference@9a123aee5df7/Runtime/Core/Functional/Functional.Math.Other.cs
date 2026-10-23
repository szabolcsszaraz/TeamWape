
namespace Unity.InferenceEngine
{
    public static partial class Functional
    {
        /// <summary>
        /// Expands each input tensor with rank less than `1` to rank `1`. Returns an array of expanded tensors.
        /// </summary>
        /// <remarks>
        /// This operation ensures that all input tensors have at least one dimension.
        /// Expands scalar tensors (rank `0`) to shape `[1]`. Tensors with rank `1` or higher remain unchanged.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var scalar = Functional.Constant(5.0f); // Shape: []
        /// var vector = Functional.Constant(new[] { 1.0f, 2.0f }); // Shape: [2]
        /// var results = Functional.AtLeast1D(scalar, vector);
        /// // results[0] shape: [1], values: [5.0]
        /// // results[1] shape: [2], values: [1.0, 2.0] (unchanged)
        /// ]]></code>
        /// </example>
        /// <param name="tensors">The input tensor array.</param>
        /// <returns>The output tensor array.</returns>
        public static FunctionalTensor[] AtLeast1D(params FunctionalTensor[] tensors)
        {
            var outputs = new FunctionalTensor[tensors.Length];
            for (var i = 0; i < outputs.Length; i++)
            {
                if (!tensors[i].shape.isRankDynamic && tensors[i].shape.rank >= 1)
                    outputs[i] = tensors[i];
                else
                    outputs[i] = BroadcastTo(tensors[i], new[] { 1 });
            }
            return outputs;
        }

        /// <summary>
        /// Expands each input tensor with rank less than `2` to rank `2`. Returns an array of expanded tensors.
        /// </summary>
        /// <remarks>
        /// This operation ensures that all input tensors have at least `2` dimensions.
        /// Expands tensors with rank less than `2` by prepending dimensions of size `1`. Tensors with rank `2` or higher remain unchanged.
        /// Scalars become `[1, 1]`, vectors become `[1, n]`, and matrices remain unchanged.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var scalar = Functional.Constant(5.0f); // Shape: []
        /// var vector = Functional.Constant(new[] { 1.0f, 2.0f }); // Shape: [2]
        /// var matrix = Functional.Constant(new TensorShape(2, 2), new[] { 1.0f, 2.0f, 3.0f, 4.0f }); // Shape: [2, 2]
        /// var results = Functional.AtLeast2D(scalar, vector, matrix);
        /// // results[0] shape: [1, 1], values: [[5.0]]
        /// // results[1] shape: [1, 2], values: [[1.0, 2.0]]
        /// // results[2] shape: [2, 2], values: [[1.0, 2.0], [3.0, 4.0]] (unchanged)
        /// ]]></code>
        /// </example>
        /// <param name="tensors">The input tensor array.</param>
        /// <returns>The output tensor array.</returns>
        public static FunctionalTensor[] AtLeast2D(params FunctionalTensor[] tensors)
        {
            var outputs = new FunctionalTensor[tensors.Length];
            for (var i = 0; i < outputs.Length; i++)
            {
                if (!tensors[i].shape.isRankDynamic && tensors[i].shape.rank >= 2)
                    outputs[i] = tensors[i];
                else
                    outputs[i] = BroadcastTo(tensors[i], new[] { 1, 1 });
            }
            return outputs;
        }

        /// <summary>
        /// Expands each input tensor with rank less than `3` to rank `3`. Returns an array of expanded tensors.
        /// </summary>
        /// <remarks>
        /// This operation ensures that all input tensors have at least `3` dimensions.
        /// Expands tensors with rank less than `3` by prepending dimensions of size `1`, while tensors with rank `3` or higher remain unchanged.
        /// Scalars become `[1, 1, 1]`, vectors become `[1, 1, n]`, matrices become `[1, m, n]`, and 3D tensors remain unchanged.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var scalar = Functional.Constant(5.0f); // Shape: []
        /// var vector = Functional.Constant(new[] { 1.0f, 2.0f }); // Shape: [2]
        /// var matrix = Functional.Constant(new TensorShape(2, 2), new[] { 1.0f, 2.0f, 3.0f, 4.0f }); // Shape: [2, 2]
        /// var results = Functional.AtLeast3D(scalar, vector, matrix);
        /// // results[0] shape: [1, 1, 1], values: [[[5.0]]]
        /// // results[1] shape: [1, 1, 2], values: [[[1.0, 2.0]]]
        /// // results[2] shape: [1, 2, 2], values: [[[1.0, 2.0], [3.0, 4.0]]]
        /// ]]></code>
        /// </example>
        /// <param name="tensors">The input tensor array.</param>
        /// <returns>The output tensor array.</returns>
        public static FunctionalTensor[] AtLeast3D(params FunctionalTensor[] tensors)
        {
            var outputs = new FunctionalTensor[tensors.Length];
            for (var i = 0; i < outputs.Length; i++)
            {
                if (!tensors[i].shape.isRankDynamic && tensors[i].shape.rank >= 3)
                    outputs[i] = tensors[i];
                else
                    outputs[i] = BroadcastTo(tensors[i], new[] { 1, 1, 1 });
            }
            return outputs;
        }

        /// <summary>
        /// Returns the `input` tensor broadcasted to a shape.
        /// </summary>
        /// <remarks>
        /// This operation expands the `input` tensor to match the specified `shape` by duplicating elements along dimensions of size `1`.
        /// Broadcasting follows these rules: dimensions are aligned from right to left, and dimensions of size `1` can be expanded to any size.
        /// The `input` tensor's shape must be compatible with the target shape for broadcasting.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, 2.0f, 3.0f }); // Shape: [3]
        /// var result = Functional.BroadcastTo(input, new[] { 2, 3 });
        /// // Result shape: [2, 3]
        /// // [[1.0, 2.0, 3.0], [1.0, 2.0, 3.0]]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="shape">The shape to broadcast to.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor BroadcastTo(this FunctionalTensor input, int[] shape)
        {
            return FunctionalLayer.Expand(input, Constant(shape));
        }

        /// <summary>
        /// Returns a copy of the `input`.
        /// </summary>
        /// <remarks>
        /// This operation creates a copy of the `input` tensor.
        /// The resulting tensor has the same shape and values as the `input`, but is a distinct tensor in the computational graph.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, 2.0f, 3.0f });
        /// var result = Functional.Clone(input);
        /// // Result: [1.0, 2.0, 3.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Clone(this FunctionalTensor input)
        {
            return FunctionalLayer.Identity(input);
        }

        /// <summary>
        /// Returns the cumulative sum of the elements of the `input` in a dimension.
        /// </summary>
        /// <remarks>
        /// This operation computes the cumulative sum of elements along the specified dimension.
        /// Each element in the output is the sum of all elements up to and including that position along the dimension.
        /// The output tensor has the same shape as the `input` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, 2.0f, 3.0f, 4.0f });
        /// var result = Functional.CumSum(input, 0);
        /// // Result: [1.0, 3.0, 6.0, 10.0]
        /// // (1, 1+2, 1+2+3, 1+2+3+4)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension in which to sum.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor CumSum(FunctionalTensor input, int dim)
        {
            return FunctionalLayer.CumSum(input, Constant(dim), false, false);
        }

        /// <summary>
        /// Returns the remaining dimensions of the `input`, with the diagonal defined by `dim1` and `dim2` appended as the last dimension.
        /// </summary>
        /// <remarks>
        /// This operation extracts diagonal elements from the `input` tensor along two specified dimensions.
        /// The `offset` parameter controls which diagonal to extract: `0` for the main diagonal, a positive value for above the main diagonal, and negative for below the main diagonal.
        /// Appends the extracted diagonal elements as the last dimension of the output tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(3, 3), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f });
        /// var result = Functional.Diagonal(input);
        /// // Result: [1.0, 5.0, 9.0] (main diagonal elements)
        ///
        /// var result2 = Functional.Diagonal(input, offset: 1);
        /// // Result: [2.0, 6.0] (diagonal above the main diagonal)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="offset">Defines the diagonal to consider as an offset from the main diagonal.</param>
        /// <param name="dim1">The first dimension that defines the diagonal.</param>
        /// <param name="dim2">The second dimension that defines the diagonal.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Diagonal(FunctionalTensor input, int offset = 0, int dim1 = 0, int dim2 = 1)
        {
            return FunctionalLayer.Diagonal(input, offset, dim1, dim2);
        }

        /// <summary>
        /// Returns the sums the product of the elements of the `operands` tensors along dimensions specified using a notation based on the Einstein summation convention.
        /// </summary>
        /// <remarks>
        /// This operation performs Einstein summation.
        /// The `equation` string specifies which dimensions to multiply and sum over using subscript labels.
        /// In the `equation`, repeated indices define dimensions to sum over, and indices that appear only once are kept in the output.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Matrix multiplication: C[i,j] = sum_k A[i,k] * B[k,j]
        /// var a = Functional.Constant(new TensorShape(2, 2), new[] { 1.0f, 2.0f, 3.0f, 4.0f });
        /// var b = Functional.Constant(new TensorShape(2, 2), new[] { 5.0f, 6.0f, 7.0f, 8.0f });
        /// var result = Functional.Einsum("ij,jk->ik", a, b);
        /// // Result: [[19.0, 22.0], [43.0, 50.0]]
        ///
        /// // Batch dot product: sum_i A[i] * B[i]
        /// var x = Functional.Constant(new[] { 1.0f, 2.0f, 3.0f });
        /// var y = Functional.Constant(new[] { 4.0f, 5.0f, 6.0f });
        /// var dot = Functional.Einsum("i,i->", x, y);
        /// // Result: 32.0 (1*4 + 2*5 + 3*6)
        /// ]]></code>
        /// </example>
        /// <param name="equation">The equation of the Einstein summation as a comma-separated list of subscript labels.</param>
        /// <param name="operands">The input tensors.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Einsum(string equation, params FunctionalTensor[] operands)
        {
            return FunctionalLayer.Einsum(operands, equation);
        }

        /// <summary>
        /// Returns the `input` tensor with its elements reversed on some dimensions.
        /// </summary>
        /// <remarks>
        /// This operation reverses the order of elements along the specified dimensions.
        /// An array specifies the dimension indices.
        /// The array must not contain repeated dimensions.
        /// The `input`'s shape remains the same.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f });
        /// var result = Functional.Flip(input, new[] { 0 });
        /// // Result: [[4.0, 5.0, 6.0], [1.0, 2.0, 3.0]] (reversed along rows)
        ///
        /// var result2 = Functional.Flip(input, new[] { 1 });
        /// // Result: [[3.0, 2.0, 1.0], [6.0, 5.0, 4.0]] (reversed along columns)
        ///
        /// var result3 = Functional.Flip(input, new[] { 0, 1 });
        /// // Result: [[6.0, 5.0, 4.0], [3.0, 2.0, 1.0]] (reversed along rows and columns)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dims">The dimensions on which to reverse the elements, values may not repeat.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Flip(this FunctionalTensor input, int[] dims)
        {
            //Slice(x, starts = [-1], ends = [INT_MIN], steps = [-1])
            var starts = new int[dims.Length];
            var ends = new int[dims.Length];
            var steps = new int[dims.Length];
            for (var i = 0; i < dims.Length; i++)
            {
                starts[i] = -1;
                ends[i] = int.MinValue;
                steps[i] = -1;
            }

            return FunctionalLayer.Slice(input, Constant(starts), Constant(ends), Constant(dims), Constant(steps));
        }

        /// <summary>
        /// Returns the `input` tensor with its elements reversed on the second dimension.
        /// </summary>
        /// <remarks>
        /// This operation flips the tensor left-to-right by reversing the order of elements along dimension `1` (columns).
        /// Equivalent to calling <see cref="Flip"/> with `dims=[1]`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f });
        /// var result = Functional.FlipLR(input);
        /// // Result: [[3.0, 2.0, 1.0], [6.0, 5.0, 4.0]]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor FlipLR(this FunctionalTensor input)
        {
            return Flip(input, new[] { 1 });
        }

        /// <summary>
        /// Returns the `input` tensor with its elements reversed on the first dimension.
        /// </summary>
        /// <remarks>
        /// This operation flips the tensor up-to-down by reversing the order of elements along dimension `0` (rows).
        /// Equivalent to calling <see cref="Flip"/> with `dims=[0]`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f });
        /// var result = Functional.FlipUD(input);
        /// // Result: [[4.0, 5.0, 6.0], [1.0, 2.0, 3.0]]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor FlipUD(this FunctionalTensor input)
        {
            return Flip(input, new[] { 0 });
        }

        /// <summary>
        /// Returns the `input` tensor with its elements flattened to a single dimension.
        /// </summary>
        /// <remarks>
        /// This operation flattens the `input` tensor to a 1D array containing all its elements.
        /// The elements are arranged in row-major (C-style) order.
        /// Equivalent to calling <see cref="Reshape"/> with shape `[-1]`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f });
        /// var result = Functional.Ravel(input);
        /// // Result: [1.0, 2.0, 3.0, 4.0, 5.0, 6.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Ravel(this FunctionalTensor input)
        {
            return Reshape(input, new[] { -1 });
        }

        /// <summary>
        /// Retains the lower triangular values of `input` matrix. The other values are zeroed.
        /// </summary>
        /// <remarks>
        /// This operation returns a tensor with the lower triangular part of the `input` retained and all other elements set to zero.
        /// The `diagonal` parameter controls which diagonal acts as the boundary: `0` for the main diagonal, positive for diagonals above, and negative for diagonals below.
        /// Keeps elements on and below the specified diagonal. Zeroes elements above the specified diagonal.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(3, 3), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f });
        /// var result = Functional.TriL(input);
        /// // Result: [[1.0, 0.0, 0.0], [4.0, 5.0, 0.0], [7.0, 8.0, 9.0]]
        ///
        /// var result2 = Functional.TriL(input, diagonal: 1);
        /// // Result: [[1.0, 2.0, 0.0], [4.0, 5.0, 6.0], [7.0, 8.0, 9.0]] (includes one diagonal above main)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="diagonal">The integer offset of the diagonal.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor TriL(FunctionalTensor input, int diagonal = 0)
        {
            return FunctionalLayer.Trilu(input, Constant(diagonal), Layers.TriluMode.Lower);
        }

        /// <summary>
        /// Retains the upper triangular values of an input matrix. Zeroes the other values.
        /// </summary>
        /// <remarks>
        /// This operation returns a tensor with the upper triangular part of the `input` retained and all other elements set to zero.
        /// The `diagonal` parameter controls which diagonal acts as the boundary: `0` for the main diagonal, positive for diagonals above, and negative for diagonals below.
        /// Keeps elements on and above the specified diagonal. Zeroes elements below the specified diagonal.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(3, 3), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f });
        /// var result = Functional.TriU(input);
        /// // Result: [[1.0, 2.0, 3.0], [0.0, 5.0, 6.0], [0.0, 0.0, 9.0]]
        ///
        /// var result2 = Functional.TriU(input, diagonal: -1);
        /// // Result: [[1.0, 2.0, 3.0], [4.0, 5.0, 6.0], [0.0, 8.0, 9.0]] (includes one diagonal below main)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="diagonal">The integer offset of the diagonal.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor TriU(FunctionalTensor input, int diagonal = 0)
        {
            return FunctionalLayer.Trilu(input, Constant(diagonal), Layers.TriluMode.Upper);
        }
    }
}
