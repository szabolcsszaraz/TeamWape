using System;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.LiteRT
{
    [CustomEditor(typeof(LiteRTModelImporter))]
    class LiteRTModelImporterEditor : ScriptedImporterEditor
    {
        SignatureEditor m_SignatureEditor;

        public override VisualElement CreateInspectorGUI()
        {
            var container = new VisualElement();
            m_SignatureEditor = new SignatureEditor(this);
            container.Add(m_SignatureEditor);
            container.Add(new IMGUIContainer(ApplyRevertGUI));

            return container;
        }

        public override void DiscardChanges()
        {
            base.DiscardChanges();
            m_SignatureEditor?.InitializeVisuals();
        }
    }
}
