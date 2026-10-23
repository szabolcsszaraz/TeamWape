using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor.AssetImporters;
using UnityEngine;

[assembly: InternalsVisibleTo("Unity.InferenceEngine.Editor.Onnx")]

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Contains additional metadata about the ONNX model, stored in the ONNX file.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    [Serializable]
    public struct ONNXModelMetadata : ISerializationCallbackReceiver
    {
        /// <summary>
        /// Human-readable documentation for this model.
        /// </summary>
        public string DocString;
        /// <summary>
        /// A reverse-DNS name to indicate the model namespace or domain.
        /// </summary>
        public string Domain;
        /// <summary>
        /// Version number of the ONNX Intermediate Representation (IR) used in this model.
        /// </summary>
        public long IRVersion;
        /// <summary>
        /// Named metadata as dictionary.
        /// </summary>
        [NonSerialized]
        public Dictionary<string, string> MetadataProps;
        /// <summary>
        /// The name of the tool used to generate the model.
        /// </summary>
        public string ProducerName;
        /// <summary>
        /// The version of the generating tool.
        /// </summary>
        public string ProducerVersion;
        /// <summary>
        /// The version of the model itself, encoded in an integer.
        /// </summary>
        public long ModelVersion;

        [SerializeField]
        List<string> m_MetadataKeys;
        [SerializeField]
        List<string> m_MetadataValues;

        /// <inheritdoc/>
        public void OnBeforeSerialize()
        {
            if (MetadataProps == null)
                return;

            m_MetadataKeys = new List<string>(MetadataProps.Keys);
            m_MetadataValues = new List<string>(MetadataProps.Values);
        }

        /// <inheritdoc/>
        public void OnAfterDeserialize()
        {
            MetadataProps = new Dictionary<string, string>();
            for (int i = 0; i < m_MetadataKeys.Count; i++)
            {
                MetadataProps[m_MetadataKeys[i]] = m_MetadataValues[i];
            }
        }
    }

    /// <summary>
    /// Interface for receiving callbacks for metadata during ONNX import.
    /// </summary>
    /// <remarks>
    /// Implement this interface to import metadata from the ONNX model to Sentis.
    /// The ONNX import calls the `OnMetadataImported` method to capture the <see cref = "ONNXModelMetadata" />.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Class to read and store the metadata from the ONNX file
    /// public class MyMetadataCallbackHandler : IONNXMetadataImportCallbackReceiver
    /// {
    ///     public ONNXModelMetadata Metadata { get; private set; }
    ///
    ///     // Callback for metadata import. Member Metadata is of type ONNXModelMetadata.
    ///     public void OnMetadataImported(AssetImportContext ctx, ONNXModelMetadata metadata)
    ///     {
    ///         Metadata = metadata;
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="ONNXModelMetadata" />
    /// <seealso cref="AssetImportContext" />
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    public interface IONNXMetadataImportCallbackReceiver
    {
        /// <summary>
        /// This method is called when metadata is loaded during ONNX import, before the model is serialized.
        /// </summary>
        /// <param name="ctx">The context of the current import process.</param>
        /// <param name="metadata">The metadata fields of the imported ONNX file.</param>
        /// <remarks>
        /// Interface for handling metadata during the import process. It provides a way to store metadata for later access and optionally create additional assets using the <see cref = "AssetImportContext" />.
        /// in a way that it can be accessed later. The <see cref = "AssetImportContext" /> is provided to so that
        /// additional assets can be created and added to the import context, if necessary.
        /// Note that the model itself is not available in the <see cref = "AssetImportContext" /> at this point.
        /// </remarks>
        void OnMetadataImported(AssetImportContext ctx, ONNXModelMetadata metadata);
    }
}
