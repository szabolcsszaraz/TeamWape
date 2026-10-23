using System;

namespace Unity.InferenceEngine
{
    public static partial class Functional
    {
        /// <summary>
        /// Returns the indices of the maximum value of the elements of the `input` along a dimension.
        /// </summary>
        /// <remarks>
        /// This operation finds the index of the maximum value along the specified dimension.
        /// If `keepdim` is `false` (default), the output tensor has one fewer dimension than the `input`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1.0f, 2.0f, 3.0f, 3.5f, -2.0f, 3.0f });
        /// // Get indices of max values along dimension 1
        /// var tensor = Functional.ArgMax(input, dim: 1);
        /// // Result: [2, 0]
        /// // When keepdim is true, the resulting tensor has the same rank as the input
        /// var tensor2 = Functional.ArgMax(input, dim: 1, keepdim: true);
        /// // Result: [[2], [0]]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimension in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ArgMax(FunctionalTensor input, int dim = 0, bool keepdim = false)
        {
            return FunctionalLayer.ArgMax(input, dim, keepdim, false);
        }

        /// <summary>
        /// Returns the indices of the minimum value of the elements of the `input` along a dimension.
        /// </summary>
        /// <remarks>
        /// This operation finds the index of the minimum value along the specified dimension.
        /// If `keepdim` is `false` (default), the output tensor has one fewer dimension than the `input`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1.0f, 2.0f, 3.0f, 3.5f, -2.0f, 3.0f });
        /// // Get indices of min values along dimension 1
        /// var tensor = Functional.ArgMin(input, dim: 1);
        /// // Result: [0, 1]
        /// // When keepdim is true, the resulting tensor has the same rank as the input
        /// var tensor2 = Functional.ArgMin(input, dim: 1, keepdim: true);
        /// // Result: [[0], [1]]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimension in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ArgMin(FunctionalTensor input, int dim = 0, bool keepdim = false)
        {
            return FunctionalLayer.ArgMin(input, dim, keepdim, false);
        }

        /// <summary>
        /// Returns the maximum value of the elements of the `input` tensor along the dimensions.
        /// </summary>
        /// <remarks>
        /// This operation computes the maximum value along the specified dimensions.
        /// If `keepdim` is `false` (default), this operation removes reduced dimensions from the output.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1.0f, 3.0f, 2.0f, 4.0f, 2.0f, 5.0f });
        /// var result = Functional.ReduceMax(input, new[] { 1 });
        /// // Result: [3.0, 5.0] (max values along dimension 1)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimensions to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimensions in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceMax(FunctionalTensor input, int[] dim, bool keepdim = false)
        {
            return FunctionalLayer.ReduceMax(input, Constant(dim), keepdim, false);
        }

        /// <summary>
        /// Returns the maximum value of the elements of the `input` tensor along the dimension.
        /// </summary>
        /// <remarks>
        /// This operation computes the maximum value along a single dimension.
        /// If `keepdim` is `false` (default), the operation removes the reduced dimension from the output.
        /// </remarks>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimension in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceMax(FunctionalTensor input, int dim, bool keepdim = false)
        {
            return ReduceMax(input, new[] { dim }, keepdim);
        }

        /// <summary>
        /// Returns the minimum value of the elements of the `input` tensor along the dimensions.
        /// </summary>
        /// <remarks>
        /// This operation computes the minimum value along the specified dimensions.
        /// If `keepdim` is `false` (default), this operation removes reduced dimensions from the output.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1.0f, 3.0f, 2.0f, 4.0f, 2.0f, 5.0f });
        /// var result = Functional.ReduceMin(input, new[] { 1 });
        /// // Result: [1.0, 2.0] (min values along dimension 1)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimensions to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimensions in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceMin(FunctionalTensor input, int[] dim, bool keepdim = false)
        {
            return FunctionalLayer.ReduceMin(input, Constant(dim), keepdim, false);
        }

        /// <summary>
        /// Returns the minimum value of the elements of the `input` tensor along the dimension.
        /// </summary>
        /// <remarks>
        /// This operation computes the minimum value along a single dimension.
        /// If `keepdim` is `false` (default), the operation removes the reduced dimension from the output.
        /// </remarks>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimension in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceMin(FunctionalTensor input, int dim, bool keepdim = false)
        {
            return ReduceMin(input, new[] { dim }, keepdim);
        }

        /// <summary>
        /// Returns the L1 norm of the elements of the `input` tensor along the dimensions.
        /// </summary>
        /// <remarks>
        /// This operation computes the L1 norm (sum of absolute values) along the specified dimensions.
        /// If `keepdim` is `false` (default), this operation removes reduced dimensions from the output.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 2), new[] { -1.0f, 2.0f, 3.0f, -4.0f });
        /// var result = Functional.ReduceL1(input, new[] { 1 });
        /// // Result: [3.0, 7.0] (|−1| + |2| = 3, |3| + |−4| = 7)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimensions to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimensions in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceL1(FunctionalTensor input, int[] dim, bool keepdim = false)
        {
            return FunctionalLayer.ReduceL1(input, Constant(dim), keepdim, false);
        }

