using System;
using System.Reflection;
using Unity.InferenceEngine.Editor.DynamicDims;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Sentis
{
    [CustomEditor(typeof(SentisModelImporter))]
    [CanEditMultipleObjects]
    class SentisModelImporterEditor : ScriptedImporterEditor
    {
        static PropertyInfo s_InspectorModeInfo;

        static SentisModelImporterEditor()
        {
            s_InspectorModeInfo = typeof(SerializedObject).GetProperty("inspectorMode", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public override VisualElement CreateInspectorGUI()
        {
            var container = new VisualElement();
            var editor = new DynamicDimConfigsEditor(this);
            container.Add(editor);
            container.Add(new IMGUIContainer(ApplyRevertGUI));

            return container;
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
