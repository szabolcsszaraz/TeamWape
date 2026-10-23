using System;
using System.Collections.Generic;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Components
{
    /// <summary>
    /// Operators selection view for adding to prompt.
    /// </summary>
    class AddToPromptView : VisualElement
    {
        public event Action OnDismissRequested;

        const string k_Uxml = "Packages/com.unity.ai.assistant/Modules/Unity.AI.Image/Components/AddToPromptButton/AddToPromptView.uxml";
        readonly Dictionary<ImageReferenceType, bool> m_TypesValidationResults;

        void OnCancelButtonPressed() => OnDismissRequested?.Invoke();

        public AddToPromptView(Dictionary<ImageReferenceType, bool> typesValidationResults)
        {
            m_TypesValidationResults = typesValidationResults;

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);
        }

        void InitOperator(string operatorTemplateName, ImageReferenceType imageReferenceType)
        {
            var operatorReference = this.Q<VisualElement>(operatorTemplateName);
            m_TypesValidationResults.TryGetValue(imageReferenceType, out var isOperatorEnabled);
            var isOperatorActive = this.GetState().SelectImageReferenceIsActive(this, imageReferenceType);
            var supportsMultiRef = this.GetState().SelectSupportsMultiReferenceImages(this);

            if (supportsMultiRef && imageReferenceType == ImageReferenceType.PromptImage)
            {
                var state = this.GetState();
                var maxImages = state.SelectMaxReferenceImages(this);
                var promptIsActive = state.SelectImageReferenceIsActive(this, ImageReferenceType.PromptImage);
                var unlabeledCount = state.SelectUnlabeledImageReferences(this)?.Count ?? 0;
                var currentCount = (promptIsActive ? 1 : 0) + unlabeledCount;
                var remaining = maxImages - currentCount;

                var isAtMax = remaining <= 0;
                operatorReference.SetEnabled(isOperatorEnabled && !isAtMax);

                var iconContainer = operatorReference.Q<VisualElement>("operator-icon")?.parent;
                if (iconContainer != null)
                {
                    var badge = new Label(remaining.ToString());
                    badge.AddToClassList("operator-count-badge");
                    iconContainer.Add(badge);
                }

                operatorReference.AddManipulator(new Clickable(evt =>
                {
                    if (!this.GetState().SelectImageReferenceIsActive(this, ImageReferenceType.PromptImage))
                    {
                        this.Dispatch(GenerationSettingsActions.setImageReferenceActive, new ImageReferenceActiveData(ImageReferenceType.PromptImage, true));
                        this.Dispatch(GenerationSettingsActions.setPendingPing, ImageReferenceType.PromptImage.GetImageReferenceName());
                    }
                    else
                    {
                        this.Dispatch(GenerationSettingsActions.addUnlabeledImageReference, new ImageReferenceSettings(0.25f, true));
                    }

                    OnCancelButtonPressed();
                }));
                return;
            }

            operatorReference.SetEnabled(isOperatorEnabled && !isOperatorActive);
            operatorReference.AddManipulator(new Clickable(evt =>
            {
                this.Dispatch(GenerationSettingsActions.setImageReferenceActive, new ImageReferenceActiveData(imageReferenceType, !isOperatorActive));

                if (!isOperatorActive)
                    this.Dispatch(GenerationSettingsActions.setPendingPing, imageReferenceType.GetImageReferenceName());

                OnCancelButtonPressed();
            }));
        }

        void OnAttachToPanel(AttachToPanelEvent _)
        {
            InitOperator("operator-image-prompt", ImageReferenceType.PromptImage);
            InitOperator("operator-style", ImageReferenceType.StyleImage);
            InitOperator("operator-composition", ImageReferenceType.CompositionImage);
            InitOperator("operator-pose", ImageReferenceType.PoseImage);
            InitOperator("operator-depth", ImageReferenceType.DepthImage);
            InitOperator("operator-line-art", ImageReferenceType.LineArtImage);
            InitOperator("operator-feature", ImageReferenceType.FeatureImage);

            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

        void OnDetachFromPanel(DetachFromPanelEvent _) => UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

        void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape)
                return;
            evt.StopPropagation();
            OnCancelButtonPressed();
        }
    }
}
