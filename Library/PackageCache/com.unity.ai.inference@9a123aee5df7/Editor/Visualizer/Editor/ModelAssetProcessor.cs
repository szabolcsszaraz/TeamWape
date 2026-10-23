using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.InferenceEngine.Editor.Visualizer.Editor
{
    class ModelAssetProcessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var windows = Resources.FindObjectsOfTypeAll<ModelVisualizerWindow>();

            foreach (var window in windows)
            {
                if (importedAssets.Contains(AssetDatabase.GetAssetPath(window.ModelAsset)))
                {
                    window.OnReimport();
                }
            }
        }
    }
}
