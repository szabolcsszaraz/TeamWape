using System;
using UnityEngine;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// A binary representation of a Sentis asset.
    /// </summary>
    /// <remarks>
    /// Represents the full description of the <see cref="Model" />, including inputs, outputs, constants, layers, metadata, and weights, in binary format.
    /// </remarks>
    /// <example>
    /// <para>This script loads the `ModelAsset` into a `Model` and creates a `Worker` with the `Model`. The `Worker` will run inference on the `Model`.</para>
    /// <code>
    /// public class MyScript : MonoBehaviour
    /// {
    ///     public ModelAsset modelAsset;
    ///     Model m_Model;
    ///     Worker m_Worker;
    ///     ...
    ///
    ///     void Start()
    ///     {
    ///         // Load the binary asset and create a Worker
    ///         m_Model = ModelLoader.Load(modelAsset);
    ///         m_Worker = new Worker(m_Model, BackendType.GPUCompute);
    ///         ...
    ///     }
    /// }
    /// </code>
    /// </example>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    [PreferBinarySerialization]
    public class ModelAsset : ScriptableObject
    {
        /// <summary>
        /// The serialized binary data for the input descriptions, constant descriptions, layers, outputs, and metadata of the model.
        /// </summary>
        [SerializeField]
        internal ModelAssetData modelAssetData;

        /// <summary>
        /// The serialized binary data for the constant weights of the model, split into chunks.
        /// </summary>
        [SerializeField]
        internal ModelAssetWeightsData[] modelWeightsChunks;
    }
}
