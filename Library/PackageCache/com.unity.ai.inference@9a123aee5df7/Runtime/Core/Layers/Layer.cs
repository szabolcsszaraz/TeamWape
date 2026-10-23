using System;
using System.Collections.Generic;
using SentisFlatBuffer;
using Unity.InferenceEngine.Google.FlatBuffers;
using Unity.Profiling;
using UnityEngine;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Represents the base class for all model layers.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    [Inputs(names = new[] { "input" })]
    [Outputs(names = new[] { "output" })]
    public abstract class Layer
    {
        /// <summary>
        /// The indices to use for the input tensors for a layer.
        /// </summary>
        public int[] inputs;

        /// <summary>
        /// The indices to use for all of the output tensors for a layer.
        /// </summary>
        public int[] outputs;

        /// <summary>
        /// ProfilerMarker for this layer
        /// </summary>
        public abstract ProfilerMarker profilerMarker { get; }

        internal virtual int OutputCount => 1;

        internal Layer() { }

        /// <summary>
        /// Initializes and returns a `Layer` from given arrays of input and output indices
        /// </summary>
        /// <param name="outputs">The indices array representing the outputs of this layer.</param>
        /// <param name="inputs">The indices array representing the inputs of this layer.</param>
        protected Layer(int[] outputs, int[] inputs)
        {
            this.outputs = outputs;
            this.inputs = inputs;
        }

        /// <summary>
        /// Infer the output partial tensors.
        /// </summary>
        internal abstract void InferPartial(PartialInferenceContext ctx);

        /// <summary>
        /// Executes the layer using the operations and variables from the `ExecutionContext`.
        /// </summary>
        /// <param name="ctx">The execution context with the backend and variables for the execution.</param>
        internal abstract void Execute(ExecutionContext ctx);

        /// <summary>
        /// Returns a string that represents the operation of the `Layer`.
        /// </summary>
        public abstract string opName { get; }

        internal abstract string category { get; }

        /// <summary>
        /// Whether the input tensor data is read on the CPU.
        /// For example, the tensor elements are used as a shape for reshaping.
        /// </summary>
        internal virtual bool IsInputCPURead(int i) => false;

        /// <summary>
        /// Whether the output tensor doesn't depend on the input tensor data at all.
        /// For example, this returns 'true' if only the shape or data type of the input tensor are used for calculating the output tensor.
        /// </summary>
        internal virtual bool IsInputNoDataDependency(int i) => false;
        internal virtual bool IsOutputList => false;
        internal abstract string[] GetInputNames();
        internal abstract string[] GetOutputNames();

        /// <summary>
        /// Returns a string that represents the `Layer`.
        /// </summary>
        /// <returns>The string representation of the `Layer`.</returns>
        public override string ToString()
        {
            return $"{opName} - index: {outputs[0]}, inputs: [{string.Join(", ", inputs)}]";
        }

        internal abstract void SerializeFields(FlatBufferBuilder builder, List<Offset<EValue>> values);
    }
}

namespace Unity.InferenceEngine.Layers
{
    /// <summary>
    /// Options for applying an activation at the end of executing a `FusedActivation` layer.
    /// </summary>
    enum FusableActivation
    {
        /// <summary>
        /// Use no activation function.
        /// </summary>
        None,
        /// <summary>
        /// Use `Relu` activation function: f(x) = max(0, x).
        /// </summary>
        Relu
    }
}
