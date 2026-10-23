using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Mesh.Services.Stores.Actions;
using Unity.AI.Mesh.Services.Stores.Selectors;
using Unity.AI.Mesh.Services.Stores.States;
using Unity.AI.Mesh.Services.Utilities;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.ModelSelector.Services.Utilities;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Toolkit.Asset;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Mesh.Windows
{
    class GenerationMetadataContent : VisualElement
    {
        public event Action OnDismissRequested;

        const string k_Uxml = "Packages/com.unity.ai.assistant/modules/Unity.AI.Mesh/Windows/GenerationMetadataWindow/GenerationMetadataWindow.uxml";

        readonly Store m_Store;
        readonly GenerationMetadata m_GenerationMetadata;

        ModelSettings m_ModelSettings;
        readonly List<Button> m_DismissButtons;

        public GenerationMetadataContent(IStore store, GenerationMetadata generationMetadata)
        {
            m_GenerationMetadata = generationMetadata;
            m_Store = (Store)store;

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            var useAllButton = this.Q<Button>("use-all-button");
            useAllButton.clicked += UseAll;

            InitRefinementMode();
            InitModel();
            InitPrompt();
            InitNegativePrompt();
            InitCustomSeed();
            InitPromptImageReference();
            InitMultiviewReferences();
            InitModelReference();
            InitFaceCount();

            m_DismissButtons = this.Query<Button>(className: "data-button").ToList();
            foreach (var button in m_DismissButtons)
            {
                button.clicked += OnDismiss;
            }
        }

        void InitRefinementMode()
        {
            var refinementModeContainer = this.Q<VisualElement>(className: "refinement-mode-container");
            var refinementModeMetadata = this.Q<Label>("refinement-mode-metadata");
            var refinementModeUseButton = this.Q<Button>("use-refinement-mode-button");

            var refinementMode = m_GenerationMetadata?.refinementMode;
            refinementModeMetadata.text = refinementMode.AddSpaceBeforeCapitalLetters();
            refinementModeUseButton.clicked += UseRefinementMode;
            refinementModeContainer.EnableInClassList("hidden", string.IsNullOrEmpty(refinementMode));
        }

        void InitModel()
        {
            m_ModelSettings = m_Store?.State?.SelectModelSettings(m_GenerationMetadata);
            var modelName = m_ModelSettings?.name;
            if (string.IsNullOrEmpty(modelName))
            {
                if(!string.IsNullOrEmpty(m_GenerationMetadata.modelName))
                    modelName = m_GenerationMetadata.modelName;
                else
                    modelName = "Invalid Model";
            }

            var modelContainer = this.Q<VisualElement>(className: "model-container");
            var modelMetadata = this.Q<Label>("model-metadata");
            var modelCopyButton = this.Q<Button>("copy-model-button");
            var modelUseButton = this.Q<Button>("use-model-button");

            modelMetadata.text = modelName;
            modelCopyButton.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = m_GenerationMetadata?.model;
            };
            modelUseButton.clicked += UseModel;
            modelUseButton.SetEnabled(m_ModelSettings.IsValid());
            modelContainer.EnableInClassList("hidden", string.IsNullOrEmpty(modelName));
        }

        void InitPrompt()
        {
            var promptContainer = this.Q<VisualElement>(className: "prompt-container");
            var promptMetadata = this.Q<Label>("prompt-metadata");
            var promptCopyButton = this.Q<Button>("copy-prompt-button");
            var promptUseButton = this.Q<Button>("use-prompt-button");

            var prompt = m_GenerationMetadata?.prompt;
            promptMetadata.text = prompt;
            promptCopyButton.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = prompt;
            };
            promptUseButton.clicked += UsePrompt;
            promptContainer.EnableInClassList("hidden", string.IsNullOrEmpty(prompt));
        }

        void InitNegativePrompt()
        {
            var negativePromptContainer = this.Q<VisualElement>(className: "negative-prompt-container");
            var negativePromptMetadata = this.Q<Label>("negative-prompt-metadata");
            var negativePromptCopyButton = this.Q<Button>("copy-negative-prompt-button");
            var negativePromptUseButton = this.Q<Button>("use-negative-prompt-button");

            var negativePrompt = m_GenerationMetadata?.negativePrompt;
            negativePromptMetadata.text = negativePrompt;
            negativePromptCopyButton.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = negativePrompt;
            };
            negativePromptUseButton.clicked += UseNegativePrompt;
            negativePromptContainer.EnableInClassList("hidden", string.IsNullOrEmpty(negativePrompt));
        }

        void InitCustomSeed()
        {
            var customSeedContainer = this.Q<VisualElement>(className: "custom-seed-container");
            var customSeedMetadata = this.Q<Label>("custom-seed-metadata");
            var customSeedCopyButton = this.Q<Button>("copy-custom-seed-button");
            var customSeedUseButton = this.Q<Button>("use-custom-seed-button");

            var customSeed = m_GenerationMetadata?.customSeed;
            customSeedMetadata.text = customSeed.ToString();
            customSeedCopyButton.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = customSeed.ToString();
            };
            customSeedUseButton.clicked += UseCustomSeed;
            customSeedContainer.EnableInClassList("hidden", customSeed == -1);
        }

        void UseRefinementMode()
        {
            var refinementMode = m_GenerationMetadata.refinementMode;

            if(Enum.TryParse<RefinementMode>(refinementMode, out var mode))
                this.Dispatch(GenerationSettingsActions.setRefinementMode, mode);
        }

        void UseModel()
        {
            if (m_ModelSettings.IsValid())
                this.Dispatch(GenerationSettingsActions.setSelectedModelID, (this.GetState().SelectRefinementMode(this), m_ModelSettings.id));
        }

        void UsePrompt()
        {
            var truncatedPrompt = PromptUtilities.TruncatePrompt(m_GenerationMetadata?.prompt);
            this.Dispatch(GenerationSettingsActions.setPrompt, truncatedPrompt);
        }

        void UseNegativePrompt()
        {
            var truncatedPrompt = PromptUtilities.TruncatePrompt(m_GenerationMetadata?.negativePrompt);
            this.Dispatch(GenerationSettingsActions.setNegativePrompt, truncatedPrompt);
        }

        void UseCustomSeed()
        {
            if (m_GenerationMetadata.customSeed != -1)
            {
                this.Dispatch(GenerationSettingsActions.setUseCustomSeed, true);
                this.Dispatch(GenerationSettingsActions.setCustomSeed, m_GenerationMetadata.customSeed);
            }
        }

        void InitPromptImageReference()
        {
            var container = this.Q<VisualElement>(className: "prompt-image-reference-container");
            var objectField = container.Q<ObjectField>(className: "metadata-object-field__input-field");
            var copyButton = this.Q<Button>("copy-prompt-image-ref-button");
            var useButton = this.Q<Button>("use-prompt-image-ref-button");

            var guid = m_GenerationMetadata?.promptImageReferenceGuid;
            if (string.IsNullOrEmpty(guid))
            {
                container.EnableInClassList("hidden", true);
                return;
            }

            objectField.SetEnabled(false);
            objectField.EnableInClassList("unity-disabled", false);

            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(assetPath) && File.Exists(assetPath))
            {
                var assetRef = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                objectField.SetValueWithoutNotify(assetRef);
                copyButton.clicked += () => EditorGUIUtility.systemCopyBuffer = guid;
            }
            else
            {
                objectField.SetValueWithoutNotify(null);
                var objectFieldLabel = objectField.Q<Label>(className: "unity-object-field-display__label");
                objectFieldLabel.text = "Reference not found in project";
                copyButton.SetEnabled(false);
                useButton.SetEnabled(false);
            }

            useButton.clicked += UsePromptImageReference;
        }

        static readonly (string containerClass, string copyButton, string useButton, int index)[] k_MultiviewViews =
        {
            ("multiview-front-container", "copy-multiview-front-button", "use-multiview-front-button", 0),
            ("multiview-back-container", "copy-multiview-back-button", "use-multiview-back-button", 1),
            ("multiview-left-container", "copy-multiview-left-button", "use-multiview-left-button", 2),
            ("multiview-right-container", "copy-multiview-right-button", "use-multiview-right-button", 3),
            ("multiview-left-front-container", "copy-multiview-left-front-button", "use-multiview-left-front-button", 4),
            ("multiview-right-front-container", "copy-multiview-right-front-button", "use-multiview-right-front-button", 5),
            ("multiview-top-container", "copy-multiview-top-button", "use-multiview-top-button", 6),
            ("multiview-bottom-container", "copy-multiview-bottom-button", "use-multiview-bottom-button", 7),
        };

        void InitMultiviewReferences()
        {
            var outerContainer = this.Q<VisualElement>(className: "multiview-references-container");
            var anyVisible = false;

            foreach (var view in k_MultiviewViews)
            {
                var container = this.Q<VisualElement>(className: view.containerClass);
                var guid = GetMultiviewGuid(view.index);

                if (string.IsNullOrEmpty(guid))
                {
                    container.EnableInClassList("hidden", true);
                    continue;
                }

                anyVisible = true;
                var objectField = container.Q<ObjectField>(className: "metadata-object-field__input-field");
                var copyButton = this.Q<Button>(view.copyButton);
                var useButton = this.Q<Button>(view.useButton);

                objectField.SetEnabled(false);
                objectField.EnableInClassList("unity-disabled", false);

                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(assetPath) && File.Exists(assetPath))
                {
                    var assetRef = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    objectField.SetValueWithoutNotify(assetRef);
                    var capturedGuid = guid;
                    copyButton.clicked += () => EditorGUIUtility.systemCopyBuffer = capturedGuid;
                }
                else
                {
                    objectField.SetValueWithoutNotify(null);
                    var objectFieldLabel = objectField.Q<Label>(className: "unity-object-field-display__label");
                    objectFieldLabel.text = "Reference not found in project";
                    copyButton.SetEnabled(false);
                    useButton.SetEnabled(false);
                }

                var capturedIndex = view.index;
                var capturedUseGuid = guid;
                useButton.clicked += () =>
                {
                    if (!string.IsNullOrEmpty(capturedUseGuid))
                        this.Dispatch(GenerationSettingsActions.setMultiviewImageReference, (capturedIndex, new AssetReference { guid = capturedUseGuid }));
                };
            }

            outerContainer.EnableInClassList("hidden", !anyVisible);
        }

        string GetMultiviewGuid(int index)
        {
            return index switch
            {
                0 => m_GenerationMetadata?.multiviewFrontGuid,
                1 => m_GenerationMetadata?.multiviewBackGuid,
                2 => m_GenerationMetadata?.multiviewLeftGuid,
                3 => m_GenerationMetadata?.multiviewRightGuid,
                4 => m_GenerationMetadata?.multiviewLeftFrontGuid,
                5 => m_GenerationMetadata?.multiviewRightFrontGuid,
                6 => m_GenerationMetadata?.multiviewTopGuid,
                7 => m_GenerationMetadata?.multiviewBottomGuid,
                _ => null
            };
        }

        void UseMultiviewReferences()
        {
            foreach (var view in k_MultiviewViews)
            {
                var guid = GetMultiviewGuid(view.index);
                if (!string.IsNullOrEmpty(guid))
                    this.Dispatch(GenerationSettingsActions.setMultiviewImageReference, (view.index, new AssetReference { guid = guid }));
            }
        }

        void InitModelReference()
        {
            var container = this.Q<VisualElement>(className: "model-reference-container");
            var objectField = container.Q<ObjectField>(className: "metadata-object-field__input-field");
            var copyButton = this.Q<Button>("copy-model-ref-button");
            var useButton = this.Q<Button>("use-model-ref-button");

            var guid = m_GenerationMetadata?.modelReferenceGuid;
            if (string.IsNullOrEmpty(guid))
            {
                container.EnableInClassList("hidden", true);
                return;
            }

            objectField.SetEnabled(false);
            objectField.EnableInClassList("unity-disabled", false);

            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(assetPath) && File.Exists(assetPath))
            {
                var assetRef = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                objectField.SetValueWithoutNotify(assetRef);
                copyButton.clicked += () => EditorGUIUtility.systemCopyBuffer = guid;
            }
            else
            {
                objectField.SetValueWithoutNotify(null);
                var objectFieldLabel = objectField.Q<Label>(className: "unity-object-field-display__label");
                objectFieldLabel.text = "Reference not found in project";
                copyButton.SetEnabled(false);
                useButton.SetEnabled(false);
            }

            useButton.clicked += UseModelReference;
        }

        void InitFaceCount()
        {
            var container = this.Q<VisualElement>(className: "face-count-container");
            var faceCountMetadata = this.Q<Label>("face-count-metadata");
            var useButton = this.Q<Button>("use-face-count-button");

            var faceLimit = m_GenerationMetadata?.faceLimit ?? -1;
            if (faceLimit < 0)
            {
                container.EnableInClassList("hidden", true);
                return;
            }

            faceCountMetadata.text = faceLimit.ToString("N0");
            useButton.clicked += UseFaceCount;
        }

        void UsePromptImageReference()
        {
            var guid = m_GenerationMetadata?.promptImageReferenceGuid;
            if (!string.IsNullOrEmpty(guid))
                this.Dispatch(GenerationSettingsActions.setPromptImageReferenceAsset, new AssetReference { guid = guid });
        }

        void UseModelReference()
        {
            var guid = m_GenerationMetadata?.modelReferenceGuid;
            if (!string.IsNullOrEmpty(guid))
                this.Dispatch(GenerationSettingsActions.setModelReferenceAsset, new AssetReference { guid = guid });
        }

        void UseFaceCount()
        {
            var faceLimit = m_GenerationMetadata?.faceLimit ?? -1;
            if (faceLimit >= 0)
            {
                this.Dispatch(GenerationSettingsActions.setUseFaceLimit, true);
                this.Dispatch(GenerationSettingsActions.setFaceLimit, faceLimit);
            }
        }

        void UseAll()
        {
            UseRefinementMode();
            UseModel();
            UsePrompt();
            UseNegativePrompt();
            UseCustomSeed();
            UsePromptImageReference();
            UseMultiviewReferences();
            UseModelReference();
            UseFaceCount();
        }

        void OnDismiss()
        {
            OnDismissRequested?.Invoke();
        }
    }
}
