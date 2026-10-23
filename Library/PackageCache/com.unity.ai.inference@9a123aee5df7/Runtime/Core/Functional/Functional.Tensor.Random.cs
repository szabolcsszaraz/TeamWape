using System;

namespace Unity.InferenceEngine
{
    public static partial class Functional
    {
        /// <summary>
        /// Returns an output generated with values `0` and `1` from a Bernoulli distribution.
        /// </summary>
        /// <remarks>
        /// This operation generates random binary values (`0` or `1`) based on Bernoulli trials.
        /// Each element in the `input` tensor represents the probability of generating a `1` at that position.
        /// The output has the same shape as the `input`.
        /// You can provide an optional `seed` for reproducible results.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var probs = Functional.Constant(new[] { 0.0f, 0.5f, 1.0f });
        /// var result = Functional.Bernoulli(probs, DataType.Int, seed: 42);
        /// // Result: Binary values (0 or 1) based on probabilities
        /// // [0, 0/1 (random), 1]
        /// ]]></code>
        /// </example>
        /// <param name="input">The probabilities used for generating the output values.</param>
        /// <param name="dataType">The data type of the output.</param>
        /// <param name="seed">(Optional) The seed value for the random number generator.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Bernoulli(FunctionalTensor input, DataType dataType = DataType.Int, int? seed = null)
        {
            return FunctionalLayer.Bernoulli(input, dataType, seed.HasValue, seed.GetValueOrDefault());
        }

        /// <summary>
        /// Returns an output generated from the multinomial probability distribution in the corresponding row of the `input`.
        /// </summary>
        /// <remarks>
        /// This operation samples indices from a multinomial probability distribution.
        /// Each row of the `input` tensor represents a probability distribution, and `numSamples` indices are drawn from each row.
        /// Promotes `input` to float type if necessary.
        /// The output shape is `[batch_size, numSamples]` containing the sampled indices.
        /// You can provide an optional `seed` for reproducible results.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var probs = Functional.Constant(new TensorShape(1, 3), new[] { 0.1f, 0.3f, 0.6f });
        /// var result = Functional.Multinomial(probs, numSamples: 5, seed: 42);
        /// // Result: Sampled indices (0, 1, or 2) based on probabilities
        /// // Shape: [1, 5], values like [[2, 2, 1, 2, 0]]
        /// ]]></code>
        /// </example>
        /// <param name="input">The probability distributions.</param>
        /// <param name="numSamples">The number of samples.</param>
        /// <param name="seed">(Optional) The seed value for the random number generator.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Multinomial(FunctionalTensor input, int numSamples, int? seed = null)
        {
            // TODO add replacement arg
            input = input.Float();
            return FunctionalLayer.Multinomial(input, numSamples, seed.HasValue, seed.GetValueOrDefault());
        }

        /// <summary>
        /// Returns a randomly selected values from a 1D `input` tensor.
        /// </summary>
        /// <remarks>
        /// This operation randomly selects elements from a 1D `input` tensor with uniform probability.
        /// The `input` must have rank `1`. The output has the specified shape with values drawn randomly from the `input`.
        /// Sampling is done with replacement, so the same value can appear multiple times.
        /// You can provide an optional `seed` for reproducible results.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var values = Functional.Constant(new[] { 10.0f, 20.0f, 30.0f, 40.0f });
        /// var result = Functional.RandomChoice(values, size: new[] { 2, 3 }, seed: 42);
        /// // Result: Random selections from values with shape [2, 3]
        /// // Example: [[20.0, 40.0, 10.0], [30.0, 20.0, 40.0]]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor to select random values from.</param>
        /// <param name="size">The shape of the output tensor.</param>
        /// <param name="seed">(Optional) The seed value for the random number generator.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor RandomChoice(FunctionalTensor input, int[] size, int? seed = null)
        {
            DeclareRank(input, 1);
            var shape = new TensorShape(size);
            if (shape.HasZeroDims())
                return Zeros(size, input.dataType);
            var inputSize = FunctionalLayer.Size(input);
            var index = Int(Floor(inputSize * Rand(new[] { shape.length }, seed)));
            return Gather(input, 0, index).Reshape(size);
        }

