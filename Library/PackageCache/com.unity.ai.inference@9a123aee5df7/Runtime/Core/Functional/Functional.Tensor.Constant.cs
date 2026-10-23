namespace Unity.InferenceEngine
{
    public static partial class Functional
    {
        /// <summary>
        /// Returns a functional tensor with shape and data taken from a tensor.
        /// </summary>
        /// <remarks>
        /// This operation creates a <see cref="FunctionalTensor"/> from an existing `Tensor` object.
        /// You can use the created functional tensor in <see cref="FunctionalGraph"/> construction.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var tensor1 = new Tensor<float>(new TensorShape(2, 3), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f });
        /// // Create a functional tensor from the existing tensor
        /// var tensor2 = Functional.Constant(tensor1);
        /// ]]></code>
        /// </example>
        /// <param name="tensor">The tensor to use as the source.</param>
        /// <returns>The functional tensor.</returns>
        public static FunctionalTensor Constant(Tensor tensor)
        {
            return FunctionalTensor.FromTensor(tensor);
        }

        /// <summary>
        /// Returns an integer tensor.
        /// </summary>
        /// <remarks>
        /// This operation creates an integer tensor with the specified `shape` and `values`.
        /// Ensure the values array contains enough elements to fill the tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Create an int tensor of shape [2, 3]
        /// var tensor = Functional.Constant(new TensorShape(2, 3), new[] { 1, 2, 3, 4, 5, 6 });
        /// ]]></code>
        /// </example>
        /// <param name="shape">The shape of the tensor.</param>
        /// <param name="values">The values of the element.</param>
        /// <returns>The tensor.</returns>
        public static FunctionalTensor Constant(TensorShape shape, int[] values)
        {
            var constantTensor = new ConstantTensor(shape, values);
            return new FunctionalTensor(constantTensor.GetPartialTensor(), new ConstantNode(constantTensor));
        }

        /// <summary>
        /// Returns a scalar integer tensor.
        /// </summary>
        /// <remarks>
        /// This operation creates a scalar integer tensor (rank `0` - containing a single value).
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Create a scalar integer tensor with value 42
        /// var scalar = Functional.Constant(42);
        /// ]]></code>
        /// </example>
        /// <param name="value">The value of the element.</param>
        /// <returns>The tensor.</returns>
        public static FunctionalTensor Constant(int value)
        {
            return Constant(new TensorShape(), new[] { value });
        }

        /// <summary>
        /// Returns a 1D integer tensor.
        /// </summary>
        /// <remarks>
        /// This operation creates a 1D integer tensor from an array of values.
        /// The array length determines the tensor shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Create a 1D tensor of shape [5]
        /// var vector = Functional.Constant(new[] { 1, 2, 3, 4, 5 });
        /// ]]></code>
        /// </example>
        /// <param name="values">The values of the elements.</param>
        /// <returns>The tensor.</returns>
        public static FunctionalTensor Constant(int[] values)
        {
            return Constant(new TensorShape(values.Length), values);
        }

        /// <summary>
        /// Returns a float tensor.
        /// </summary>
        /// <remarks>
        /// This operation creates a float tensor with the specified `shape` and `values`.
        /// Ensure the values array contains enough elements to fill the tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Create a float tensor of shape [2, 3]
        /// var tensor = Functional.Constant(new TensorShape(2, 3), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f });
        /// ]]></code>
        /// </example>
        /// <param name="shape">The shape of the tensor.</param>
        /// <param name="values">The values of the element.</param>
        /// <returns>The tensor.</returns>
        public static FunctionalTensor Constant(TensorShape shape, float[] values)
        {
            var constantTensor = new ConstantTensor(shape, values);
            return new FunctionalTensor(constantTensor.GetPartialTensor(), new ConstantNode(constantTensor));
        }

        /// <summary>
        /// Returns a scalar float tensor.
        /// </summary>
        /// <remarks>
        /// This operation creates a scalar float tensor (rank `0` - containing a single value).
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Create a scalar float tensor with value 3.14
        /// var scalar = Functional.Constant(3.14f);
        /// ]]></code>
        /// </example>
        /// <param name="value">The value of the element.</param>
        /// <returns>The tensor.</returns>
        public static FunctionalTensor Constant(float value)
        {
            return Constant(new TensorShape(), new[] { value });
        }

        /// <summary>
        /// Returns a 1D float tensor.
        /// </summary>
        /// <remarks>
        /// This operation creates a 1D float tensor from an array of values.
        /// The array length determines the tensor shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Create a 1D tensor with shape [5]
        /// var vector = Functional.Constant(new[] { 1.0f, 2.5f, 3.7f, 4.2f, 5.1f });
        /// ]]></code>
        /// </example>
        /// <param name="values">The values of the elements.</param>
        /// <returns>The tensor.</returns>
        public static FunctionalTensor Constant(float[] values)
        {
            return Constant(new TensorShape(values.Length), values);
        }
    }
}
