using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Editor
{
    class ModelVisualizerSettingsProvider : SettingsProvider
    {
        SerializedObject m_SerializedObject;
        SerializedProperty m_CanvasControlScheme;

        ModelVisualizerSettingsProvider(string path, SettingsScope scope = SettingsScope.Project)
            : base(path, scope) {}

        public static bool IsSettingsAvailable()
        {
            return true;
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            m_SerializedObject = new SerializedObject(ModelVisualizerSettings.instance);
            m_CanvasControlScheme = m_SerializedObject.FindProperty("m_CanvasControlScheme");
        }

        public override void OnGUI(string searchContext)
        {
            m_SerializedObject.Update();

            GUILayout.BeginHorizontal();
            GUILayout.Space(6);
            GUILayout.BeginVertical();
            GUILayout.Space(12);

            EditorGUILayout.PropertyField(m_CanvasControlScheme, new GUIContent("Canvas Control Scheme"));

            EditorGUILayout.Space();
            if (GUILayout.Button("Reset to Defaults"))
            {
                ModelVisualizerSettings.instance.ResetToDefaults();
                m_SerializedObject.Update();
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            // Check if properties were modified
            var wasModified = m_SerializedObject.ApplyModifiedProperties();

            // If properties were modified, call Save to trigger the OnSave event
            if (wasModified)
            {
                ModelVisualizerSettings.instance.PropertiesModified();
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateGraphSettingsProvider()
        {
            if (IsSettingsAvailable())
            {
                var provider = new ModelVisualizerSettingsProvider("Project/Sentis/Model Visualizer");
                provider.keywords = new[] { "inference", "graph", "visualization", "node", "visualizer" };
                return provider;
            }

            return null;
        }
    }
}
