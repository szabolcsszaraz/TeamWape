using System.Collections.Generic;
using Unity.InferenceEngine.Compiler.Passes.Optimization;
using Unity.InferenceEngine.Graph;
using UnityEngine.Assertions;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Represents a model graph built with the <see cref="Functional"/> API.
    /// </summary>
    /// <remarks>
    /// <see cref="FunctionalGraph"/> defines a computation graph by adding inputs, applying <see cref="Functional"/> operations to <see cref="FunctionalTensor"/> values, and declaring outputs.
    /// Use <see cref="AddInput"/> or <see cref="AddInputs"/> to define graph inputs, then chain operations from the <see cref="Functional"/> class.
    /// Call <see cref="AddOutput"/> or <see cref="AddOutputs"/> to declare which tensors the graph produces, then <see cref="Compile"/> to build an optimized runtime <see cref="Model"/>.
    ///
    /// You can create new models from scratch, wrap or extend existing models with <see cref="Functional.Forward"/>, or build preprocessing and postprocessing pipelines.
    ///
    /// **Additional resources**
    ///
    /// - <see cref="Functional"/>
    /// - <see cref="FunctionalTensor"/>
    /// - <see cref="Model"/>
    /// - <see cref="DynamicTensorShape"/>
    /// - <see cref="TensorShape"/>
    /// - <see cref="AddInput"/>
    /// - <see cref="AddOutput"/>
    /// - <see cref="Compile"/>
    /// </remarks>
    /// <example>
    /// <para>Create a functional graph from scratch. Build a graph with inputs, operations, and outputs, then compile to a model.</para>
    /// <code lang="cs"><![CDATA[
    /// var graph = new FunctionalGraph();
    /// var x = graph.AddInput<float>(new TensorShape(6), "input_x");
    /// var y = graph.AddInput<float>(new TensorShape(6), "input_y");
    /// var prod = x * y;
    /// var reduce = Functional.ReduceSum(prod, dim: 0, keepdim: false);
    /// graph.AddOutput(reduce, "output");
    /// Model model = graph.Compile();
    /// ]]></code>
    /// <code lang="cs"><![CDATA[
    /// // Modify an existing model.
    /// // Declare a functional graph.
    /// var graph = new FunctionalGraph();
    /// // Get the input functional tensor from the graph with input data type and shape matching that of the original model input.
    ///var RGB = graph.AddInput(sourceModel, 0);
    /// // Apply f(x) = x^(1/2.2) element-wise to transform from RGB to sRGB.
    /// var sRGB = Functional.Pow(RGB, Functional.Constant(1 / 2.2f));
    /// // Apply f(x) = x * 2 - 1 element-wise to transform values from the range [0, 1] to the range [-1, 1].
    /// var sRGB_normalised = sRGB * 2 - 1;
    /// // Apply the forward method of the source model to the transformed functional input and add the outputs to the graph.
    /// var outputs = Functional.Forward(sourceModel, sRGB_normalised);
    /// graph.AddOutputs(outputs);
    /// // Compile the graph to return the final model.
    /// m_RuntimeModel = graph.Compile();
    /// ]]></code>
    /// </example>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    public class FunctionalGraph
    {
        List<InputNode> m_Inputs = new();
        List<FunctionalTensor> m_OutputTensors = new();

        /// <summary>
        /// Appends an input to the graph with the specified data type and dynamic shape.
        /// </summary>
        /// <remarks>
        /// Use this overload when the input has dynamic dimensions (for example, variable batch size or sequence length).
        /// </remarks>
        /// <param name="dataType">The data type of the input.</param>
        /// <param name="shape">The dynamic shape of the input.</param>
        /// <param name="name">The name of the input. If null, defaults to <c>input_</c> plus the input index.</param>
        /// <returns>The functional tensor input.</returns>
        /// <example>
        /// <para>Add an input with dynamic shape for variable batch or sequence length.</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = DynamicTensorShape.DynamicOfRank(2);
        /// var input = graph.AddInput(DataType.Float, shape, "input");
        /// ]]></code>
        /// </example>
        public FunctionalTensor AddInput(DataType dataType, DynamicTensorShape shape, string name = null)
        {
            var index = m_Inputs.Count;
            var inputNode = new InputNode(dataType, shape, name ?? $"input_{index}");
            m_Inputs.Add(inputNode);
            return new FunctionalTensor(PartialTensor.Create(dataType, shape), inputNode);
        }

        /// <summary>
        /// Appends an input to the graph with the specified data type and static shape.
        /// </summary>
        /// <remarks>
        /// Use this overload when all dimensions of the input are known at compile time.
        /// </remarks>
        /// <param name="dataType">The data type of the input.</param>
        /// <param name="shape">The static shape of the input.</param>
        /// <param name="name">The name of the input. If null, defaults to <c>input_</c> plus the input index.</param>
        /// <returns>The functional tensor input.</returns>
        /// <example>
        /// <para>Add an input with static shape.</para>
        /// <code lang="cs"><![CDATA[
        /// var input = graph.AddInput(DataType.Float, new TensorShape(1, 3, 224, 224), "image");
        /// ]]></code>
        /// </example>
        public FunctionalTensor AddInput(DataType dataType, TensorShape shape, string name = null)
        {
            return AddInput(dataType, new DynamicTensorShape(shape), name);
        }

        /// <summary>
        /// Appends an input to the graph with type inferred from <typeparamref name="T"/> and a dynamic shape.
        /// </summary>
        /// <remarks>
        /// The data type is derived from the generic parameter (for example, <c>float</c> for <c>AddInput&lt;float&gt;</c>). Use when you prefer type inference over explicit <see cref="DataType"/>.
        /// </remarks>
        /// <param name="shape">The dynamic shape of the input.</param>
        /// <param name="name">The name of the input. If null, defaults to <c>input_</c> plus the input index.</param>
        /// <typeparam name="T">The element type of the input (for example, <c>float</c> or <c>int</c>).</typeparam>
        /// <returns>The functional tensor input.</returns>
        /// <example>
        /// <para>Add an input with type inferred from the generic parameter.</para>
        /// <code lang="cs"><![CDATA[
        /// var input = graph.AddInput<float>(DynamicTensorShape.DynamicOfRank(1));
        /// ]]></code>
        /// </example>
        public FunctionalTensor AddInput<T>(DynamicTensorShape shape, string name = null) where T : unmanaged
        {
            return AddInput(AllocatorUtils.ToDataType<T>(), shape, name);
        }

        /// <summary>
        /// Appends an input to the graph with type inferred from <typeparamref name="T"/> and a static shape.
        /// </summary>
        /// <remarks>
        /// The data type is derived from the generic parameter (for example, <c>float</c> for <c>AddInput&lt;float&gt;</c>). Use when you prefer type inference over explicit <see cref="DataType"/>.
        /// </remarks>
        /// <param name="shape">The static shape of the input.</param>
        /// <param name="name">The name of the input. If null, defaults to <c>input_</c> plus the input index.</param>
        /// <typeparam name="T">The element type of the input (for example, <c>float</c> or <c>int</c>).</typeparam>
        /// <returns>The functional tensor input.</returns>
        /// <example>
        /// <para>Add inputs with type inferred from the generic parameter.</para>
        /// <code lang="cs"><![CDATA[
        /// var x = graph.AddInput<float>(new TensorShape(6), "input_x");
        /// var y = graph.AddInput<int>(new TensorShape(3), "indices");
        /// ]]></code>
        /// </example>
        public FunctionalTensor AddInput<T>(TensorShape shape, string name = null) where T : unmanaged
        {
            return AddInput(AllocatorUtils.ToDataType<T>(), shape, name);
        }

        /// <summary>
        /// Appends an input to the graph that matches a specific input of an existing model.
        /// </summary>
        /// <remarks>
        /// Use this overload when extending or wrapping an existing model. The new input has the same data type and shape as the model input at the given index. Pass the resulting tensor(s) to <see cref="Functional.Forward"/> to run the model.
        /// </remarks>
        /// <param name="model">The model whose input to match.</param>
        /// <param name="index">The index of the input in the model.</param>
        /// <param name="name">The name for this input. If null, uses the name from the model.</param>
        /// <returns>The functional tensor input.</returns>
        /// <example>
        /// <para>Add an input matching a model input, then run the model and add its output.</para>
        /// <code lang="cs"><![CDATA[
        /// var input = graph.AddInput(sourceModel, 0);
        /// var outputs = Functional.Forward(sourceModel, new[] { input });
        /// graph.AddOutput(outputs[0]);
        /// ]]></code>
        /// </example>
        public FunctionalTensor AddInput(Model model, int index, string name = null)
        {
            var modelInput = model.inputs[index];
            return AddInput(modelInput.dataType, modelInput.shape, name ?? modelInput.name);
        }

        /// <summary>
        /// Appends inputs to the graph that match all inputs of an existing model.
        /// </summary>
        /// <remarks>
        /// Use this overload when wrapping or extending a model. Each input matches the corresponding model input by index. Pass the returned array to <see cref="Functional.Forward"/> to run the model.
        /// </remarks>
        /// <param name="model">The model whose inputs to match.</param>
        /// <returns>The array of functional tensor inputs, one per model input.</returns>
        /// <example>
        /// <para>Add inputs matching all model inputs, run the model, and add a postprocessed output.</para>
        /// <code lang="cs"><![CDATA[
        /// var inputs = graph.AddInputs(sourceModel);
        /// var softmax = Functional.Softmax(outputs[0]);
        /// graph.AddOutput(softmax);
        /// ]]></code>
        /// </example>
        public FunctionalTensor[] AddInputs(Model model)
        {
            var inputTensors = new FunctionalTensor[model.inputs.Count];
            for (var i = 0; i < inputTensors.Length; i++)
                inputTensors[i] = AddInput(model, i);
            return inputTensors;
        }

        /// <summary>
        /// Appends an output to the graph from a functional tensor.
        /// </summary>
        /// <remarks>
        /// The output tensor must be derived from inputs or constants in the graph. If the tensor comes from an input or constant, it is cloned before being added. You must add at least one output before calling <see cref="Compile"/>.
        /// </remarks>
        /// <param name="output">The functional tensor to use as an output.</param>
        /// <param name="name">The name for the output. If null, defaults to <c>output_</c> plus the output index, or is inferred from the original model when the tensor is from a forward pass.</param>
        /// <example>
        /// <para>Add an output from a functional tensor.</para>
        /// <code lang="cs"><![CDATA[
        /// var x = graph.AddInput<float>(new TensorShape(6));
        /// var result = Functional.Relu(x);
        /// graph.AddOutput(result, "output");
        /// ]]></code>
        /// </example>
        public void AddOutput(FunctionalTensor output, string name = null)
        {
            if (output.source is InputNode or ConstantNode)
                output = output.Clone();

            if (name is not null)
                output.SetName(name);
            m_OutputTensors.Add(output.Copy());
        }

        /// <summary>
        /// Appends multiple outputs to the graph from the given functional tensors.
        /// </summary>
        /// <remarks>
        /// Equivalent to calling <see cref="AddOutput"/> for each tensor in the array. Use when the graph produces multiple outputs.
        /// You must add at least one output before calling <see cref="Compile"/>
        /// </remarks>
        /// <param name="outputs">The functional tensors to use as outputs.</param>
        /// <example>
        /// <para>Add multiple outputs at once.</para>
        /// <code lang="cs"><![CDATA[
        /// var x = graph.AddInput<float>(new TensorShape(6));
        /// var y = graph.AddInput<float>(new TensorShape(6));
        /// var reduce = Functional.ReduceSum(x * y, dim: 0, keepdim: false);
        /// var prod = x * y;
        /// graph.AddOutputs(reduce, prod);
        /// ]]></code>
        /// </example>
        public void AddOutputs(params FunctionalTensor[] outputs)
        {
            foreach (var output in outputs)
                AddOutput(output);
        }

        /// <summary>
        /// Compiles a graph with the given outputs and returns an optimized runtime model.
        /// </summary>
        /// <remarks>
        /// You must add at least one output to the graph using <see cref="AddOutput"/> or <see cref="AddOutputs"/> before calling this method. The returned model is optimized and ready to run with a <see cref="Worker"/>.
        /// </remarks>
        /// <returns>The compiled runtime model.</returns>
        /// <example>
        /// <para>Compile the graph to an optimized model.</para>
        /// <code lang="cs"><![CDATA[
        /// var x = graph.AddInput<float>(new TensorShape(6));
        /// var result = Functional.Relu(x);
        /// graph.AddOutput(result);
        /// Model model = graph.Compile();
        /// ]]></code>
        /// </example>
        public Model Compile()
        {
            var gm = BuildGraphModule();
            ModelOptimizer.OptimizeGraph(gm);
            var model = GraphConverter.GraphToModel(gm);
            return model;
        }

        /// <summary>
        /// Compiles a graph with the given outputs and returns an optimized runtime model.
        /// </summary>
        /// <remarks>
        /// Adds the specified tensors as outputs and then compiles.
        /// With this overload, don't add outputs to the graph separately or <see cref="AddOutputs"/>.
        /// The returned model is optimized and ready to run with a <see cref="Worker"/>.
        /// Throws an exception if outputs were already added to the graph.
        /// </remarks>
        /// <param name="outputs">The functional tensors to use as outputs.</param>
        /// <returns>The compiled runtime model.</returns>
        /// <exception cref="UnityEngine.Assertions.AssertionException">Thrown when outputs have already been added to the graph.</exception>
        /// <example>
        /// <para>Compile with inline outputs (no prior AddOutput call).</para>
        /// <code lang="cs"><![CDATA[
        /// var x = graph.AddInput<float>(new TensorShape(6));
        /// var y = graph.AddInput<float>(new TensorShape(6));
        /// var result = x * y;
        /// Model model = graph.Compile(result);
        /// ]]></code>
        /// </example>
        public Model Compile(params FunctionalTensor[] outputs)
        {
            Logger.AssertIsTrue(m_OutputTensors.Count == 0, "Graph outputs have already been added using FunctionalGraph.AddOutput. Call FunctionalGraph.Compile() with no arguments to compile the graph.");
            AddOutputs(outputs);
            return Compile();
        }

        enum NodeProgress
        {
            NotVisited,
            InProgress,
            Done
        }

        internal Model Build()
        {
            var gm = BuildGraphModule();
            return GraphConverter.GraphToModel(gm);
        }

        internal GraphModule BuildGraphModule()
        {
            // create empty model
            var gm = new GraphModule();
            var nodes = new Dictionary<FunctionalNode, Node>();
            var constantNameIndex = 0;

            // create for post order traversal algorithm
            var nodeStack = new Stack<FunctionalNode>(); // stack of nodes to inspect and then process
            var nodeProgress = new Dictionary<FunctionalNode, NodeProgress>(); // nodes which have been processed and added to the model

            // iterate inputs to ensure they are in the right order on the model
            foreach (var input in m_Inputs)
            {
                var inputNode = gm.Input(input.name, input.dataType, input.shape);
                nodes[input] = inputNode;
                nodeProgress[input] = NodeProgress.Done;
            }

            // queue nodes for the output expressions in reverse order
            for (var i = m_OutputTensors.Count - 1; i >= 0; i--)
                nodeStack.Push(m_OutputTensors[i].source);

            // push dependency nodes ahead of current node in stack
            // only process node once dependencies have been processed
            while (nodeStack.TryPeek(out var n))
            {
                var nProgress = nodeProgress.GetValueOrDefault(n, NodeProgress.NotVisited);
                if (nProgress == NodeProgress.InProgress)
                {
                    // add node to model
                    Logger.AssertIsTrue(n is not InputNode, "Input expression from incorrect source.");
                    if (n is ConstantNode constantNode)
                    {
                        var name = constantNameIndex.ToString();
                        constantNameIndex++;
                        gm.attributes[name] = constantNode.constant;
                        nodes[constantNode] = gm.graph.GetAttr(name);
                    }
                    else if (n is LayerNode layerNode)
                    {
                        var args = GraphUtils.MapArg(layerNode.args, fakeNode => nodes[((FakeNode)fakeNode).functionalTensor.source]);
                        nodes[layerNode] = gm.graph.CallFunction(layerNode.target, args);
                    }
                    else if (n is IndexerNode indexerNode)
                    {
                        nodes[indexerNode] = gm.graph.CallFunction("getitem", new Argument[] { nodes[indexerNode.layerNode], indexerNode.index });
                    }
                    nodeProgress[n] = NodeProgress.Done;
                    nodeStack.Pop();
                    continue;
                }

                if (nProgress == NodeProgress.Done)
                {
                    // node already added to model
                    nodeStack.Pop();
                    continue;
                }

                // node is not visited, iterate descendants
                nodeProgress[n] = NodeProgress.InProgress;

                void Visit(FunctionalNode node)
                {
                    var mProgress = nodeProgress.GetValueOrDefault(node, NodeProgress.NotVisited);
                    if (mProgress == NodeProgress.NotVisited)
                        nodeStack.Push(node);
                    else
                        Assert.IsTrue(mProgress != NodeProgress.InProgress, "Model graph has cycle");
                }

                if (n is LayerNode lNode)
                {
                    GraphUtils.VisitArg(lNode.args, node =>
                    {
                        var fakeNode = (FakeNode)node;
                        Visit(fakeNode.functionalTensor.source);
                    }, reverse: true);
                }
                else if (n is IndexerNode iNode)
                {
                    Visit(iNode.layerNode);
                }
            }

            var outputTensors = new Node[m_OutputTensors.Count];
            var outputNames = new string[m_OutputTensors.Count];
            for (var i = 0; i < outputTensors.Length; i++)
            {
                outputTensors[i] = nodes[m_OutputTensors[i].source];
                outputNames[i] = m_OutputTensors[i].name ?? $"output_{i}";
            }

            gm.Outputs(outputNames, outputTensors);

            return gm;
        }
    }
}
