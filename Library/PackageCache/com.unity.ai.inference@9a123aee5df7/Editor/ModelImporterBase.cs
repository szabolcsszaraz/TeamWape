using System;
using System.Linq;
using Unity.InferenceEngine.Editor.DynamicDims;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
#if SENTIS_ANALYTICS_ENABLED
using Unity.InferenceEngine.Editor.Analytics.Import;
#endif

namespace Unity.InferenceEngine.Editor
{
    /// <summary>
    /// Base class for model importer
    /// </summary>
    abstract class ModelImporterBase : ScriptedImporter
    {
#if SENTIS_ANALYTICS_ENABLED
        internal ModelImportAnalytics.ImportData m_ImportReport;
#endif
        protected ModelConverterBase m_ModelConverter;

        /// <summary>
        /// Callback that Sentis calls when the model has finished importing.
        /// </summary>
        /// <param name="ctx">Asset import context</param>
        public override void OnImportAsset(AssetImportContext ctx)
        {
            long fileSizeBytes = 0;
            byte[] modelDescriptionBytes = null;
            byte[][] modelWeightsBytes = null;

            try
            {
#if SENTIS_ANALYTICS_ENABLED

                // Initialize analytics report before conversion
                // Use streaming hash to avoid loading entire file into memory (supports >2GB files)
                var attribute = GetType()
                    .GetCustomAttributes(typeof(ScriptedImporterAttribute), true)
                    .FirstOrDefault() as ScriptedImporterAttribute;
                var modelType = attribute?.fileExtensions?.FirstOrDefault() ?? "unknown";

                var (sourceFileHash, sourceFileSizeBytes) = ModelImportAnalytics.ComputeFileHashStreaming(ctx.assetPath);
                m_ImportReport = new ModelImportAnalytics.ImportData
                {
                    modelType = modelType,
                    sourceModel =
                    {
                        fileSizeBytes = sourceFileSizeBytes,
                        fileHash = sourceFileHash
                    }
                };
#endif
                var model = LoadModel(ctx);
                if (model == null)
                {
                    var failedMessage = "Failed to load model.";

#if SENTIS_ANALYTICS_ENABLED
                    m_ImportReport.importSucceeded = false;
                    if (string.IsNullOrEmpty(m_ImportReport.failureReason))
                        m_ImportReport.failureReason = failedMessage;
#endif
                    UnityEngine.Debug.LogError(failedMessage);
                    return;
                }

                var asset = ScriptableObject.CreateInstance<ModelAsset>();
                ModelWriter.SaveModel(model, out modelDescriptionBytes, out modelWeightsBytes);

#if SENTIS_ANALYTICS_ENABLED

                // Capture imported model metrics and graph structure
                if (model != null)
                {
                    SentisModelImportAnalyticsHelper.CaptureImportedModel(model, m_ImportReport);

                    // If this importer supports dynamic dimensions, capture the final dynamicDimConfigs
                    // (after InitializeDynamicDimsConfig has been called in LoadModel)
                    if (this is IDynamicDimImporter dynamicDimImporter)
                        m_ImportReport.importedModel.dynamicDimConfigs = ModelImportAnalytics.ConvertDynamicDimConfigs(dynamicDimImporter.dynamicDimConfigs);
                }

                m_ImportReport.importSucceeded = true;
#endif

                // Calculate total serialized file size
                fileSizeBytes = modelDescriptionBytes.Length;
                for (var i = 0; i < modelWeightsBytes.Length; i++)
                {
                    fileSizeBytes += modelWeightsBytes[i].Length;
                }

                var modelAssetData = ScriptableObject.CreateInstance<ModelAssetData>();
                modelAssetData.value = modelDescriptionBytes;
                modelAssetData.name = "Data";
                modelAssetData.hideFlags = HideFlags.HideInHierarchy;
                asset.modelAssetData = modelAssetData;

                asset.modelWeightsChunks = new ModelAssetWeightsData[modelWeightsBytes.Length];
                for (var i = 0; i < modelWeightsBytes.Length; i++)
                {
                    asset.modelWeightsChunks[i] = ScriptableObject.CreateInstance<ModelAssetWeightsData>();
                    asset.modelWeightsChunks[i].value = modelWeightsBytes[i];
                    asset.modelWeightsChunks[i].name = "Data";
                    asset.modelWeightsChunks[i].hideFlags = HideFlags.HideInHierarchy;

                    ctx.AddObjectToAsset($"model data weights {i}", asset.modelWeightsChunks[i]);
                }

                ctx.AddObjectToAsset("main obj", asset);
                ctx.AddObjectToAsset("model data", modelAssetData);

                ctx.SetMainObject(asset);

                EditorUtility.SetDirty(this);
            }
#if SENTIS_ANALYTICS_ENABLED
            catch (Exception e)
            {
                m_ImportReport.importSucceeded = false;
                m_ImportReport.failureReason = e.Message;
                throw;
            }
#endif
            finally
            {
#if SENTIS_ANALYTICS_ENABLED
                m_ImportReport.assetGuid = AssetDatabase.AssetPathToGUID(ctx.assetPath);
#endif
                if (m_ModelConverter != null && m_ModelConverter.ImportWarnings != null)
                {
                    // Log warnings/errors even if import failed
                    foreach (var warning in m_ModelConverter.ImportWarnings)
                    {
#if SENTIS_ANALYTICS_ENABLED
                        m_ImportReport.importWarnings.Add(warning);
#endif
                        switch (warning.messageSeverity)
                        {
                            case ModelConverterBase.WarningType.Warning:
                                ctx.LogImportWarning(warning.message);
                                break;
                            case ModelConverterBase.WarningType.Error:
                                ctx.LogImportError(warning.message);
                                break;
                            default:
                            case ModelConverterBase.WarningType.None:
                            case ModelConverterBase.WarningType.Info:
                                break;
                        }
                    }
                }

#if SENTIS_ANALYTICS_ENABLED
                OnModelImportComplete(fileSizeBytes, modelDescriptionBytes, modelWeightsBytes);
#endif
            }
        }

#if SENTIS_ANALYTICS_ENABLED
        /// <summary>
        /// Called after model serialization is complete. Override to send analytics with file data.
        /// </summary>
        /// <param name="fileSizeBytes">The size of the serialized model in bytes.</param>
        /// <param name="modelDescriptionBytes">The model description bytes.</param>
        /// <param name="modelWeightsBytes">The model weights bytes (chunked).</param>
        void OnModelImportComplete(long fileSizeBytes, byte[] modelDescriptionBytes, byte[][] modelWeightsBytes)
        {
            if (m_ImportReport == null)
                return;

            // Note: We intentionally send analytics even on failed imports (fileSizeBytes = 0)
            // to track failure reasons. The importSucceeded flag indicates success/failure.
            m_ImportReport.importedModel.fileSizeBytes = fileSizeBytes;

            // Compute hash of imported model (description + weights) using fast xxHash64
            if (fileSizeBytes > 0)
            {
                // Combine description and all weight chunks for hashing
                var allChunks = new byte[1 + modelWeightsBytes.Length][];
                allChunks[0] = modelDescriptionBytes;
                Array.Copy(modelWeightsBytes, 0, allChunks, 1, modelWeightsBytes.Length);

                m_ImportReport.importedModel.fileHash = ModelImportAnalytics.ComputeFileHash(allChunks);
            }

            ModelImportAnalytics.SendEvent(m_ImportReport);
        }
#endif
        protected abstract Model LoadModel(AssetImportContext ctx);
    }
}
