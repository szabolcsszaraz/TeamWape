using System;

namespace Unity.InferenceEngine
{
    public static partial class Functional
    {
        /// <summary>
        /// Returns a one hot tensor with given index values that has zeros everywhere except where the index of last dimension matches the corresponding value of the `input` tensor, in which case it will be 1.
        /// </summary>
        /// <remarks>
        /// This operation creates a one-hot encoded tensor from integer indices.
        /// For each index value in the `input`, the corresponding position in the last dimension of the output is set to `1`, while all other positions are `0`.
        /// If `numClasses` is `-1`, the number of classes is inferred as `max(tensor) + 1`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var indices = Functional.Constant(new[] { 0, 2, 1 });
        /// var result = Functional.OneHot(indices, numClasses: 3);
        /// // Result: [[1, 0, 0], [0, 0, 1], [0, 1, 0]]
        /// // Shape: [3, 3]
        /// ]]></code>
        /// </example>
        /// <param name="tensor">The index tensor.</param>
        /// <param name="numClasses">Total number of classes. If set to `-1`, the number of classes will be inferred as one greater than the largest class value in the input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor OneHot(FunctionalTensor tensor, int numClasses = -1)
        {
            FunctionalTensor depthTensor;
            if (numClasses == -1)
                depthTensor = ReduceMax(tensor, 0) + 1;
            else
                depthTensor = Constant(numClasses);
            return FunctionalLayer.OneHot(tensor, depthTensor, Constant(new[] { 0, 1 }), -1, true);
        }
    }
}
