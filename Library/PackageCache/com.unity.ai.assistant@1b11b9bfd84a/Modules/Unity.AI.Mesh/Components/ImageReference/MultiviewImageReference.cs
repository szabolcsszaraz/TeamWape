using System.Collections.Generic;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Mesh.Services.Stores.Actions;
using Unity.AI.Mesh.Services.Stores.Selectors;
using Unity.AI.Mesh.Services.Stores.States;
using Unity.AI.Toolkit.Asset;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using AssetRefExtensions = Unity.AI.Generators.Asset.AssetReferenceExtensions;

namespace Unity.AI.Mesh.Components
{
    [UxmlElement]
    partial class MultiviewImageReference : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/modules/Unity.AI.Mesh/Components/ImageReference/MultiviewImageReference.uxml";
        const string k_SlotUxml = "Packages/com.unity.ai.assistant/modules/Unity.AI.Mesh/Components/ImageReference/MultiviewImageReferenceSlot.uxml";

        readonly List<ObjectField> m_ObjectFields = new();

        public MultiviewImageReference()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            var slotTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_SlotUxml);
            var slotsContainer = this.Q<VisualElement>("multiview-slots-container");
            var defaults = MultiviewImageReferenceSettings.CreateDefaults();

            for (var i = 0; i < defaults.Count; i++)
            {
                var index = i;
                var settings = defaults[i];

                var slot = new VisualElement();
                slotTree.CloneTree(slot);

                var headerLabel = slot.Q<Label>("header-title");
                headerLabel.text = settings.label;

                var clearButton = slot.Q<Button>("multiview-clear-button");
                clearButton.clicked += () =>
                {
                    this.Dispatch(GenerationSettingsActions.setMultiviewImageReference, (index, new AssetReference()));
                };

                var objectField = slot.Q<ObjectField>("multiview-object-field__input-field");
                objectField.AddManipulator(new ScaleToFitObjectFieldImage());
                objectField.RegisterValueChangedCallback(evt =>
                {
                    var assetRef = AssetRefExtensions.FromObject(evt.newValue as Texture);
                    this.Dispatch(GenerationSettingsActions.setMultiviewImageReference, (index, assetRef));
                });

                var settingsButton = slot.Q<Button>("multiview-settings-button");
                var container = slot.Q<VisualElement>("multiview-object-field-container");

                settingsButton.clicked += () => ShowMenu(objectField, settingsButton, index);
                container.RegisterCallback<ContextClickEvent>(_ => ShowMenu(objectField, settingsButton, index, true));

                slotsContainer.Add(slot);
                m_ObjectFields.Add(objectField);
            }

            this.Use(state => state.SelectMultiviewImageReferences(this), UpdateFields);
        }

        void ShowMenu(ObjectField objectField, Button settingsButton, int index, bool isContextClick = false)
        {
            var menu = new GenericMenu();

            if (isContextClick)
            {
                menu.AddItem(new GUIContent("Import from Project"), false, () => objectField.ShowObjectPicker());
                menu.AddSeparator("");
            }

            if (objectField.value)
                menu.AddItem(new GUIContent("Clear"), false, () =>
                {
                    this.Dispatch(GenerationSettingsActions.setMultiviewImageReference, (index, new AssetReference()));
                });
            else
                menu.AddDisabledItem(new GUIContent("Clear"));

            if (isContextClick)
                menu.ShowAsContext();
            else
                menu.DropDown(settingsButton.worldBound);
        }

        void UpdateFields(List<MultiviewImageReferenceSettings> refs)
        {
            if (refs == null)
                return;

            for (var i = 0; i < refs.Count && i < m_ObjectFields.Count; i++)
            {
                m_ObjectFields[i].SetValueWithoutNotify(AssetRefExtensions.GetObject(refs[i].asset));
            }
        }
    }
}
