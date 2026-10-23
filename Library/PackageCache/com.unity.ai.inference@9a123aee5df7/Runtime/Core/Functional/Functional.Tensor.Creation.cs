using System;

namespace Unity.InferenceEngine
{
    public static partial class Functional
    {
        /// <summary>
        /// Returns a tensor with shape `size`, filled with values taken from the `input` tensor using `stride` and `offset`.
        /// </summary>
        /// <remarks>
        /// Creates a tensor of the given `size`, with values taken from the `input` tensor, using the `stride`, and starting at `offset`.
        /// The size is the shape of the created tensor.
        /// The `stride` determines the step in the `input` tensor for each dimension.
        /// The `offset` (optional) specifies the starting position in the `input` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.ARange(12); // [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]
        /// // Create a tensor of shape [3, 2] from values in input tensor
        /// var strided = Functional.AsStrided(input, new int[] {3, 2}, new int[] {4, 1}, 0);
        /// // Result is a tensor of shape [3, 2], with values: [[0, 1], [4, 5], [8, 9]]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="size">The shape of the output tensor.</param>
        /// <param name="stride">The stride of the output tensor.</param>
        /// <param name="offset">(Optional) The data offset of the output tensor. Default value is `0`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor AsStrided(FunctionalTensor input, int[] size, int[] stride, int offset = 0)
        {
            return FunctionalLayer.AsStrided(input, Constant(size), Constant(stride), Constant(offset));
        }