        /// <summary>
        /// Returns a randomly selected value from a 1D `input` tensor with probabilities given by the tensor `p`
        /// </summary>
        /// <remarks>
        /// This operation randomly selects elements from a 1D input tensor using specified probabilities.
        /// Both `input` and `p` must have rank `1` with matching lengths. The output has the specified shape with values drawn from the `input`.
        /// Each element in `p` represents the probability of selecting the corresponding element from input.
        /// Sampling is done with replacement, so the same value can appear multiple times.
        /// You can provide an optional `seed` for reproducible results.
        /// The output shape is `size`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var values = Functional.Constant(new[] { 10.0f, 20.0f, 30.0f });
        /// var probs = Functional.Constant(new[] { 0.1f, 0.3f, 0.6f }); // Higher probability for 30.0
        /// var result = Functional.RandomChoice(values, size: new[] { 5 }, p: probs, seed: 42);
        /// // Result: More likely to contain 30.0 than 10.0 or 20.0
        /// // Example: [30.0, 30.0, 20.0, 30.0, 10.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor to select random values from.</param>
        /// <param name="size">The shape of the output tensor.</param>
        /// <param name="p">The probabilities tensor corresponding to the values.</param>
        /// <param name="seed">(Optional) The seed value for the random number generator.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor RandomChoice(FunctionalTensor input, int[] size, FunctionalTensor p, int? seed = null)
        {
            DeclareRank(input, 1);
            DeclareRank(p, 1);
            var shape = new TensorShape(size);
            if (shape.HasZeroDims())
                return Zeros(size, input.dataType);
            var index = Multinomial(p.Unsqueeze(0), shape.length, seed).Squeeze(new[] { 0 });
            return Gather(input, 0, index).Reshape(size);
        }

        /// <summary>
        /// Returns an output generated by sampling a normal distribution.
        /// </summary>
        /// <remarks>
        /// This operation generates random values from a normal (Gaussian) distribution with specified `mean` and standard deviation `std`.
        /// The output has the specified shape with values drawn from `N(mean, std²)`.
        /// You can provide an optional `seed` for reproducible results.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var result = Functional.Normal(mean: 0.0f, std: 1.0f, size: new[] { 2, 3 }, seed: 42);
        /// // Result: Random values from standard normal distribution
        /// // Shape: [2, 3], values centered around 0 with std 1
        /// ]]></code>
        /// </example>
        /// <param name="mean">The mean of the normal distribution.</param>
        /// <param name="std">The standard deviation of the normal distribution.</param>
        /// <param name="size">The shape of the output tensor.</param>
        /// <param name="seed">(Optional) The seed value for the random number generator.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Normal(float mean, float std, int[] size, int? seed = null)
        {
            return FunctionalLayer.RandomNormal(mean, std, size, seed.HasValue, seed.GetValueOrDefault());
        }

        /// <summary>
        /// Returns an output generated by sampling a normal distribution with shape matching the `input` tensor.
        /// </summary>
        /// <remarks>
        /// This operation generates random values from a normal (Gaussian) distribution with specified `mean` and standard deviation `std`.
        /// The output has the same shape as the `input` tensor with values drawn from `N(mean, std²)`.
        /// You can provide an optional `seed` for reproducible results.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f }); // Shape: [2, 3]
        /// var result = Functional.NormalLike(mean: 0.0f, std: 1.0f, input: input, seed: 42);
        /// // Result: Random values from standard normal distribution with shape [2, 3]
        /// ]]></code>
        /// </example>
        /// <param name="mean">The mean of the normal distribution.</param>
        /// <param name="std">The standard deviation of the normal distribution.</param>
        /// <param name="input">The input tensor.</param>
        /// <param name="seed">(Optional) The seed value for the random number generator.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor NormalLike(float mean, float std, FunctionalTensor input, int? seed = null)
        {
            return FunctionalLayer.RandomNormalLike(input, mean, std, seed.HasValue, seed.GetValueOrDefault());
        }

        /// <summary>
        /// Returns an output generated by sampling a uniform distribution on the interval `[0, 1)`.
        /// </summary>
        /// <remarks>
        /// This operation generates random floating-point values uniformly distributed between `0` (inclusive) and `1` (exclusive).
        /// The output has the specified shape.
        /// You can provide an optional `seed` for reproducible results.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var result = Functional.Rand(size: new[] { 2, 3 }, seed: 42);
        /// // Result: Random values in [0, 1) with shape [2, 3]
        /// // Example: [[0.374, 0.950, 0.731], [0.598, 0.156, 0.155]]
        /// ]]></code>
        /// </example>
        /// <param name="size">The shape of the output tensor.</param>
        /// <param name="seed">(Optional) The seed value for the random number generator.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Rand(int[] size, int? seed = null)
        {
            return FunctionalLayer.RandomUniform(0, 1, size, seed.HasValue, seed.GetValueOrDefault());
        }