        /// <summary>
        /// Returns the L1 norm of the elements of the `input` tensor along the dimension.
        /// </summary>
        /// <remarks>
        /// This operation computes the L1 norm (sum of absolute values) along a single dimension.
        /// If `keepdim` is `false` (default), the operation removes the reduced dimension from the output.
        /// </remarks>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimension in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceL1(FunctionalTensor input, int dim, bool keepdim = false)
        {
            return ReduceL1(input, new[] { dim }, keepdim);
        }

        /// <summary>
        /// Returns the L2 norm of the elements of the `input` tensor along the dimensions.
        /// </summary>
        /// <remarks>
        /// This operation computes the L2 norm (Euclidean norm: square root of sum of squares) along the specified dimensions.
        /// If `keepdim` is `false` (default), this operation removes reduced dimensions from the output.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 2), new[] { 3.0f, 4.0f, 1.0f, 2.0f });
        /// var result = Functional.ReduceL2(input, new[] { 1 });
        /// // Result: [5.0, 2.236] (√(3²+4²) = 5, √(1²+2²) ≈ 2.236)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimensions to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimensions in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceL2(FunctionalTensor input, int[] dim, bool keepdim = false)
        {
            return FunctionalLayer.ReduceL2(input, Constant(dim), keepdim, false);
        }

        /// <summary>
        /// Returns the L2 norm of the elements of the `input` tensor along the dimension.
        /// </summary>
        /// <remarks>
        /// This operation computes the L2 norm (Euclidean norm [square root of sum of squares]) along a single dimension.
        /// If `keepdim` is `false` (default), the operation removes the reduced dimension from the output.
        /// </remarks>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimension in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceL2(FunctionalTensor input, int dim, bool keepdim = false)
        {
            return ReduceL2(input, new[] { dim }, keepdim);
        }

        /// <summary>
        /// Returns the log of summed exponentials of the elements of the `input` tensor along the dimensions.
        /// </summary>
        /// <remarks>
        /// This operation computes `log(sum(exp(x)))` along the specified dimensions, providing numerical stability.
        /// Promotes `input` to float type if necessary.
        /// If `keepdim` is `false` (default), the operation removes reduced dimensions from the output.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 2), new[] { 1.0f, 2.0f, 3.0f, 4.0f });
        /// var result = Functional.ReduceLogSumExp(input, new[] { 1 });
        /// // Result: [2.313, 4.313] (log(e¹ + e²), log(e³ + e⁴))
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimensions to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimensions in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceLogSumExp(FunctionalTensor input, int[] dim, bool keepdim = false)
        {
            input = input.Float();
            return FunctionalLayer.ReduceLogSumExp(input, Constant(dim), keepdim, false);
        }

        /// <summary>
        /// Returns the log of summed exponentials of the elements of the `input` tensor along the dimension.
        /// </summary>
        /// <remarks>
        /// This operation computes `log(sum(exp(x)))` along a single dimension, providing numerical stability.
        /// Promotes `input` to float type if necessary.
        /// If `keepdim` is `false` (default), the operation removes the reduced dimension from the output.
        /// </remarks>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimension in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceLogSumExp(FunctionalTensor input, int dim, bool keepdim = false)
        {
            return ReduceLogSumExp(input, new[] { dim }, keepdim);
        }

        /// <summary>
        /// Returns the mean of the elements of the `input` tensor along the dimensions.
        /// </summary>
        /// <remarks>
        /// This operation computes the average value along the specified dimensions.
        /// Promotes `input` to float type if necessary.
        /// If `keepdim` is `false` (default), the operation removes reduced dimensions from the output.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f });
        /// var result = Functional.ReduceMean(input, new[] { 1 });
        /// // Result: [2.0, 5.0] (mean of each row)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimensions to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimensions in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceMean(FunctionalTensor input, int[] dim, bool keepdim = false)
        {
            input = input.Float();
            return FunctionalLayer.ReduceMean(input, Constant(dim), keepdim, false);
        }

        /// <summary>
        /// Returns the mean of the elements of the `input` tensor along the dimension.
        /// </summary>
        /// <remarks>
        /// This operation computes the average value along a single dimension.
        /// Promotes `input` to float type if necessary.
        /// If `keepdim` is `false` (default), the operation removes the reduced dimension from the output.
        /// </remarks>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimension in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceMean(FunctionalTensor input, int dim, bool keepdim = false)
        {
            return ReduceMean(input, new[] { dim }, keepdim);
        }

        /// <summary>
        /// Returns the product of the elements of the `input` tensor along the dimensions.
        /// </summary>
        /// <remarks>
        /// This operation multiplies all elements along the specified dimensions.
        /// If `keepdim` is `false` (default), the operation removes reduced dimensions from the output.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f });
        /// var result = Functional.ReduceProd(input, new[] { 1 });
        /// // Result: [6.0, 120.0] (1×2×3 = 6, 4×5×6 = 120)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimensions to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimensions in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceProd(FunctionalTensor input, int[] dim, bool keepdim = false)
        {
            return FunctionalLayer.ReduceProd(input, Constant(dim), keepdim, false);
        }

