using System;
using UnityEditor.AssetImporters;
using UnityEngine;
#if SENTIS_ANALYTICS_ENABLED
using Unity.InferenceEngine.Editor.Analytics.Import;
#endif

namespace Unity.InferenceEngine.Editor.Torch
{
    /// <summary>
    /// Represents an importer for serialized PyTorch (.pt2) model files.
    /// </summary>
    [ScriptedImporter(1, new[] { "pt2" })]
    [HelpURL("https://docs.unity3d.com/Packages/com.unity.ai.inference@latest/index.html")]
    class TorchModelImporter : ModelImporterBase
    {
        protected override Model LoadModel(AssetImportContext ctx)
        {
            m_ModelConverter = new TorchModelConverter(ctx.assetPath);

#if SENTIS_ANALYTICS_ENABLED

            // Subscribe to converter events for real-time analytics capture
            if (m_ModelConverter is TorchModelConverter converter)
            {
                converter.OnTorchDataType += dataType => m_ImportReport.sourceModel.AddDataType(dataType);
                converter.OnTorchDataTypeUnsupported += dataType => m_ImportReport.sourceModel.AddUnsupportedDataType(dataType);
                converter.OnTorchOperator += op =>
                {
                    m_ImportReport.sourceModel.layerCount++;
                    m_ImportReport.sourceModel.AddOperator(op);
                };
                converter.OnTorchOperatorUnsupported += op => m_ImportReport.sourceModel.AddUnsupportedOperator(op);
                converter.OnTorchModelLoaded += exportedProgram =>
                    TorchModelImportAnalyticsHelper.CaptureSourceModel(exportedProgram, m_ImportReport);
            }

#endif

            var model = m_ModelConverter.Convert();

            return model;
        }
    }
}
