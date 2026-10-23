using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.LiteRT
{
    class SignatureEditor : VisualElement
    {
        const string k_Label = "Signature";
        const string k_SignatureNoneLabel = "None";

        LiteRTModelImporterEditor m_Editor;

        internal SignatureEditor(LiteRTModelImporterEditor editor)
        {
            m_Editor = editor;

            InitializeVisuals();
        }

        internal void InitializeVisuals()
        {
            Clear();

            var signatureKeysProperty = m_Editor.serializedObject.FindProperty("signatureKeys");
            var signatureKeyProperty = m_Editor.serializedObject.FindProperty("signatureKey");

            var items = new List<string>();
            items.Add(k_SignatureNoneLabel);

            if (signatureKeysProperty.isArray)
            {
                for (var i = 0; i < signatureKeysProperty.arraySize; i++)
                {
                    items.Add(signatureKeysProperty.GetArrayElementAtIndex(i).stringValue);
                }
            }

            var defaultValue = items.Contains(signatureKeyProperty.stringValue) ? signatureKeyProperty.stringValue : k_SignatureNoneLabel;

            var dropdownField = new DropdownField(
                label: k_Label,
                choices: items,
                defaultValue: defaultValue
            );

            dropdownField.RegisterValueChangedCallback(e =>
            {
                m_Editor.serializedObject.FindProperty("signatureKey").stringValue = e.newValue.Equals(k_SignatureNoneLabel) ? string.Empty : e.newValue;
            });

            Add(dropdownField);
        }
    }
}