        /// <summary>
        /// Returns the product of the elements of the `input` tensor along the dimension.
        /// </summary>
        /// <remarks>
        /// This operation multiplies all elements along a single dimension.
        /// If `keepdim` is `false` (default), the operation removes the reduced dimension from the output.
        /// </remarks>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimension in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceProd(FunctionalTensor input, int dim, bool keepdim = false)
        {
            return ReduceProd(input, new[] { dim }, keepdim);
        }

        /// <summary>
        /// Returns the sum of the elements of the `input` tensor along the dimensions.
        /// </summary>
        /// <remarks>
        /// This operation adds all elements along the specified dimensions.
        /// If `keepdim` is `false` (default), the operation removes reduced dimensions from the output.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f });
        /// var result = Functional.ReduceSum(input, new[] { 1 });
        /// // Result: [6.0, 15.0] (1+2+3 = 6, 4+5+6 = 15)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimensions to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimensions in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceSum(FunctionalTensor input, int[] dim, bool keepdim = false)
        {
            return FunctionalLayer.ReduceSum(input, Constant(dim), keepdim, false);
        }

        /// <summary>
        /// Returns the sum of the elements of the `input` tensor along the dimension.
        /// </summary>
        /// <remarks>
        /// This operation adds all elements along a single dimension.
        /// If `keepdim` is `false` (default), the operation removes the reduced dimension from the output.
        /// </remarks>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimension in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceSum(FunctionalTensor input, int dim, bool keepdim = false)
        {
            return ReduceSum(input, new[] { dim }, keepdim);
        }

        /// <summary>
        /// Returns the sum of the square of the elements of the `input` tensor along the dimensions.
        /// </summary>
        /// <remarks>
        /// This operation computes the sum of squared values along the specified dimensions.
        /// If `keepdim` is `false` (default), the operation removes reduced dimensions from the output.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 2), new[] { 1.0f, 2.0f, 3.0f, 4.0f });
        /// var result = Functional.ReduceSumSquare(input, new[] { 1 });
        /// // Result: [5.0, 25.0] (1²+2² = 5, 3²+4² = 25)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimensions to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimensions in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceSumSquare(FunctionalTensor input, int[] dim, bool keepdim = false)
        {
            return FunctionalLayer.ReduceSumSquare(input, Constant(dim), keepdim, false);
        }

        /// <summary>
        /// Returns the sum of the square of the elements of the `input` tensor along the dimension.
        /// </summary>
        /// <remarks>
        /// This operation computes the sum of squared values along a single dimension.
        /// If `keepdim` is `false` (default), the operation removes the reduced dimension from the output.
        /// </remarks>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimension in the output. Default is `false`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceSumSquare(FunctionalTensor input, int dim, bool keepdim = false)
        {
            return ReduceSumSquare(input, new[] { dim }, keepdim);
        }

        /// <summary>
        /// Returns the variance of the elements of the `input` tensor along the dimensions.
        /// </summary>
        /// <remarks>
        /// This operation computes the variance along the specified dimensions.
        /// Promotes `input` to float type if necessary.
        /// If `keepdim` is `false` (default), the operation removes reduced dimensions from the output.
        /// The `correction` parameter adjusts degrees of freedom (the default is `1` for sample variance with Bessel's correction).
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f });
        /// var result = Functional.ReduceVariance(input, new[] { 1 });
        /// // Result: [1.0, 1.0] (variance of each row)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimensions to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimensions in the output. Default is `false`.</param>
        /// <param name="correction">The difference between the sample size and sample degrees of freedom. Defaults to Bessel's correction.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceVariance(FunctionalTensor input, int[] dim, bool keepdim = false, float correction = 1f)
        {
            input = input.Float();
            return FunctionalLayer.ReduceVariance(input, Constant(dim), keepdim, false, correction);
        }

        /// <summary>
        /// Returns the variance of the elements of the `input` tensor along the dimension.
        /// </summary>
        /// <remarks>
        /// This operation computes the variance along a single dimension.
        /// Promotes `input` to float type if necessary.
        /// If `keepdim` is `false` (default), the operation removes the reduced dimension from the output.
        /// The `correction` parameter adjusts degrees of freedom (the default is `1` for sample variance with Bessel's correction).
        /// </remarks>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension to reduce.</param>
        /// <param name="keepdim">Whether to keep the reduced dimensions in the output. Default is `false`.</param>
        /// <param name="correction">The difference between the sample size and sample degrees of freedom. Defaults to Bessel's correction.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor ReduceVariance(FunctionalTensor input, int dim, bool keepdim = false, float correction = 1f)
        {
            return ReduceVariance(input, new[] { dim }, keepdim, correction);
        }
    }
}
