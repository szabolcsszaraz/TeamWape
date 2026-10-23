using System;
using Unity.AI.Animate.Services.Stores.Actions;
using Unity.AI.Animate.Services.Stores.Selectors;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.ModelSelector.Services.Stores.States;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Animate.Components
{
    [UxmlElement]
    partial class Prompt : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/modules/Unity.AI.Animate/Components/Prompt/Prompt.uxml";

        readonly VisualElement m_NegativePrompt;

        public Prompt()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            var promptText = this.Q<TextField>("prompt");
            var negativePromptText = this.Q<TextField>("negative-prompt");
            var promptLimitIndicator = this.Q<Label>("prompt-limit-indicator");
            var negativePromptLimitIndicator = this.Q<Label>("negative-prompt-limit-indicator");
            m_NegativePrompt = negativePromptText.parent;

            promptText.maxLength = PromptUtilities.maxPromptLength;
            negativePromptText.maxLength = PromptUtilities.maxPromptLength;

            promptText.RegisterValueChangedCallback(evt => this.Dispatch(GenerationSettingsActions.setPrompt, PromptUtilities.TruncatePrompt(evt.newValue)));
            negativePromptText.RegisterValueChangedCallback(evt => this.Dispatch(GenerationSettingsActions.setNegativePrompt, PromptUtilities.TruncatePrompt(evt.newValue)));

            promptText.RegisterTabEvent();
            negativePromptText.RegisterTabEvent();

            this.Use(state => state.SelectPrompt(this), prompt =>
            {
                promptText.value = prompt;
                promptLimitIndicator.text = $"{prompt.Length}/{PromptUtilities.maxPromptLength}";
            });
            this.Use(state => state.SelectNegativePrompt(this), negativePrompt =>
            {
                negativePromptText.value = negativePrompt;
                negativePromptLimitIndicator.text = $"{negativePrompt.Length}/{PromptUtilities.maxPromptLength}";
            });

            this.Use(state => state.SelectSelectedModel(this),
                model => m_NegativePrompt.SetShown(model?.SupportsParam(ModelConstants.SchemaKeys.NegativePrompt) ?? false));
        }
    }
}
