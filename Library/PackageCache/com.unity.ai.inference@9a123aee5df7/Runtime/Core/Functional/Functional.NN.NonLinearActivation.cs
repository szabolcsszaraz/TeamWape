using System;

namespace Unity.InferenceEngine
{
    public static partial class Functional
    {
        /// <summary>
        /// Returns relu(input) element-wise.
        /// </summary>
        /// <remarks>
        /// This operation applies the Rectified Linear Unit (ReLU) activation function element-wise.
        /// ReLU is defined as `max(0, x)`, returning the `input` for positive values and zero for negative values.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -2.0f, -1.0f, 0.0f, 1.0f, 2.0f });
        /// var result = Functional.Relu(input);
        /// // Result: [0.0, 0.0, 0.0, 1.0, 2.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Relu(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Relu(input);
        }

        /// <summary>
        /// Returns hardswish(input) element-wise.
        /// </summary>
        /// <remarks>
        /// This operation applies the Hard Swish activation function element-wise.
        /// Hard Swish is defined as `x * relu6(x + 3) / 6`.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -3.0f, -1.0f, 0.0f, 1.0f, 3.0f });
        /// var result = Functional.HardSwish(input);
        /// // Result: [0.0, -0.333, 0.0, 0.666, 3.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor HardSwish(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.HardSwish(input);
        }

        /// <summary>
        /// Returns swish(input) element-wise.
        /// </summary>
        /// <remarks>
        /// This operation applies the Swish activation function element-wise.
        /// Swish is defined as ` x / (1 + e^(−αx))`.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -3.0f, -1.0f, 0.0f, 1.0f, 3.0f });
        /// var result = Functional.Swish(input);
        /// // Result: [-0.142, -0.269, 0.000, 0.731, 2.858]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="alpha">The alpha value for the swish. Default is `1.0`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Swish(FunctionalTensor input, float alpha = 1.0f)
        {
            input = input.Float();
            return FunctionalLayer.Swish(input, alpha);
        }

        /// <summary>
        /// Returns relu6(input) element-wise.
        /// </summary>
        /// <remarks>
        /// This operation applies the ReLU6 activation function element-wise.
        /// ReLU6 is defined as `min(max(0, x), 6)`, clamping values to the range `[0, 6]`.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -2.0f, 0.0f, 3.0f, 6.0f, 8.0f });
        /// var result = Functional.Relu6(input);
        /// // Result: [0.0, 0.0, 3.0, 6.0, 6.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Relu6(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Relu6(input);
        }

        /// <summary>
        /// Returns mish(input) element-wise.
        /// </summary>
        /// <remarks>
        /// This operation applies the Mish activation function element-wise.
        /// Mish is defined as `x * tanh(softplus(x))` or `x * tanh(ln(1 + e^x))`.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -2.0f, -1.0f, 0.0f, 1.0f, 2.0f });
        /// var result = Functional.Mish(input);
        /// // Result: [-0.252, -0.303, 0.0, 0.865, 1.943]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Mish(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Mish(input);
        }

        /// <summary>
        /// Returns elu(input) element-wise.
        /// </summary>
        /// <remarks>
        /// This operation applies the Exponential Linear Unit (ELU) activation function element-wise.
        /// ELU is defined as `x if x > 0, else alpha * (e^x - 1)`.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -2.0f, -1.0f, 0.0f, 1.0f, 2.0f });
        /// var result = Functional.Elu(input, alpha: 1.0f);
        /// // Result: [-0.864, -0.632, 0.0, 1.0, 2.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="alpha">The alpha value for the elu. Default is `1.0`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Elu(FunctionalTensor input, float alpha = 1.0f)
        {
            input = input.Float();
            return FunctionalLayer.Elu(input, alpha);
        }

        /// <summary>
        /// Returns selu(input) element-wise.
        /// </summary>
        /// <remarks>
        /// This operation applies the Scaled Exponential Linear Unit (SELU) activation function element-wise.
        /// SELU is defined as `scale * (x if x > 0, else alpha * (e^x - 1))`, with fixed `scale ≈ 1.0507` and `alpha ≈ 1.6733`.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -2.0f, -1.0f, 0.0f, 1.0f, 2.0f });
        /// var result = Functional.Selu(input);
        /// // Result: [-1.520, -1.111, 0.0, 1.0507, 2.1014]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Selu(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Selu(input, 1.67326319217681884765625f, 1.05070102214813232421875f);
        }

        /// <summary>
        /// Returns celu(input) element-wise.
        /// </summary>
        /// <remarks>
        /// This operation applies the Continuously Differentiable Exponential Linear Unit (CELU) activation function element-wise.
        /// CELU is defined as `max(0, x) + min(0, alpha * (e^(x/alpha) - 1))`.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -2.0f, -1.0f, 0.0f, 1.0f, 2.0f });
        /// var result = Functional.Celu(input, alpha: 1.0f);
        /// // Result: [-0.864, -0.632, 0.0, 1.0, 2.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="alpha">The alpha value for the celu. Default is `1.0`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Celu(FunctionalTensor input, float alpha = 1.0f)
        {
            input = input.Float();
            return FunctionalLayer.Celu(input, alpha);
        }

        /// <summary>
        /// Returns leaky_relu(input) element-wise.
        /// </summary>
        /// <remarks>
        /// This operation applies the Leaky ReLU activation function element-wise.
        /// Leaky ReLU is defined as `x if x > 0, else negativeSlope * x`, allowing a small gradient for negative values.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -2.0f, -1.0f, 0.0f, 1.0f, 2.0f });
        /// var result = Functional.LeakyRelu(input, negativeSlope: 0.01f);
        /// // Result: [-0.02, -0.01, 0.0, 1.0, 2.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="negativeSlope">The negative slope value for the leaky relu. Default is `0.01`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor LeakyRelu(FunctionalTensor input, float negativeSlope = 0.01f)
        {
            input = input.Float();
            return FunctionalLayer.LeakyRelu(input, negativeSlope);
        }

        /// <summary>
        /// Returns PRelu(input) element-wise.
        /// </summary>
        /// <remarks>
        /// This operation applies the Parametric Rectified Linear Unit (PReLU) activation function element-wise.
        /// PReLU is defined as `x if x > 0, else weight * x`.
        /// Promotes `input` and `weight` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -2.0f, -1.0f, 0.0f, 1.0f, 2.0f });
        /// var weight = Functional.Constant(new[] { 0.25f });
        /// var result = Functional.PRelu(input, weight);
        /// // Result: [-0.5, -0.25, 0.0, 1.0, 2.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="weight">The weight tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor PRelu(FunctionalTensor input, FunctionalTensor weight)
        {
            input = input.Float();
            weight = weight.Float();
            return FunctionalLayer.PRelu(input, weight);
        }

        /// <summary>
        /// Returns gelu(input) element-wise.
        /// </summary>
        /// <remarks>
        /// This operation applies the Gaussian Error Linear Unit (GELU) activation function element-wise.
        /// GELU is defined as `x * Φ(x)`, where `Φ(x)` is the cumulative distribution function of the standard normal distribution.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -2.0f, -1.0f, 0.0f, 1.0f, 2.0f });
        /// var result = Functional.Gelu(input);
        /// // Result: [-0.0454, -0.1587, 0.0, 0.8413, 1.9546]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Gelu(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Gelu(input);
        }

        /// <summary>
        /// Returns softsign(input) element-wise.
        /// </summary>
        /// <remarks>
        /// This operation applies the Softsign activation function element-wise.
        /// Softsign is defined as `x / (1 + |x|)`, producing outputs in the range `(-1, 1)`.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -2.0f, -1.0f, 0.0f, 1.0f, 2.0f });
        /// var result = Functional.Softsign(input);
        /// // Result: [-0.666, -0.5, 0.0, 0.5, 0.666]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Softsign(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Softsign(input);
        }

        /// <summary>
        /// Returns softplus(input) element-wise.
        /// </summary>
        /// <remarks>
        /// This operation applies the Softplus activation function element-wise.
        /// Softplus is defined as `ln(1 + e^x)`.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -2.0f, -1.0f, 0.0f, 1.0f, 2.0f });
        /// var result = Functional.Softplus(input);
        /// // Result: [0.126, 0.313, 0.693, 1.313, 2.126]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Softplus(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Softplus(input);
        }

        /// <summary>
        /// Returns softmax(input) element-wise along a dimension.
        /// </summary>
        /// <remarks>
        /// This operation applies the Softmax function along the specified dimension.
        /// Softmax is defined as `e^(x_i) / sum(e^(x_j))` for all `j` in the dimension, converting values to a probability distribution that sums to 1.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, 2.0f, 3.0f });
        /// var result = Functional.Softmax(input, dim: 0);
        /// // Result: [0.0900, 0.2447, 0.6652] (sums to 1.0)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension along which to calculate the softmax. Default is `-1`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Softmax(FunctionalTensor input, int dim = -1)
        {
            input = input.Float();
            return FunctionalLayer.Softmax(input, dim);
        }

        /// <summary>
        /// Returns log(softmax(input)) element-wise along a specified dimension.
        /// </summary>
        /// <remarks>
        /// This operation applies the log of the Softmax function along the specified dimension (`dim`).
        /// Log Softmax is defined as `x_i - log(sum(e^(x_j)))` for all `j` in the dimension.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, 2.0f, 3.0f });
        /// var result = Functional.LogSoftmax(input, dim: 0);
        /// // Result: [-2.407, -1.407, -0.407]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dim">The dimension along which to calculate the softmax. Default is `-1`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor LogSoftmax(FunctionalTensor input, int dim = -1)
        {
            input = input.Float();
            return FunctionalLayer.LogSoftmax(input, dim);
        }

        /// <summary>
        /// Returns sigmoid(input) element-wise.
        /// </summary>
        /// <remarks>
        /// This operation applies the Sigmoid activation function element-wise.
        /// Sigmoid is defined as `1 / (1 + e^(-x))`, mapping values to the range `(0, 1)`.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -2.0f, -1.0f, 0.0f, 1.0f, 2.0f });
        /// var result = Functional.Sigmoid(input);
        /// // Result: [0.119, 0.268, 0.5, 0.731, 0.880]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Sigmoid(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Sigmoid(input);
        }

        /// <summary>
        /// Returns hard_sigmoid(input) element-wise.
        /// </summary>
        /// <remarks>
        /// This operation applies the Hard Sigmoid activation function element-wise.
        /// Hard Sigmoid is defined as `max(0, min(1, x/6 + 0.5))`, providing a piecewise linear approximation to <see cref="Sigmoid"/>.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -3.0f, -1.0f, 0.0f, 1.0f, 3.0f });
        /// var result = Functional.HardSigmoid(input);
        /// // Result: [0.0, 0.333, 0.5, 0.666, 1.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor HardSigmoid(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.HardSigmoid(input, 1 / 6f, 0.5f);
        }

        /// <summary>
        /// Returns the result of computing the mean variance on the second dimension of the `input` tensor and normalizes it according to the weight and bias.
        /// </summary>
        /// <remarks>
        /// This operation applies Batch Normalization to the `input` tensor.
        /// It normalizes the `input` using pre-computed `runningMean` and `runningVar` statistics, then scales and shifts by learned `weight` and `bias` parameters.
        /// The formula is: `(input - runningMean) / sqrt(runningVar + eps) * weight + bias`.
        /// Promotes all tensors to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, 2.0f, 3.0f, 4.0f });
        /// var runningMean = Functional.Constant(new[] { 2.5f });
        /// var runningVar = Functional.Constant(new[] { 1.25f });
        /// var weight = Functional.Constant(new[] { 1.0f });
        /// var bias = Functional.Constant(new[] { 0.0f });
        /// // Normalizes input using the running statistics
        /// var result = Functional.BatchNorm(input, runningMean, runningVar, weight, bias);
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="runningMean">The mean values tensor.</param>
        /// <param name="runningVar">The variance values tensor.</param>
        /// <param name="weight">The weight tensor.</param>
        /// <param name="bias">The bias tensor.</param>
        /// <param name="eps">The epsilon value used to avoid division by zero. Default is `1.0e-5`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor BatchNorm(FunctionalTensor input, FunctionalTensor runningMean, FunctionalTensor runningVar, FunctionalTensor weight, FunctionalTensor bias, float eps = 1e-5f)
        {
            input = input.Float();
            runningMean = runningMean.Float();
            runningVar = runningVar.Float();
            weight = weight.Float();
            bias = bias.Float();
            return FunctionalLayer.BatchNormalization(input, weight, bias, runningMean, runningVar, eps);
        }

        /// <summary>
        /// Returns the result of computing the mean variance on the spatial dimensions of the `input` tensor and normalizes it according to the weight and bias.
        /// </summary>
        /// <remarks>
        /// This operation applies Instance Normalization to the `input` tensor.
        /// Instance normalization computes mean and variance statistics separately for each instance and channel, normalizing over spatial dimensions.
        /// The normalization is then scaled and shifted by learned `weight` and `bias` parameters.
        /// Promotes all tensors to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 1, 2, 2), new[] { 1.0f, 2.0f, 3.0f, 4.0f }); // Shape: [1, 1, 2, 2]
        /// var weight = Functional.Constant(new[] { 1.0f });
        /// var bias = Functional.Constant(new[] { 0.0f });
        /// // Normalizes each instance independently across spatial dimensions
        /// var result = Functional.InstanceNorm(input, weight, bias);
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="weight">The weight tensor.</param>
        /// <param name="bias">The bias tensor.</param>
        /// <param name="eps">The epsilon value used to avoid division by zero. Default is `1.0e-5`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor InstanceNorm(FunctionalTensor input, FunctionalTensor weight, FunctionalTensor bias, float eps = 1e-5f)
        {
            input = input.Float();
            weight = weight.Float();
            bias = bias.Float();
            return FunctionalLayer.InstanceNormalization(input, weight, bias, eps);
        }

        /// <summary>
        /// Returns the result of computing Layer Normalization over a mini-batch of inputs.
        /// see paper: https://arxiv.org/abs/1607.06450
        /// </summary>
        /// <remarks>
        /// This operation applies Layer Normalization to the `input` tensor.
        /// Layer normalization computes mean and variance statistics across the feature dimension for each sample independently.
        /// The normalization is then scaled and shifted by learned `weight` and `bias` parameters.
        /// Promotes all tensors to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 4), new[] { 1.0f, 2.0f, 3.0f, 4.0f }); // Shape: [1, 4]
        /// var weight = Functional.Constant(new[] { 1.0f, 1.0f, 1.0f, 1.0f });
        /// var bias = Functional.Constant(new[] { 0.0f, 0.0f, 0.0f, 0.0f });
        /// // Normalizes across the feature dimension
        /// var result = Functional.LayerNorm(input, weight, bias);
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="weight">The weight tensor.</param>
        /// <param name="bias">The bias tensor.</param>
        /// <param name="eps">The epsilon value used to avoid division by zero. Default is `1.0e-5`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor LayerNorm(FunctionalTensor input, FunctionalTensor weight, FunctionalTensor bias, float eps = 1e-5f)
        {
            input = input.Float();
            weight = weight.Float();
            bias = bias.Float();
            return FunctionalLayer.LayerNormalization(input, weight, bias, eps);
        }

        /// <summary>
        /// Returns the result of normalizing the `input` tensor over local input regions.
        /// </summary>
        /// <remarks>
        /// This operation applies Local Response Normalization (LRN) to the `input` tensor.
        /// LRN performs normalization over local neighborhoods across channels using the formula: `output = input / (k + alpha * sum_of_squares)^beta`.
        /// Computes the sum over a local region of adjacent channels determined by `size`.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 1, 4), new[] { 1.0f, 2.0f, 3.0f, 4.0f }); // Shape: [1, 1, 4]
        /// // Normalizes each position using values from adjacent channels
        /// var result = Functional.LocalResponseNorm(input, size: 3);
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="size">The size of the regions used for normalization.</param>
        /// <param name="alpha">The multiplicative factor in the normalization. Default is `0.0001`.</param>
        /// <param name="beta">The exponent in the normalization. Default is `0.75`.</param>
        /// <param name="k">The additive factor in the normalization. Default is `1.0`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor LocalResponseNorm(FunctionalTensor input, int size, float alpha = 0.0001f, float beta = 0.75f, float k = 1.0f)
        {
            input = input.Float();

            // The note below is about how torch handles support asymmetry when size is even, vs what its doc says,
            // but the TLDR is we always follow ONNX to simplify our code as in practice it shouldn't change anything.

            // Torch has a slightly different semantics for the support than ONNX:
            // First the documentation seems to imply that when "size" is even, it doesn't include the point itself for which we do the LRN:
            // https://docs.pytorch.org/docs/stable/generated/torch.nn.LocalResponseNorm.html
            // Note the sum from c - n/2 to c + n/2. It would also appear to be always symmetric.
            //
            // This is not the case with ONNX: eg when size = 2, the support is the center point and next point,
            // no points before the center point would be used, but ONNX runtime fails on even sizes anyway.
            //
            // However looking at the implementation in pytorch:
            // https://github.com/pytorch/pytorch/blob/main/torch/nn/modules/normalization.py#L17
            // https://github.com/pytorch/pytorch/blob/main/torch/nn/functional.py#L2993
            // eg if input has rank 3, the sum of squares is calculated as such:
            //
            //      div = input.mul(input)
            //      div = div.unsqueeze(1)
            //      div = pad(div, (0, 0, size // 2, (size - 1) // 2))
            //      div = avg_pool2d(div, (size, 1), stride = 1).squeeze(1)
            //
            // note that because the avg_pool2d kernel size has "size" for support, size is always the true size
            // of the support and if even, it will not be symmetric. The padding will in fact be 0 on the "right",
            // so it effectively have the opposite "skew" of ONNX.
            //
            // Thus when size is odd, ONNX and PyTorch match even if PyTorch's doc says otherwise,
            // when size is even the asymmetric support size is forward skewed in ONNX
            // but backward skewed in PyTorch.
            // We ignore this slight difference and just use our ONNX implementation.

            return FunctionalLayer.LRN(input, alpha, beta, k, size);
        }

        /// <summary>
        /// Returns the result of computing Root Mean Square Normalization to the `input` tensor.
        /// </summary>
        /// <remarks>
        /// This operation applies Root Mean Square Normalization (RMSNorm) to the `input` tensor.
        /// RMSNorm normalizes the input across the last dimension by dividing by the root mean square.
        /// The formula is: `input / sqrt(mean(input²) + eps) * scale`, where `mean` is computed over the last dimension.
        /// Promotes all tensors to float type if necessary.
        /// See paper: <a href="https://arxiv.org/pdf/1910.07467">RMSNorm</a>
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 4), new[] { 4.0f, 2.0f, 1.0f, 4.0f });
        /// var scale = Functional.Constant(new[] { 2.0f, 1.0f, 0.5f, 1.0f });
        /// // Normalizes across the feature dimension
        /// var result = Functional.RMSNorm(input, scale);
        /// // Result: { 2.630, 0.658, 0.164, 1.315 }
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="scale">The scale tensor.</param>
        /// <param name="eps">The epsilon value used to avoid division by zero. Default is `1.0e-5`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor RMSNorm(FunctionalTensor input, FunctionalTensor scale, float eps = 1e-5f)
        {
            input = input.Float();
            scale = scale.Float();
            return FunctionalLayer.RMSNormalization(input, scale, eps);
        }
    }
}
