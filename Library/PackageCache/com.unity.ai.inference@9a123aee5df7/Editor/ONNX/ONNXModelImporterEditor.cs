using System;
using Unity.InferenceEngine.Editor.DynamicDims;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Onnx
{
    [CustomEditor(typeof(ONNXModelImporter))]
    class ONNXModelImporterEditor : ScriptedImporterEditor
    {
        DynamicDimConfigsEditor m_DynamicDimConfigsEditor;

        public override VisualElement CreateInspectorGUI()
        {
            var container = new VisualElement();
            m_DynamicDimConfigsEditor = new DynamicDimConfigsEditor(this);
            container.Add(m_DynamicDimConfigsEditor);
            container.Add(new IMGUIContainer(ApplyRevertGUI));

            return container;
        }

        public override void DiscardChanges()
        {
            base.DiscardChanges();
            m_DynamicDimConfigsEditor?.InitializeVisuals();
        }
    }
}