        /// <summary>
        /// Returns a tensor filled with `0` with shape `size` and data type `dataType`.
        /// </summary>
        /// <remarks>
        /// Creates a tensor with the specified shape (`size`) with all elements initialized to `0`.
        /// `dataType` can be either <see cref="DataType.Int"/> or <see cref="DataType.Float"/>, defaulting to <see cref="DataType.Int"/>.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Create a tensor of shape [3, 4] filled with integer zeros
        /// var intZeros = Functional.Zeros(new int[] {3, 4});
        /// // Create a tensor of shape [2, 3, 4] filled with float zeros
        /// var floatZeros = Functional.Zeros(new int[] {2, 3, 4}, DataType.Float);
        /// ]]></code>
        /// </example>
        /// <param name="size">The shape of the tensor.</param>
        /// <param name="dataType">The data type of the tensor. Default is <see cref="DataType.Int"/>.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Zeros(int[] size, DataType dataType = DataType.Int)
        {
            return dataType switch
            {
                DataType.Float => Full(size, 0f),
                DataType.Int => Full(size, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
            };
        }

        /// <summary>
        /// Returns a tensor filled with `0` with the shape of `input` and data type `dataType`.
        /// </summary>
        /// <remarks>
        /// Creates a tensor with the same shape as the `input` tensor, with all elements initialized to `0`.
        /// `dataType` can be either <see cref="DataType.Int"/> or <see cref="DataType.Float"/>, defaulting to <see cref="DataType.Int"/>.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(3, 4), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f });
        /// // Create a tensor of integer zeros with the same shape as input
        /// var intZeros = Functional.ZerosLike(input);
        /// // Creates a tensor of float zeros with the same shape as input
        /// var floatZeros = Functional.ZerosLike(input, DataType.Float);
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dataType">The data type of the tensor. Default is <see cref="DataType.Int"/>.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ZerosLike(FunctionalTensor input, DataType dataType = DataType.Int)
        {
            return dataType switch
            {
                DataType.Float => FullLike(input, 0f),
                DataType.Int => FullLike(input, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
            };
        }

        /// <summary>
        /// Returns a tensor filled with `1` with given shape `size` and data type `dataType`.
        /// </summary>
        /// <remarks>
        /// Creates a tensor with the specified shape `size` with all elements initialized to `1`.
        /// `dataType` can be either <see cref="DataType.Int"/> or <see cref="DataType.Float"/>, defaulting to <see cref="DataType.Int"/>.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Create a tensor of shape [3, 4] filled with integer ones
        /// var intOnes = Functional.Ones(new int[] {3, 4});
        /// // Create a tensor of shape [2, 3, 4] filled with float ones
        /// var floatOnes = Functional.Ones(new int[] {2, 3, 4}, DataType.Float);
        /// ]]></code>
        /// </example>
        /// <param name="size">The shape of the tensor.</param>
        /// <param name="dataType">The data type of the tensor. Default is <see cref="DataType.Int"/>.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Ones(int[] size, DataType dataType = DataType.Int)
        {
            return dataType switch
            {
                DataType.Float => Full(size, 1f),
                DataType.Int => Full(size, 1),
                _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
            };
        }

        /// <summary>
        /// Returns a tensor filled with `1` with the shape of `input` and data type `dataType`.
        /// </summary>
        /// <remarks>
        /// Creates a tensor with the same shape as the `input` tensor, with all elements initialized to `1`.
        /// `dataType` can be either <see cref="DataType.Int"/> or <see cref="DataType.Float"/>, defaulting to <see cref="DataType.Int"/>.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(3, 4), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f });
        /// // Create a tensor of integer ones with the same shape as input
        /// var intOnes = Functional.OnesLike(input);
        /// // Creates a tensor of float ones with the same shape as input
        /// var floatOnes = Functional.OnesLike(input, DataType.Float);
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dataType">The data type of the tensor. Default is <see cref="DataType.Int"/>.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor OnesLike(FunctionalTensor input, DataType dataType = DataType.Int)
        {
            return dataType switch
            {
                DataType.Float => FullLike(input, 1f),
                DataType.Int => FullLike(input, 1),
                _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
            };
        }

        /// <summary>
        /// Returns a 1D tensor of size `⌈(end − start) / step⌉` with values from the interval `[start, end)`, with a step beginning from `start`.
        /// </summary>
        /// <remarks>
        /// Creates a tensor with all integer values from `start` (included) to `end` (not included), separated by the (optional) `step` value.
        /// The three inputs are integers: the start and end of the range, and the step between values.
        /// If end is less than or equal to start, the returned tensor is empty.
        /// The `step` parameter is optional and defaults to `1`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Get a tensor with values [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]
        /// var tensor = Functional.ARange(0, 10);
        /// // Get a tensor with values [2, 5, 8, 11, 14]
        /// var tensor2 = Functional.ARange(2, 15, 3);
        /// ]]></code>
        /// </example>
        /// <param name="start">The value of the first element.</param>
        /// <param name="end">The upper end of the interval.</param>
        /// <param name="step">(Optional) The delta between each element. Default value is `1`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ARange(int start, int end, int step = 1)
        {
            return FunctionalLayer.Range(Constant(start), Constant(end), Constant(step));
        }

        /// <summary>
        /// Returns a 1D tensor of size `⌈end / step⌉` with values from the interval `[0, end)` with a step `1` beginning at `0`.
        /// </summary>
        /// <remarks>
        /// Creates a tensor with all integer values from `0` (included) to `end` (not included).
        /// The input value is an integer representing the end of the range.
        /// If `end` is less than or equal to `0`, returns an empty tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Get a tensor with values [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]
        /// var tensor = Functional.ARange(10);
        /// ]]></code>
        /// </example>
        /// <param name="end">The upper end of the interval.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ARange(int end)
        {
            return ARange(0, end);
        }

        /// <summary>
        /// Returns a 1D tensor of size `⌈(end − start) / step⌉` with values from the interval `[start, end)`, with a step beginning from `start`.
        /// </summary>
        /// <remarks>
        /// Creates a tensor with all float values from `start` (included) to `end` (not included), separated by the (optional) `step` value.
        /// The three inputs are floats: the `start` and `end` of the range, and the `step` between values.
        /// If end is less than or equal to start, the returned tensor is empty.
        /// The `step` parameter is optional and defaults to `1.0`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Get a tensor with values [0.1, 1.2, 2.3, 3.4, 4.5, 5.6]
        /// var tensor = Functional.ARange(0.1f, 6f, 1.1f);
        /// // Get a tensor with values [2.2, 3.2, 4.2, 5.2, 6.2]
        /// var tensor = Functional.ARange(2.2f, 7f);
        /// ]]></code>
        /// </example>
        /// <param name="start">The value of the first element.</param>
        /// <param name="end">The upper end of the interval.</param>
        /// <param name="step">(Optional) The delta between each element. Default value is `1.0`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ARange(float start, float end, float step = 1)
        {
            return FunctionalLayer.Range(Constant(start), Constant(end), Constant(step));
        }

        /// <summary>
        /// Returns a 1D tensor of size `⌈end / step⌉` with values from the interval `[0, end)` with a step `1` beginning at `0`.
        /// </summary>
        /// <remarks>
        /// Creates a tensor with all float values from `0` (included) to `end` (not included).
        /// The input is a float value representing the end of the range.
        /// If `end` is less than or equal to `0`, returns an empty tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Get a tensor with float values [0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0]
        /// var tensor = Functional.ARange(10f);
        /// ]]></code>
        /// </example>
        /// <param name="end">The upper end of the interval.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ARange(float end)
        {
            return ARange(0, end);
        }

        /// <summary>
        /// Returns a 1D tensor of size `steps` with values evenly spaced from the interval `[start, end]`.
        /// </summary>
        /// <remarks>
        /// Creates a 1D tensor of dimension `steps` with evenly spaced values over a specified interval.
        /// The output includes the `start` and `end` values.
        /// The `steps` parameter determines the number of elements in the output.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Create a tensor with 5 evenly spaced values from 0 to 10
        /// var tensor = Functional.LinSpace(0f, 10f, 5); // [0.0, 2.5, 5.0, 7.5, 10.0]
        /// // Create a tensor with 11 evenly spaced values from -1 to 1
        /// var tensor2 = Functional.LinSpace(-1f, 1f, 11); // [-1.0, -0.8, -0.6, -0.4, -0.2, 0.0, 0.2, 0.4, 0.6, 0.8, 1.0]
        /// ]]></code>
        /// </example>
        /// <param name="start">The value of the first element.</param>
        /// <param name="end">The value of the last element.</param>
        /// <param name="steps">The number of elements.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor LinSpace(float start, float end, int steps)
        {
            Logger.AssertIsTrue(steps >= 0, "LinSpace.InputError steps must be non-negative");
            if (steps == 0)
                return Constant(Array.Empty<float>());
            if (steps == 1)
                return Constant(new[] { start });
            var delta = (end - start) / (steps - 1);
            var starts = start;
            var ends = end + 0.5f * delta;
            return FunctionalLayer.Range(Constant(starts), Constant(ends), Constant(delta));
        }

        /// <summary>
        /// Returns a 1D tensor of size `steps` with values evenly spaced from the interval `[logBase^start, logBase^end]` on a logarithmic scale.
        /// </summary>
        /// <remarks>
        /// Creates a tensor with values that are evenly spaced on a logarithmic scale.
        /// Computes the output values as logBase raised to the power of evenly spaced exponents.
        /// The default base is `10`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Create a tensor with 4 logarithmically spaced values from 10^1 to 10^4
        /// var tensor = Functional.LogSpace(1f, 4f, 4); // [10, 100, 1000, 10000]
        /// // Create a tensor with 5 logarithmically spaced values using base 2
        /// var tensor2 = Functional.LogSpace(0f, 4f, 5, 2f); // [1, 2, 4, 8, 16]
        /// ]]></code>
        /// </example>
        /// <param name="start">The value of the first exponent.</param>
        /// <param name="end">The value of the last exponent.</param>
        /// <param name="steps">The number of elements.</param>
        /// <param name="logBase">The base of the logarithm. Default value is `10`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor LogSpace(float start, float end, int steps, float logBase = 10)
        {
            return Pow(Constant(logBase), LinSpace(start, end, steps));
        }

        /// <summary>
        /// Returns a tensor filled with constant `fillValue` with given shape `size`.
        /// </summary>
        /// <remarks>
        /// Creates a tensor with the specified shape (`size`), with all elements initialized to the provided integer value `fillValue`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Create a tensor of shape [3, 4] filled with the value 42
        /// var tensor = Functional.Full(new int[] {3, 4}, 42);
        /// // Create a tensor of shape [2, 3, 4] filled with the value -5
        /// var tensor2 = Functional.Full(new int[] {2, 3, 4}, -5);
        /// ]]></code>
        /// </example>
        /// <param name="size">The shape of the tensor.</param>
        /// <param name="fillValue">The fill value of the tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Full(int[] size, int fillValue)
        {
            return FunctionalLayer.ConstantOfShape(Constant(size), DataType.Int, 0, fillValue);
        }

        /// <summary>
        /// Returns a tensor filled with a constant `fillValue` with given shape `size`.
        /// </summary>
        /// <remarks>
        /// Creates a tensor with the specified shape (`size`), with all elements initialized to the provided float value `fillValue`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Create a tensor of shape [3, 4] filled with the value 3.14
        /// var tensor = Functional.Full(new int[] {3, 4}, 3.14f);
        /// // Create a tensor of shape [2, 3, 4] filled with the value -0.5
        /// var tensor2 = Functional.Full(new int[] {2, 3, 4}, -0.5f);
        /// ]]></code>
        /// </example>
        /// <param name="size">The shape of the tensor.</param>
        /// <param name="fillValue">The fill value of the tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Full(int[] size, float fillValue)
        {
            return FunctionalLayer.ConstantOfShape(Constant(size), DataType.Float, fillValue, 0);
        }

        /// <summary>
        /// Returns a tensor filled with a constant value with the same shape as the `input` tensor.
        /// </summary>
        /// <remarks>
        /// Creates a tensor with the same shape as the `input` tensor, with all elements initialized to the provided integer value `fillValue`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(3, 4), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f });
        /// // Create a tensor of the same shape as input filled with 42
        /// var tensor = Functional.FullLike(input, 42);
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="fillValue">The fill value of the tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor FullLike(FunctionalTensor input, int fillValue)
        {
            var shape = FunctionalLayer.Shape(input, 0, TensorShape.maxRank);
            return FunctionalLayer.ConstantOfShape(shape, DataType.Int, 0, fillValue);
        }

        /// <summary>
        /// Returns a tensor filled with a constant value `fillValue` with the same shape as the `input` tensor.
        /// </summary>
        /// <remarks>
        /// Creates a tensor with the same shape as the `input` tensor, with all elements initialized to the provided float value `fillValue`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(3, 4), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f });
        /// // Create a tensor of the same shape as input filled with 3.14
        /// var tensor = Functional.FullLike(input, 3.14f);
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="fillValue">The fill value of the tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor FullLike(FunctionalTensor input, float fillValue)
        {
            var shape = FunctionalLayer.Shape(input, 0, TensorShape.maxRank);
            return FunctionalLayer.ConstantOfShape(shape, DataType.Float, fillValue, 0);
        }
    }
}
