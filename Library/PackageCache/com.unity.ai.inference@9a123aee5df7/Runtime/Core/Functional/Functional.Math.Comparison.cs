using System;

namespace Unity.InferenceEngine
{
    public static partial class Functional
    {
        /// <summary>
        /// Returns `input == other` element-wise.
        /// </summary>
        /// <remarks>
        /// This operator compares two tensors element-wise for equality.
        /// Returns an integer tensor where each element is `1` if the corresponding elements are equal, and `0` otherwise.
        /// Promotes `input` and `other` to a compatible data type if necessary.
        /// The tensors are broadcasted to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 1.0f, 2.0f, 3.0f });
        /// var b = Functional.Constant(new[] { 1.0f, 0.0f, 3.0f });
        /// var result = Functional.Equals(a, b);
        /// // Result: [1, 0, 1] (true, false, true)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input.</param>
        /// <param name="other">The second input.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Equals(FunctionalTensor input, FunctionalTensor other)
        {
            (input, other) = PromoteTypes(input, other);
            return FunctionalLayer.Equal(input, other);
        }

        /// <summary>
        /// Returns `input == value` element-wise.
        /// </summary>
        /// <remarks>
        /// This operator compares each element of the `input` tensor with a scalar integer value for equality.
        /// Returns an integer tensor where each element is `1` if equal to the value, and `0` otherwise.
        /// Promotes `value` to match `input`'s data type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1, 2, 3, 2, 1 });
        /// var result = Functional.Equals(input, 2);
        /// // Result: [0, 1, 0, 1, 0] (only elements equal to 2 return 1)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input.</param>
        /// <param name="value">The integer value.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Equals(FunctionalTensor input, int value)
        {
            return input.dataType == DataType.Float ? Equals(input, (float)value) : Equals(input, Constant(value));
        }

        /// <summary>
        /// Returns `input == value` element-wise.
        /// </summary>
        /// <remarks>
        /// This operator compares each element of the `input` tensor with a scalar float value for equality.
        /// Returns an integer tensor where each element is `1` if equal to the value, and `0` otherwise.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, 2.5f, 3.0f, 2.5f });
        /// var result = Functional.Equals(input, 2.5f);
        /// // Result: [0, 1, 0, 1] (only elements equal to 2.5 return 1)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input.</param>
        /// <param name="value">The float value.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Equals(FunctionalTensor input, float value)
        {
            return Equals(input, Constant(value));
        }

        /// <summary>
        /// Returns `input ≥ other` element-wise.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise greater-than or equal to comparison between two tensors.
        /// Returns an integer tensor where each element is `1` if input[i] ≥ other[i], and `0` otherwise.
        /// Promotes `input` and `other` to a compatible data type if necessary.
        /// The tensors are broadcasted to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 3.0f, 1.0f, 5.0f });
        /// var b = Functional.Constant(new[] { 2.0f, 1.0f, 6.0f });
        /// var result = Functional.GreaterEqual(a, b);
        /// // Result: [1, 1, 0] (3≥2: true, 1≥1: true, 5≥6: false)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input.</param>
        /// <param name="other">The second input.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor GreaterEqual(FunctionalTensor input, FunctionalTensor other)
        {
            (input, other) = PromoteTypes(input, other);
            return FunctionalLayer.GreaterOrEqual(input, other);
        }

        /// <summary>
        /// Returns `input > other` element-wise.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise greater-than comparison between two tensors.
        /// Returns an integer tensor where each element is `1` if `input[i]` > `other[i]`, and `0` otherwise.
        /// Promotes `input` and `other` to a compatible data type if necessary.
        /// The tensors are broadcasted to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 3.0f, 1.0f, 5.0f });
        /// var b = Functional.Constant(new[] { 2.0f, 1.0f, 6.0f });
        /// var result = Functional.Greater(a, b);
        /// // Result: [1, 0, 0] (3>2: true, 1>1: false, 5>6: false)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input.</param>
        /// <param name="other">The second input.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Greater(FunctionalTensor input, FunctionalTensor other)
        {
            (input, other) = PromoteTypes(input, other);
            return FunctionalLayer.Greater(input, other);
        }

