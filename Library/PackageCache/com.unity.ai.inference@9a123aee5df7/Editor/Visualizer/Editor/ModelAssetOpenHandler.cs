using System;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Unity.InferenceEngine.Editor.Visualizer.Editor
{
    static class ModelAssetOpenHandler
    {
        [OnOpenAsset(9999)]
#if UNITY_6000_5_OR_NEWER
        public static bool OnOpenAssetCallback(EntityId entityId, int line)
        {
            var obj = EditorUtility.EntityIdToObject(entityId);
#else
        public static bool OnOpenAssetCallback(int instanceID, int line)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceID);
#endif
            if (obj is ModelAsset modelAsset)
            {
                ModelVisualizerWindow.VisualizeModel(modelAsset);
                return true;
            }

            return false;
        }
    }
}
