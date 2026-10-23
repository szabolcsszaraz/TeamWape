using System;

namespace Unity.InferenceEngine
{
    public static partial class Functional
    {
        /// <summary>
        /// Returns the `input` cast to the data type element-wise.
        /// </summary>
        /// <remarks>
        /// This operation casts all elements of the `input` tensor to the specified data type.
        /// If the `input` already has the target data type, this operation returns the tensor unchanged.
        /// Supported data types are `Int` and `Float`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.5f, 2.7f, 3.2f });
        /// var result = Functional.Type(input, DataType.Int);
        /// // Result: [1, 2, 3] (float values truncated to integers)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="dataType">The data type.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Type(this FunctionalTensor input, DataType dataType)
        {
            if (input.dataType == dataType)
                return input;
            return FunctionalLayer.Cast(input, dataType);
        }

        /// <summary>
        /// Returns the `input` cast to integers element-wise.
        /// </summary>
        /// <remarks>
        /// This operation casts all elements of the `input` tensor to integer type.
        /// Truncates floating-point values toward zero. If the `input` is already integer type, it's returned unchanged.
        /// Equivalent to calling `Type(input, DataType.Int)`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.5f, -2.7f, 3.2f });
        /// var result = Functional.Int(input);
        /// // Result: [1, -2, 3] (truncated toward zero)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Int(this FunctionalTensor input)
        {
            return input.Type(DataType.Int);
        }

        /// <summary>
        /// Returns the `input` cast to floats element-wise.
        /// </summary>
        /// <remarks>
        /// This operation casts all elements of the `input` tensor to floating-point type.
        /// Converts integer values to their float equivalents. If the `input` is already float type, it is returned unchanged.
        /// Equivalent to calling `Type(input, DataType.Float)`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1, 2, 3 });
        /// var result = Functional.Float(input);
        /// // Result: [1.0, 2.0, 3.0] (converted to float)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Float(this FunctionalTensor input)
        {
            return input.Type(DataType.Float);
        }

        // Promotes `a` and `b` to the same type that is the lowest type compatible with both.
        static (FunctionalTensor, FunctionalTensor) PromoteTypes(FunctionalTensor a, FunctionalTensor b)
        {
            return a.dataType == b.dataType ? (a, b) : (a.Float(), b.Float());
        }

        // Asserts if any of the input tensors have a type different to a type.
        static void DeclareType(DataType dataType, params FunctionalTensor[] tensors)
        {
            for (var i = 0; i < tensors.Length; i++)
                Logger.AssertIsTrue(tensors[i].dataType == dataType, "FunctionalTensor has incorrect type.");
        }

        static void DeclareRank(FunctionalTensor tensor, int rank)
        {
            tensor.shape.DeclareRank(rank);
        }
    }
}
