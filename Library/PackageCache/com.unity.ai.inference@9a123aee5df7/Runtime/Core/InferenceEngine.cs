using System;
using UnityEngine;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Types of devices that Sentis uses to run inference on a neural network.
    /// </summary>
    /// <remarks>
    /// Sentis can run inference on GPU or on CPU. The performance depends on the size of the model and on the type of problem to solve. Smaller models may run faster on CPU.
    /// </remarks>
    /// <example>
    /// <para>When creating a <see cref="Worker"/>, specify which device to use for model inference.</para>
    /// <code>
    /// public ModelAsset model;
    /// Worker worker = new Worker(ModelLoader.Load(model), DeviceType.GPU);
    /// </code>
    /// </example>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    public enum DeviceType
    {
        /// <summary>
        /// Use GPU to run model inference.
        /// </summary>
        GPU = 1 << 8,

        /// <summary>
        /// Use CPU to run model inference.
        /// </summary>
        CPU = 1 << 9,
    }

    /// <summary>
    /// Types of backends that Sentis uses to run inference on a neural network.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    public enum BackendType
    {
        /// <summary>
        /// Use compute shaders on the GPU to run model inference.
        /// </summary>
        GPUCompute = 0 | DeviceType.GPU,

        /// <summary>
        /// Use pixel shaders on the GPU to run model inference.
        /// </summary>
        GPUPixel = 1 | DeviceType.GPU,

        /// <summary>
        /// Use Burst on the CPU to run model inference.
        /// </summary>
        CPU = 0 | DeviceType.CPU,
    }
}
