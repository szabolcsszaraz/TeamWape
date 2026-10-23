using UnityEditor;
using UnityEditor.AssetImporters;
using System.Reflection;

namespace Unity.InferenceEngine.Editor.Torch
{
    [CustomEditor(typeof(TorchModelImporter))]
    [CanEditMultipleObjects]
    class TorchModelImporterEditor : ScriptedImporterEditor
    {
        static PropertyInfo s_InspectorModeInfo;

        static TorchModelImporterEditor()
        {
            s_InspectorModeInfo = typeof(SerializedObject).GetProperty("inspectorMode", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public override void OnInspectorGUI()
        {
            var modelImporter = target as ModelImporterBase;
            if (modelImporter == null)
            {
                ApplyRevertGUI();
                return;
            }

            InspectorMode inspectorMode = InspectorMode.Normal;
            if (s_InspectorModeInfo != null)
                inspectorMode = (InspectorMode)s_InspectorModeInfo.GetValue(assetSerializedObject);

            serializedObject.Update();

            bool debugView = inspectorMode != InspectorMode.Normal;
            SerializedProperty iterator = serializedObject.GetIterator();
            for (bool enterChildren = true; iterator.NextVisible(enterChildren); enterChildren = false)
            {
                if (iterator.propertyPath != "m_Script")
                    EditorGUILayout.PropertyField(iterator, true);
            }

            serializedObject.ApplyModifiedProperties();

            ApplyRevertGUI();
        }
    }
}
