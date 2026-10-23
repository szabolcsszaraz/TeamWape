
namespace Unity.InferenceEngine
{
    /// <summary>
    /// Represents a tensor that is a result of functional tensor operations.
    /// </summary>
    /// <remarks>
    /// A `FunctionalTensor` represents a tensor node in a <see cref="FunctionalGraph"/>.
    /// Unlike a <see cref="Tensor"/>, which contains actual data, a `FunctionalTensor` represents the result of operations that will be executed later when the graph is compiled and run.
    ///
    /// Use `FunctionalTensor` objects to build computation graphs using the <see cref="Functional"/> API.
    /// The tensor may have a partially known shape where some dimensions are dynamic (unknown at graph construction time).
    /// Chain operations together using methods from the `Functional` class, or operators defined on `FunctionalTensor`.
    ///
    /// `FunctionalTensor` supports standard arithmetic operators, comparison operators, and indexing,
    /// making it convenient to express complex tensor computations using familiar syntax.
    /// </remarks>
    /// <example>
    /// <para>Create and manipulate <c>FunctionalTensor</c> objects</para>
    /// <code lang="cs"><![CDATA[
    /// // Create functional tensors
    /// var tensor1 = Functional.Constant(new TensorShape(new[] {2, 2}), new[] { 1.0f, 2.0f, 3.0f, 4.0f });
    /// // tensor1 is a functional tensor of shape [2, 2], of data type float, with values [[1.0, 2.0], [3.0, 4.0]].
    /// var tensor2 = tensor1 + 0.1f;
    /// // tensor2 is a functional tensor of shape [2, 2], of data type float, with values [[1.1, 2.1], [3.1, 4.1]].
    /// ]]></code>
    /// <para>See also [Workflow Example](xref:sentis-workflow-example).</para>
    /// </example>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    public partial class FunctionalTensor
    {
        PartialTensor m_PartialTensor;
        FunctionalNode m_Source;
        string m_Name;

        internal PartialTensor partialTensor => m_PartialTensor;
        internal DataType dataType => m_PartialTensor.dataType;
        internal DynamicTensorShape shape => m_PartialTensor.shape;
        internal FunctionalNode source => m_Source;
        internal string name => m_Name;

        internal FunctionalTensor(PartialTensor partialTensor, FunctionalNode source, string name = null)
        {
            m_PartialTensor = partialTensor;
            m_Source = source;
            m_Name = name;
        }

        internal void SetName(string name)
        {
            m_Name = name;
        }

        internal FunctionalTensor Copy()
        {
            return new FunctionalTensor(m_PartialTensor.Copy(), source, name);
        }

        internal static FunctionalTensor FromTensor(Tensor tensor)
        {
            var constantTensor = new ConstantTensor(tensor);
            var constantNode = new ConstantNode(constantTensor);
            return new FunctionalTensor(PartialTensor.FromTensor(tensor), constantNode);
        }

        /// <summary>
        /// Returns a string representation of the functional tensor with its data type and shape.
        /// </summary>
        /// <remarks>
        /// This method provides a human-readable representation of the tensor.
        /// If the shape is fully known (static), it displays the complete shape.
        /// If the shape contains dynamic (unknown) dimensions, it displays `(?)`.
        /// </remarks>
        /// <example>
        /// <para>Get a string representation of a <c>FunctionalTensor</c></para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(new[] { 2, 3 }), new[] { 2, 3, 4, 6, 7, 8 });
        /// tensor.ToString(); // Returns "Int(2, 3)"
        ///
        /// var graph = new FunctionalGraph();
        /// var input = graph.AddInput(DataType.Float, new DynamicTensorShape(-1, 4, 24));
        /// input.ToString(); // Returns "Float(?)"
        /// ]]></code>
        /// </example>
        /// <returns>A string representation of the functional tensor.</returns>
        public override string ToString()
        {
            if (shape.IsStatic())
                return $"{dataType}{shape.ToTensorShape()}";
            else
                return $"{dataType}(?)";
        }
    }
}
