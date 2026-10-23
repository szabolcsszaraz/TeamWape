using System;
using System.Linq; // Select
using System.Collections.Generic;
using Unity.InferenceEngine.Compiler.Analyser;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Represents a Sentis neural network.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    public class Model
    {
        /// <summary>
        /// The version of the model. The value increments each time the data structure changes.
        /// </summary>
        public const int Version = 30;
        internal const int WeightsAlignment = 16;

        /// <summary>
        /// Represents an input to a model.
        /// </summary>
        public struct Input
        {
            /// <summary>
            /// The name of the input.
            /// </summary>
            public string name;

            /// <summary>
            /// The index of the input.
            /// </summary>
            public int index;

            /// <summary>
            /// The data type of the input data.
            /// </summary>
            public DataType dataType;

            /// <summary>
            /// The shape of the input, as `DynamicTensorShape`.
            /// </summary>
            public DynamicTensorShape shape;

            internal Input(string name, int index, DataType dataType, DynamicTensorShape shape)
            {
                this.name = name;
                this.index = index;
                this.dataType = dataType;
                this.shape = shape;
            }
        }

        /// <summary>
        /// Represents an output of a model.
        /// Use this struct to access or modify the outputs of a model.
        /// </summary>
        /// <remarks>
        /// An `Output` is defined by a name and an index.
        /// When the `Worker` runs the inference over the model, you can access a specific output either by its name or by its index to get the result values.
        /// See <see cref = "Worker" />.
        ///
        /// The index field identifies the output internally. Do not confuse it with the index (position) of the `Output` in the `List` <see cref = "Model.outputs" />.
        /// </remarks>
        /// <example>
        /// <para>Add an output to a model.</para>
        /// <code>
        /// var model = ModelLoader.Load(modelAsset);
        /// var output = new Model.Output { index = 1234, name = "output" };
        /// model.outputs.Add(output);
        /// </code>
        /// </example>
        public struct Output
        {
            /// <summary>
            /// The name of the output.
            /// </summary>
            public string name;

            /// <summary>
            /// The index of the output.
            /// </summary>
            public int index;

            internal Output(string name, int index)
            {
                this.name = name;
                this.index = index;
            }
        }

        /// <summary>
        /// The inputs of the model.
        /// </summary>
        public List<Input> inputs = new List<Input>();

        /// <summary>
        /// The outputs of the model.
        /// </summary>
        public List<Output> outputs = new List<Output>();

        /// <summary>
        /// The layers of the model.
        /// </summary>
        public List<Layer> layers = new List<Layer>();

        /// <summary>
        /// The constants of the model.
        /// </summary>
        public List<Constant> constants = new List<Constant>();

        /// <summary>
        /// The producer of the model, as a string.
        /// </summary>
        public string ProducerName = "Script";

        // Stores the names for the named param dimensions in the dynamic shapes.
        internal string[] symbolicDimNames;

        // Stores the data types and shapes for each tensor. These don't necessarily stay up to date if editing the model.
        internal Dictionary<int, DataType> dataTypes = new Dictionary<int, DataType>();
        internal Dictionary<int, DynamicTensorShape> shapes = new Dictionary<int, DynamicTensorShape>();

        /// <summary>
        /// Returns a string that represents the `Model`.
        /// </summary>
        /// <returns>String representation of model.</returns>
        public override string ToString()
        {
            // weights are not loaded for UI, recompute size
            var totalUniqueWeights = 0;
            return $"inputs: [{string.Join(", ", inputs.Select(i => $"{i.index} {i.shape} [{i.dataType}]"))}], " +
                $"outputs: [{string.Join(", ", outputs)}] " +
                $"\n{layers.Count} layers, {totalUniqueWeights:n0} weights: \n{string.Join("\n", layers.Select(i => $"{i.GetType()} ({i})"))}";
        }

        internal void ValidateInputTensorShape(Input input, TensorShape shape)
        {
            if (shape.rank != input.shape.rank)
            {
                D.LogWarning($"Given input shape: {shape} is not compatible with model input shape: {input.shape} for input: {input.index}");
                return;
            }

            for (var i = 0; i < shape.rank; i++)
            {
                if (input.shape[i] != shape[i])
                    D.LogWarning($"Given input shape: {shape} has different dimension from model input shape: {input.shape} for input: {input.index} at axis: {i}");
            }
        }

        /// <summary>
        /// Adds an output called `name` to the model.
        /// </summary>
        /// <param name="name">The name of the output.</param>
        /// <param name="index">The index of the output.</param>
        public void AddOutput(string name, int index)
        {
            outputs.Add(new Output { name = name, index = index });
        }

        // Infer the data types and shapes for the model tensors via the partial tensor inference.
        internal void InferDataTypesShapes()
        {
            dataTypes.Clear();
            shapes.Clear();
            var ctx = PartialInferenceAnalysis.InferModelPartialTensors(this);
            foreach (var kvp in ctx.m_PartialTensors)
            {
                dataTypes[kvp.Key] = kvp.Value.dataType;
                shapes[kvp.Key] = kvp.Value.shape;
            }
        }

        // Return the data type for a given tensor index, returns null if not found.
        internal DataType? GetDataType(int index)
        {
            if (dataTypes.TryGetValue(index, out var dataType))
                return dataType;
            return null;
        }

        // Return the dynamic tensor shape for a given tensor index, returns null if not found.
        internal DynamicTensorShape? GetShape(int index)
        {
            if (shapes.TryGetValue(index, out var shape))
                return shape;
            return null;
        }

        // Returns a pretty string with the correct names for the dynamic dimensions.
        internal string DynamicShapeToString(DynamicTensorShape shape)
        {
            return shape.ToString(p => p < (symbolicDimNames?.Length ?? 0) ? symbolicDimNames[p] : "d" + p);
        }

        // Returns a pretty string with the correct names for the dynamic dimensions.
        internal string PartialTensorToString(PartialTensor partialTensor)
        {
            return partialTensor.ToString(p => p < (symbolicDimNames?.Length ?? 0) ? symbolicDimNames[p] : "d" + p);
        }
    }
}