        /// <summary>
        /// Returns an integer tensor with elements representing if each element of `input` is finite.
        /// </summary>
        /// <remarks>
        /// This operator checks if each element of the `input` tensor is a finite number (not infinity or Not a Number [NaN]).
        /// Returns an integer tensor where each element is `1` if the value is finite, and `0` if the value is infinite or NaN.
        /// For integer tensors, all elements are considered finite and the result is all ones.
        /// A finite number is any value that isn't positive infinity, negative infinity, or NaN.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, float.PositiveInfinity, float.NaN, -5.0f });
        /// var result = Functional.IsFinite(input);
        /// // Result: [1, 0, 0, 1] (only 1.0 and -5.0 are finite)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor IsFinite(FunctionalTensor input)
        {
            // TODO add to backend and layers
            if (input.dataType == DataType.Int)
                return OnesLike(input);
            return FunctionalLayer.Not(FunctionalLayer.Or(IsInf(input), IsNaN(input)));
        }

        /// <summary>
        /// Returns an integer tensor with elements representing if each element of `input` is positive or negative infinity.
        /// </summary>
        /// <remarks>
        /// This operator checks if each element of the `input` tensor is infinite (positive or negative infinity).
        /// Returns an integer tensor where each element is `1` if the value is infinite, and `0` otherwise.
        /// For integer tensors, no elements can be infinite and the result is all zeros.
        /// Not a Number (NaN) values aren't considered infinite.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, float.PositiveInfinity, float.NegativeInfinity, float.NaN });
        /// var result = Functional.IsInf(input);
        /// // Result: [0, 1, 1, 0] (only the infinity values return 1)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor IsInf(FunctionalTensor input)
        {
            if (input.dataType == DataType.Int)
                return ZerosLike(input);
            return FunctionalLayer.IsInf(input, true, true);
        }

        /// <summary>
        /// Returns an integer tensor with elements representing if each element of `input` is NaN.
        /// </summary>
        /// <remarks>
        /// This operator checks if each element of the `input` tensor is Not a Number (NaN).
        /// Returns an integer tensor where each element is `1` if the value is NaN, and `0` otherwise.
        /// For integer tensors, no elements can be NaN and the result is all zeros.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, float.NaN, float.PositiveInfinity, 0.0f / 0.0f });
        /// var result = Functional.IsNaN(input);
        /// // Result: [0, 1, 0, 1] (only the NaN values return 1)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor IsNaN(FunctionalTensor input)
        {
            if (input.dataType == DataType.Int)
                return ZerosLike(input);
            return FunctionalLayer.IsNaN(input);
        }

        /// <summary>
        /// Returns `input ≤ other` element-wise.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise less than or equal to comparison between two tensors.
        /// Returns an integer tensor where each element is `1` if `input[i] ≤ other[i]`, and `0` otherwise.
        /// Promotes `input` and `other` to a compatible data type if necessary.
        /// The tensors are broadcasted to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 1.0f, 2.0f, 5.0f });
        /// var b = Functional.Constant(new[] { 2.0f, 2.0f, 4.0f });
        /// var result = Functional.LessEqual(a, b);
        /// // Result: [1, 1, 0] (1≤2: true, 2≤2: true, 5≤4: false)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input.</param>
        /// <param name="other">The second input.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor LessEqual(FunctionalTensor input, FunctionalTensor other)
        {
            (input, other) = PromoteTypes(input, other);
            return FunctionalLayer.LessOrEqual(input, other);
        }

        /// <summary>
        /// Returns `input &lt; other` element-wise.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise less than comparison between two tensors.
        /// Returns an integer tensor where each element is `1` if `input[i] &lt; other[i]`, and `0` otherwise.
        /// Promotes `input` and `other` to a compatible data type if necessary.
        /// The tensors are broadcasted to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 1.0f, 2.0f, 5.0f });
        /// var b = Functional.Constant(new[] { 2.0f, 2.0f, 4.0f });
        /// var result = Functional.Less(a, b);
        /// // Result: [1, 0, 0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        ///
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Less(FunctionalTensor input, FunctionalTensor other)
        {
            (input, other) = PromoteTypes(input, other);
            return FunctionalLayer.Less(input, other);
        }

