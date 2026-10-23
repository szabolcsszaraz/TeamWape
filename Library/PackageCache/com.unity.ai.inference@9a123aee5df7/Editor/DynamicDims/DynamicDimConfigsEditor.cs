using System;
using UnityEditor.AssetImporters;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.DynamicDims
{
    class DynamicDimConfigsEditor : VisualElement
    {
        ScriptedImporterEditor m_Editor;
        IDynamicDimImporter m_Importer;

        internal DynamicDimConfigsEditor(ScriptedImporterEditor editor)
        {
            m_Importer = editor.serializedObject.targetObject as IDynamicDimImporter;
            m_Editor = editor;

            InitializeVisuals();
        }

        internal void InitializeVisuals()
        {
            Clear();

            var items = m_Importer?.dynamicDimConfigs;

            var foldout = new Foldout
            {
                text = $"<b>Dynamic Input Shape Dimensions ({items?.Length}) </b>",
                tooltip = "Specify dynamic dimension's size on model import. A value of -1 keeps the dimension dynamic.",
            };

            foldout.SetEnabled(items?.Length > 0);

            var listView = new ListView(items, 16, MakeItem, BindItem)
            {
                showAlternatingRowBackgrounds = AlternatingRowBackground.All,
                showBorder = true,
                selectionType = SelectionType.Multiple,
                style =
                {
                    flexGrow = 1
                },
                horizontalScrollingEnabled = true
            };

            foldout.Add(listView);
            Add(foldout);
        }

        VisualElement MakeItem()
        {
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };

            var label = new Label
            {
                style =
                {
                    width = 175f,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis
                }
            };

            container.Add(label);

            var integerField = new IntegerField
            {
                style =
                {
                    flexGrow = 1,
                    marginRight = 3f,
                    minWidth = 15f
                }
            };

            container.Add(integerField);

            return container;
        }

        void BindItem(VisualElement e, int i)
        {
            var label = e.Q<Label>();
            var dimName = m_Importer.dynamicDimConfigs[i].name;
            label.text = $"<b>{dimName}</b>";
            label.tooltip = dimName;

            var integerField = e.Q<IntegerField>();
            integerField.value = m_Importer.dynamicDimConfigs[i].size;

            integerField.RegisterCallback<FocusOutEvent>((_) =>
            {
                if (string.IsNullOrEmpty(integerField.text)) //If the user cleared the field, default to -1
                {
                    integerField.value = -1;
                }
            });

            integerField.RegisterValueChangedCallback((changeEvent) =>
            {
                if(changeEvent.newValue < -1)
                {
                    integerField.value = -1;
                    UnityEngine.Debug.LogWarning("Dynamic dimensions must be either -1 (dynamic) or non-negative (static)");
                }
            });

            var iterator = m_Editor.serializedObject.GetIterator();
            while (iterator.NextVisible(true))
            {
                if (iterator.name == "dynamicDimConfigs")
                {
                    var array = iterator.GetArrayElementAtIndex(i);

                    var property = array.FindPropertyRelative("size");
                    integerField.BindProperty(property);
                    break;
                }
            }
        }
    }
}