        /// <summary>
        /// Returns an output generated by sampling a uniform distribution on the interval `[0, 1)` with shape matching the `input` tensor.
        /// </summary>
        /// <remarks>
        /// This operation generates random floating-point values uniformly distributed between `0` (inclusive) and `1` (exclusive).
        /// The output has the same shape as the `input` tensor.
        /// You can provide an optional `seed` for reproducible results.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 2), new[] { 1.0f, 2.0f, 3.0f, 4.0f }); // Shape: [2, 2]
        /// var result = Functional.RandLike(input, seed: 42);
        /// // Result: Random values in [0, 1) with shape [2, 2]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="seed">(Optional) The seed value for the random number generator.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor RandLike(FunctionalTensor input, int? seed = null)
        {
            return FunctionalLayer.RandomUniformLike(input, 0, 1, seed.HasValue, seed.GetValueOrDefault());
        }

        /// <summary>
        /// Returns an output generated by sampling a uniform distribution of integers on the interval `[low, high)`.
        /// </summary>
        /// <remarks>
        /// This operation generates random integer values uniformly distributed between `low` (inclusive) and `high` (exclusive).
        /// The output has the specified shape and integer data type.
        /// You can provide an optional `seed` for reproducible results.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var result = Functional.RandInt(size: new[] { 2, 3 }, low: 0, high: 10, seed: 42);
        /// // Result: Random integers in [0, 10) with shape [2, 3]
        /// // Example: [[3, 7, 5], [9, 1, 2]]
        /// ]]></code>
        /// </example>
        /// <param name="size">The shape of the output tensor.</param>
        /// <param name="low">The inclusive minimum value of the interval.</param>
        /// <param name="high">The exclusive maximum value of the interval.</param>
        /// <param name="seed">The optional seed value for the random number generator.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor RandInt(int[] size, int low, int high, int? seed = null)
        {
            return Floor(FunctionalLayer.RandomUniform(low, high, size, seed.HasValue, seed.GetValueOrDefault())).Int();
        }

        /// <summary>
        /// Returns an output generated by sampling a uniform distribution of integers on the interval `[low, high)` with shape matching the `input` tensor.
        /// </summary>
        /// <remarks>
        /// This operation generates random integer values uniformly distributed between `low` (inclusive) and `high` (exclusive).
        /// The output has the same shape as the `input` tensor and integer data type.
        /// You can provide an optional `seed` for reproducible results.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 2), new[] { 1.0f, 2.0f, 3.0f, 4.0f }); // Shape: [2, 2]
        /// var result = Functional.RandIntLike(input, low: 0, high: 10, seed: 42);
        /// // Result: Random integers in [0, 10) with shape [2, 2]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="low">The inclusive minimum value of the interval.</param>
        /// <param name="high">The exclusive maximum value of the interval.</param>
        /// <param name="seed">(Optional) The seed value for the random number generator.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor RandIntLike(FunctionalTensor input, int low, int high, int? seed = null)
        {
            return Floor(FunctionalLayer.RandomUniformLike(input, low, high, seed.HasValue, seed.GetValueOrDefault())).Int();
        }

        /// <summary>
        /// Returns an output generated by sampling a standard normal distribution.
        /// </summary>
        /// <remarks>
        /// This operation generates random values from a standard normal distribution `(mean=0, std=1)`.
        /// The output has the specified shape with values drawn from `N(0, 1)`.
        /// You can provide an optional `seed` for reproducible results.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var result = Functional.RandN(size: new[] { 2, 3 }, seed: 42);
        /// // Result: Random values from standard normal distribution
        /// // Shape: [2, 3], values centered around 0 with std 1
        /// ]]></code>
        /// </example>
        /// <param name="size">The shape of the output tensor.</param>
        /// <param name="seed">(Optional) The seed value for the random number generator.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor RandN(int[] size, int? seed = null)
        {
            return FunctionalLayer.RandomNormal(0, 1, size, seed.HasValue, seed.GetValueOrDefault());
        }

        /// <summary>
        /// Returns an output generated by sampling a standard normal distribution with shape matching the `input` tensor.
        /// </summary>
        /// <remarks>
        /// This operation generates random values from a standard normal distribution `(mean=0, std=1)`.
        /// The output has the same shape as the `input` tensor with values drawn from `N(0, 1)`.
        /// You can provide an optional `seed` for reproducible results.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(2, 3), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f }); // Shape: [2, 3]
        /// var result = Functional.RandNLike(input, seed: 42);
        /// // Result: Random values from standard normal distribution with shape [2, 3]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="seed">(Optional) The seed value for the random number generator.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor RandNLike(FunctionalTensor input, int? seed = null)
        {
            return FunctionalLayer.RandomNormalLike(input, 0, 1, seed.HasValue, seed.GetValueOrDefault());
        }
    }
}
