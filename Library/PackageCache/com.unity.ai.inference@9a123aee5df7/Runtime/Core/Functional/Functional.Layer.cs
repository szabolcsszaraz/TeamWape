using System.Runtime.CompilerServices;
using Unity.InferenceEngine.Graph;

[assembly: InternalsVisibleTo("Unity.InferenceEngine.Tests")]

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Represents the static functional methods for model building and compilation.
    /// </summary>
    /// <remarks>
    /// The `Functional` class provides an API for creating and modifying neural network models by adding operators.
    /// This API enables you to build models from scratch with C# scripts.
    /// You can also edit existing models by adding operators before the model inputs or after its outputs.
    ///
    /// To use the `Functional` API, you work with <see cref="FunctionalGraph"/> objects to define model structure
    /// and <see cref="FunctionalTensor"/> objects to represent intermediate tensor values. A typical workflow is:
    /// 1. Create a <see cref="FunctionalGraph"/> instance.
    /// 2. Add inputs to the graph using <see cref="FunctionalGraph.AddInputs"/>.
    /// 3. Construct the graph using `Functional` operators.
    /// 4. <see cref="FunctionalGraph.Compile"/> the model to produce a runnable <see cref="Model"/>.
    ///
    /// The `Functional` class provides several categories of operators, which include:
    /// - Tensor creation and manipulation: `ARange`, `Full`, `Reshape`, `Transpose`, `Squeeze`, `Gather`, etc.
    /// - Mathematical operations: `Add`, `MatMul`, `LogicalXor`, etc.
    /// - Neural network layers: `Conv2D`, `AvgPool3D`, `HardSigmoid`, etc.
    /// - Reduction operations: `CumSum`, `ReduceMean`, `ArgMax`, etc.
    /// - Comparison operations: `GreaterEqual`, `Less`, etc.
    /// - More advanced operations: `NMS` for computer vision, `STFT`, and other spectral operations
    ///
    /// When editing existing models, use the <see cref="Forward"/> or <see cref="ForwardWithCopy"/> methods
    /// to incorporate the operations from a loaded model into your functional graph. The <see cref="Forward"/>
    /// method destructively modifies the source model for better performance, while <see cref="ForwardWithCopy"/>
    /// creates a copy to preserve the original.
    ///
    /// <b>Performance considerations</b>
    /// - The <see cref="FunctionalGraph.Compile"/> method is a slow operation that requires significant memory. It is recommended to run compilation offline and serialize the resulting model for runtime use.
    /// - Sentis applies automatic optimizations during compilation, so the actual operations executed during inference may differ from your API calls.
    /// - Operations return <see cref="FunctionalTensor"/> objects that represent computational graphs, not concrete data values. Inference computation occurs when you schedule the execution of the model on a <see cref="Worker"/>.
    ///
    /// The Functional API supports operator overloading on <see cref="FunctionalTensor"/> objects, allowing natural mathematical syntax.
    /// - Mathematical binary operators: `x + y` for `Add`, `x - y` for `Sub`, `x * y` for `Mul`, `x / y` for `Div`, `x % y` for `Remainder`
    /// - Mathematical unary operators: `+x` for `Clone`, `-x` for `Neg`
    /// - Logical operators: `x &amp; y` for `BitwiseAnd`, `x | y` for `BitwiseOr`, `x ^ y` for `BitwiseXor`, `~x` for `BitwiseNot`
    /// - Comparison operators: `x &gt; y` for `Greater`, `x &gt;= y` for `GreaterEqual`, `x &lt; y` for `Less`, `x &lt;= y` for `LessEqual`
    ///
    /// For a complete list of supported operations, refer to [Supported functional methods](xref:sentis-supported-functional-methods).
    /// </remarks>
    /// <example>
    /// <para>
    /// The following example demonstrates how to use the Functional API to create a model from scratch.
    /// </para>
    /// <code lang="cs"><![CDATA[
    /// var graph = new FunctionalGraph();
    /// var x = graph.AddInput(DataType.Float, new TensorShape(2, 3));
    /// var y = graph.AddInput(DataType.Float, new TensorShape(2, 3));
    /// var z = graph.AddInput(DataType.Float, new TensorShape(2, 3));
    /// var add = x + y;
    /// var sqrt = Functional.Sqrt(z);
    /// var atan2 = Functional.Atan2(add, sqrt);
    /// graph.AddOutput(atan2);
    /// var model = graph.Compile();
    /// ]]></code>
    /// </example>
    public static partial class Functional
    {
        /// <summary>
        /// Returns functional tensor array for a given op target and set of arguments.
        /// Node values in the argument array should be of type <see cref="FakeNode"/>, which are functional tensor wrappers.
        /// </summary>
        internal static FunctionalTensor[] FromLayer(string target, Argument[] args)
        {
            var output = FunctionalLayer.InferPartial(target, args);
            var layerNode = new LayerNode(target, args);

            if (output is PartialTensor partialTensor)
                return new[] { new FunctionalTensor(partialTensor, layerNode) };

            if (output is PartialTensor[] partialTensors)
            {
                // output is a list of tensors, insert indexer nodes equivalent to "getitem" functions in the graph
                var outputs = new FunctionalTensor[partialTensors.Length];
                for (var i = 0; i < partialTensors.Length; i++)
                    outputs[i] = new FunctionalTensor(partialTensors[i], new IndexerNode(layerNode, i));
                return outputs;
            }

            return null;
        }
    }
}
