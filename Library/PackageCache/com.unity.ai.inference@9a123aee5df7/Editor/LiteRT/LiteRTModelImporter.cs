using System;
using System.Runtime.CompilerServices;
using UnityEditor.AssetImporters;
using UnityEngine;
#if SENTIS_ANALYTICS_ENABLED
using Unity.InferenceEngine.Editor.Analytics.Import;
#endif

[assembly: InternalsVisibleTo("Unity.Sentis.EditorTests")]

namespace Unity.InferenceEngine.Editor.LiteRT
{
    /// <summary>
    /// Represents an importer for TensorFlow Lite (LiteRT) files.
    /// </summary>
    [ScriptedImporter(2, new[] { "tflite" })]
    [HelpURL("https://docs.unity3d.com/Packages/com.unity.ai.inference@latest/index.html")]
    class LiteRTModelImporter : ModelImporterBase
    {
        [SerializeField]
        internal string[] signatureKeys;

        [SerializeField]
        internal string signatureKey;

        protected override InferenceEngine.Model LoadModel(AssetImportContext ctx)
        {
            m_ModelConverter = new LiteRTModelConverter(ctx.assetPath, signatureKey);
            var converter = m_ModelConverter as LiteRTModelConverter;

#if SENTIS_ANALYTICS_ENABLED

            // Subscribe to converter events for real-time analytics capture
            converter.OnLiteRTDataType += dataType => m_ImportReport.sourceModel.AddDataType(dataType);
            converter.OnLiteRTDataTypeUnsupported += dataType => m_ImportReport.sourceModel.AddUnsupportedDataType(dataType);
            converter.OnLiteRTOperator += op =>
            {
                m_ImportReport.sourceModel.layerCount++;
                m_ImportReport.sourceModel.AddOperator(op);
            };
            converter.OnLiteRTOperatorUnsupported += op => m_ImportReport.sourceModel.AddUnsupportedOperator(op);
            converter.OnLiteRTModelLoaded += liteModel => LiteRTModelImportAnalyticsHelper.CaptureSourceModel(liteModel, m_ImportReport);
#endif

            var model = converter.Convert();

            signatureKeys = converter.signatureKeys;
            signatureKey = converter.signatureKey;

            return model;
        }
    }
}
