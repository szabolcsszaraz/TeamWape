using System;

namespace Unity.InferenceEngine
{
    public static partial class Functional
    {
        /// <summary>
        /// Returns the matrix product `input @ other`.
        /// </summary>
        /// <remarks>
        /// This operation performs matrix multiplication between two tensors.
        /// For 2D tensors, this is standard matrix multiplication. For higher dimensional tensors,
        /// the operation runs as a batch over the leading dimensions.
        /// Promotes `input` and `other` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new TensorShape(2, 2), new[] { 1.0f, 2.0f, 3.0f, 4.0f });
        /// var b = Functional.Constant(new TensorShape(2, 2), new[] { 5.0f, 6.0f, 7.0f, 8.0f });
        /// var result = Functional.MatMul(a, b);
        /// // Result: [[19.0, 22.0], [43.0, 50.0]]
        /// // (1*5 + 2*7 = 19, 1*6 + 2*8 = 22, 3*5 + 4*7 = 43, 3*6 + 4*8 = 50)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor MatMul(FunctionalTensor input, FunctionalTensor other)
        {
            input = input.Float();
            other = other.Float();
            return FunctionalLayer.MatMul(input, other);
        }

        /// <summary>
        /// Performs a batch matrix-matrix product of matrices : `y = x @ a + b`.
        /// Uses the following tensors:
        /// * Bias tensor `B` with shape `(N)`.
        /// * Weight tensor `A` with shape `(K, N)`.
        /// * Input tensor `X` with shape `(..., M, K)`.
        /// * Output tensor `O` with shape `(..., M, N)`.
        /// </summary>
        /// <remarks>
        /// This operation performs matrix multiplication followed by bias addition: output = input @ weight + bias.
        /// The bias is broadcast-added to each row of the matrix multiplication result.
        /// Promotes all inputs to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 2), new[] { 1.0f, 2.0f, 3.0f, 4.0f });
        /// var weight = Functional.Constant(new TensorShape(2, 2), new[] { 0.5f, 0.5f, 0.5f, 0.5f });
        /// var bias = Functional.Constant(new[] { 1.0f, -1.0f });
        /// var result = Functional.AddBMM(input, weight, bias);
        /// // Result: [[2.5, -0.5], [4.5, 2.5]]
        /// // Matrix multiply: [[1.5, 1.5], [3.5, 3.5]], then add bias to each row
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor `X` with shape `(..., M, K)`.</param>
        /// <param name="weight">The weight tensor `A` with shape `(K, N)`.</param>
        /// <param name="bias">The bias tensor `B` with shape `(N)`.</param>
        /// <returns>The output tensor with shape `(..., M, N)`.</returns>
        public static FunctionalTensor AddBMM(FunctionalTensor input, FunctionalTensor weight, FunctionalTensor bias)
        {
            input = input.Float();
            weight = weight.Float();
            bias = bias.Float();
            return FunctionalLayer.Dense(input, weight, bias, Layers.FusableActivation.None);
        }
    }
}