        /// <summary>
        /// Returns the element-wise maximum of `input` and `other`.
        /// </summary>
        /// <remarks>
        /// This operator computes the element-wise maximum between two tensors.
        /// For each element position, the output contains the larger of the two input values.
        /// Promotes `input` and `other` to a compatible data type if necessary.
        /// The tensors are broadcasted to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 1.0f, 5.0f, 3.0f });
        /// var b = Functional.Constant(new[] { 2.0f, 4.0f, 6.0f });
        /// var result = Functional.Max(a, b);
        /// // Result: [2.0, 5.0, 6.0] (max of each pair)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Max(FunctionalTensor input, FunctionalTensor other)
        {
            (input, other) = PromoteTypes(input, other);
            return FunctionalLayer.Max(input, other);
        }

        /// <summary>
        /// Returns the element-wise minimum of `input` and `other`.
        /// </summary>
        /// <remarks>
        /// This operator computes the element-wise minimum between two tensors.
        /// For each element position, the output contains the smaller of the two input values.
        /// Promotes `input` and `other` to a compatible data type if necessary.
        /// The tensors are broadcasted to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 1.0f, 5.0f, 3.0f });
        /// var b = Functional.Constant(new[] { 2.0f, 4.0f, 6.0f });
        /// var result = Functional.Min(a, b);
        /// // Result: [1.0, 4.0, 3.0] (min of each pair)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Min(FunctionalTensor input, FunctionalTensor other)
        {
            (input, other) = PromoteTypes(input, other);
            return FunctionalLayer.Min(input, other);
        }

        /// <summary>
        /// Returns `input ≠ other` element-wise.
        /// </summary>
        /// <remarks>
        /// This operator compares two tensors element-wise for inequality.
        /// Returns an integer tensor where each element is `1` if the corresponding elements aren't equal, and `0` if equal.
        /// Promotes `input` and `other` to a compatible data type if necessary.
        /// The tensors are broadcasted to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 1.0f, 2.0f, 3.0f });
        /// var b = Functional.Constant(new[] { 1.0f, 0.0f, 3.0f });
        /// var result = Functional.NotEqual(a, b);
        /// // Result: [0, 1, 0] (false, true, false)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor NotEqual(FunctionalTensor input, FunctionalTensor other)
        {
            (input, other) = PromoteTypes(input, other);
            return FunctionalLayer.NotEqual(input, other);
        }

        /// <summary>
        /// Returns the `k` largest elements of the `input` tensor along a given dimension.
        /// </summary>
        /// <remarks>
        /// This operator finds the `top-k` largest (or smallest) elements along a specified dimension of the `input` tensor.
        /// Returns an array containing two tensors: output `0` with the values of the `top-k` elements, output `1` with the indices of those elements in the original tensor.
        /// When `largest` is `true`, returns the `k` largest elements. When `false`, returns the `k` smallest elements.
        /// The `sorted` parameter controls whether to sort the output, in descending (`largest=true`) or ascending (`largest=false`) order.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 3.0f, 1.0f, 4.0f, 1.0f, 5.0f });
        /// var result = Functional.TopK(input, k: 3, dim: 0, largest: true);
        /// // result[0]: [5.0, 4.0, 3.0] (top 3 values)
        /// // result[1]: [4, 2, 0] (their indices in the input)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input.</param>
        /// <param name="k">The number of elements to calculate.</param>
        /// <param name="dim">The axis along which to perform the top-`k` operation.</param>
        /// <param name="largest">Whether to calculate the top-`k` largest elements. If this is `false` the layer calculates the top-`k` smallest elements.</param>
        /// <param name="sorted">Whether to return the elements in sorted order in the output tensor.</param>
        /// <returns>The output values and indices tensors in an array.</returns>
        public static FunctionalTensor[] TopK(FunctionalTensor input, int k, int dim = -1, bool largest = true, bool sorted = true)
        {
            return FunctionalLayer.TopK(input, Constant(new[] { k }), dim, largest, sorted);
        }
    }
}
