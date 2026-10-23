using Unity.AI.Generators.Asset;
using Unity.AI.Mesh.Services.Stores.Actions;
using Unity.AI.Mesh.Services.Stores.Selectors;
using Unity.AI.Mesh.Services.Stores.States;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.AI.Mesh.Components
{
    [UxmlElement]
    partial class PromptModelReference : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/modules/Unity.AI.Mesh/Components/ModelReference/PromptModelReference.uxml";

        public PromptModelReference()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            var objectField = this.Q<ObjectField>();
            objectField.allowSceneObjects = false;
            objectField.RegisterValueChangedCallback(evt =>
                this.Dispatch(GenerationSettingsActions.setModelReferenceAsset,
                    AssetReferenceExtensions.FromObject(evt.newValue)));

            var deleteButton = this.Q<Button>("delete-image-reference");
            deleteButton.clicked += () =>
                this.Dispatch(GenerationSettingsActions.setModelReference, new ModelReference());

            this.Use(
                state => Selectors.SelectModelReferenceAsset(state, this),
                asset => objectField.value = asset.GetObject());
        }
    }
}
