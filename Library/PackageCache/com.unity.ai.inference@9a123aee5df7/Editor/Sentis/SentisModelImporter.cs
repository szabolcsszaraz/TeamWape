using System;
using System.IO;
using Unity.InferenceEngine.Editor.DynamicDims;
using UnityEditor.AssetImporters;
using UnityEngine;
#if SENTIS_ANALYTICS_ENABLED
using Unity.InferenceEngine.Editor.Analytics.Import;
#endif

namespace Unity.InferenceEngine.Editor.Sentis
{
    /// <summary>
    /// Represents an importer for serialized Sentis model files.
    /// </summary>
    [ScriptedImporter(4, new[] { "sentis" })]
    [HelpURL("https://docs.unity3d.com/Packages/com.unity.ai.inference@latest/index.html")]
    class SentisModelImporter : ModelImporterBase, IDynamicDimImporter
    {
        [SerializeField]
        internal DynamicDimConfig[] dynamicDimConfigs = Array.Empty<DynamicDimConfig>();

        DynamicDimConfig[] IDynamicDimImporter.dynamicDimConfigs
        {
            get => dynamicDimConfigs;
            set => dynamicDimConfigs = value;
        }

        protected override Model LoadModel(AssetImportContext ctx)
        {
            using var fileStream = File.OpenRead(ctx.assetPath);

            var model = ModelLoader.Load(fileStream);
            if (model == null)
                return null;

#if SENTIS_ANALYTICS_ENABLED

            // Capture SOURCE model metrics from loaded .sentis file (before transformations)
            SentisModelImportAnalyticsHelper.CaptureSourceModel(model, m_ImportReport);
            SentisModelImportAnalyticsHelper.CaptureSourceModelStructure(model, m_ImportReport);
#endif

            this.InitializeDynamicDimsConfig(model);
            this.ApplyDynamicDimConfigs(model);
            this.CleanModelDynamicDims(model);

            return model;
        }
    }
}
