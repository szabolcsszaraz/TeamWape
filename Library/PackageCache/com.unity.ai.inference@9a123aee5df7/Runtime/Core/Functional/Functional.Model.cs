using System;
using System.Collections.Generic;
using Unity.InferenceEngine.Graph;

namespace Unity.InferenceEngine
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    public static partial class Functional
    {
        /// <summary>
        /// Creates and returns an array of `FunctionalTensor` as the output of the forward pass of an existing model.
        ///
        /// Sentis will make destructive edits of the source model.
        /// </summary>
        /// <remarks>
        /// This operation integrates an existing model into a functional graph.
        /// Passes the provided tensors as inputs to the model,
        /// and the model's outputs are tensors that can be used in further computations.
        ///
        /// This method allows you to chain or compose models, enabling workflows such as:
        /// - Adding preprocessing operations before a model's inputs
        /// - Adding postprocessing operations after a model's outputs
        /// - Combining multiple models into a larger computational graph
        ///
        /// <b>Important</b>: This method makes destructive modifications to the source model for performance optimization.
        /// To keep the original model unchanged, use <see cref="ForwardWithCopy"/> instead.
        ///
        /// The number of input functional tensors must match the number of inputs expected by the model.
        /// The returned array will contain one functional tensor for each output of the model.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Load an existing model
        /// var model = ModelLoader.Load(modelAsset);
        ///
        /// // Create a functional graph with preprocessing
        /// var graph = new FunctionalGraph();
        /// var input = graph.AddInput(DataType.Float, new TensorShape(1, 3, 224, 224));
        ///
        /// // Apply preprocessing: normalize the input
        /// var mean = Functional.Constant(new TensorShape(1, 3, 1, 1), new[] { 0.485f, 0.456f, 0.406f });
        /// var std = Functional.Constant(new TensorShape(1, 3, 1, 1), new[] { 0.229f, 0.224f, 0.225f });
        /// var normalized = (input - mean) / std;
        ///
        /// // Pass the preprocessed input through the existing model
        /// var outputs = Functional.Forward(model, normalized);
        ///
        /// // Use the model outputs (apply softmax for classification)
        /// var probabilities = Functional.Softmax(outputs[0], dim: -1);
        ///
        /// graph.AddOutput(probabilities);
        /// var combinedModel = graph.Compile();
        /// ]]></code>
        /// </example>
        /// <param name="model">The model to use as the source.</param>
        /// <param name="inputs">The functional tensors to use as the inputs to the model.</param>
        /// <returns>The functional tensor array.</returns>
        public static FunctionalTensor[] Forward(Model model, params FunctionalTensor[] inputs)
        {
            return Forward(model, inputs, false);
        }

        /// <summary>
        /// Creates and returns an array of `FunctionalTensor` as the output of the forward pass of an existing model.
        ///
        /// Sentis will copy the source model and not make edits to it.
        /// </summary>
        /// <remarks>
        /// This operation integrates an existing model into a functional graph.
        /// Passes the provided tensors as inputs to the model,
        /// and the model's outputs are tensors that can be used in further computations.
        ///
        /// This method allows you to chain or compose models, enabling workflows such as:
        /// - Adding preprocessing operations before a model's inputs
        /// - Adding postprocessing operations after a model's outputs
        /// - Combining multiple models into a larger computational graph
        ///
        /// This method creates a copy of the source model before processing,
        /// ensuring that the original model remains unmodified. This is useful when you need to reuse the same
        /// model multiple times or preserve it for other purposes.
        /// <b>Note</b>: Copying incurs additional memory overhead and processing time compared to
        /// the destructive <see cref="Forward"/> method.
        ///
        /// The number of input functional tensors must match the number of inputs expected by the model.
        /// The returned array will contain one functional tensor for each output of the model.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Load an existing model that you want to preserve
        /// var baseModel = ModelLoader.Load(modelAsset);
        ///
        /// // Create two different functional graphs using the same base model
        /// var graph1 = new FunctionalGraph();
        /// var input1 = graph1.AddInput(DataType.Float, new TensorShape(1, 3, 224, 224));
        /// var outputs1 = Functional.ForwardWithCopy(baseModel, input1);
        /// var result1 = Functional.Relu(outputs1[0]); // Apply Relu postprocessing
        /// graph1.AddOutput(result1);
        /// var model1 = graph1.Compile();
        ///
        /// var graph2 = new FunctionalGraph();
        /// var input2 = graph2.AddInput(DataType.Float, new TensorShape(1, 3, 224, 224));
        /// var outputs2 = Functional.ForwardWithCopy(baseModel, input2);
        /// var result2 = Functional.Sigmoid(outputs2[0]); // Apply Sigmoid postprocessing
        /// graph2.AddOutput(result2);
        /// var model2 = graph2.Compile();
        ///
        /// // baseModel remains unchanged and can still be used independently
        /// ]]></code>
        /// </example>
        /// <param name="model">The model to use as the source.</param>
        /// <param name="inputs">The functional tensors to use as the inputs to the model.</param>
        /// <returns>The functional tensor array.</returns>
        public static FunctionalTensor[] ForwardWithCopy(Model model, params FunctionalTensor[] inputs)
        {
            return Forward(model, inputs, true);
        }

        internal static FunctionalTensor[] Forward(Model model, FunctionalTensor[] inputs, bool withCopy)
        {
            Logger.AssertIsTrue(inputs.Length == model.inputs.Count, "ModelOutputs.ValueError: inputs length does not equal model input count {0}, {1}", inputs.Length, model.inputs.Count);
            var expressions = new Dictionary<int, FunctionalTensor>();

            for (var i = 0; i < inputs.Length; i++)
                expressions[model.inputs[i].index] = inputs[i];

            foreach (var constant in model.constants)
            {
                var weights = constant.array;
                if (withCopy)
                    weights = weights.ToArray();
                var constantTensor = new ConstantTensor(constant.shape, constant.dataType, weights);
                var constantNode = new ConstantNode(constantTensor);
                expressions[constant.index] = new FunctionalTensor(constantTensor.GetPartialTensor(), constantNode);
            }

            foreach (var layer in model.layers)
            {
                var layerInputs = new Node[layer.inputs.Length];
                for (var i = 0; i < layerInputs.Length; i++)
                    layerInputs[i] = layer.inputs[i] == -1 ? null : new FakeNode(expressions[layer.inputs[i]]);

                var args = GraphConverter.LayerToArgs(layer, layerInputs);
                var layerOutputs = FromLayer(layer.opName, args);

                for (var i = 0; i < layer.outputs.Length; i++)
                {
                    if (layer.outputs[i] == -1)
                        continue;
                    expressions[layer.outputs[i]] = layerOutputs[i];
                }
            }

            var outputs = new FunctionalTensor[model.outputs.Count];
            for (var i = 0; i < model.outputs.Count; i++)
            {
                outputs[i] = expressions[model.outputs[i].index];
                outputs[i].SetName(model.outputs[i].name);
            }
            return outputs;
        }
    }
}
