using System.Collections.Generic;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Toolkit.Asset;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class UnlabeledImageReferenceList : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/Modules/Unity.AI.Image/Components/UnlabeledImageReferenceList/UnlabeledImageReferenceList.uxml";
        const int k_MaxSlots = 15;
        const int k_DefaultSlots = 2;

        readonly UnlabeledImageReference[] m_References = new UnlabeledImageReference[k_MaxSlots];

        readonly VisualElement m_SlotsContainer;

        public UnlabeledImageReferenceList()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);
            AddToClassList("unlabeled-image-reference-list-component");

            m_SlotsContainer = this.Q<VisualElement>("slots-container");

            for (int i = 0; i < k_MaxSlots; i++)
            {
                var reference = new UnlabeledImageReference { Index = i };
                m_References[i] = reference;
                m_SlotsContainer.Add(reference);
            }

            this.Use(state => state.SelectUnlabeledImageReferences(this), OnReferencesChanged);
            this.Use(state => state.SelectMaxReferenceImages(this), OnMaxImagesChanged);
        }

        void OnReferencesChanged(List<ImageReferenceSettings> references)
        {
            int refCount = references?.Count ?? 0;

            for (int i = 0; i < k_MaxSlots; i++)
            {
                if (i < refCount)
                {
                    m_References[i].style.display = DisplayStyle.Flex;
                    m_References[i].Index = i;
                }
                else
                {
                    m_References[i].style.display = DisplayStyle.None;
                }
            }
        }

        void OnMaxImagesChanged(int maxImages)
        {
            var promptIsActive = this.GetState()?.SelectImageReferenceIsActive(this, ImageReferenceType.PromptImage) ?? false;
            var effectiveMax = Mathf.Clamp(maxImages - (promptIsActive ? 1 : 0), 0, k_MaxSlots);

            var references = this.GetState()?.SelectUnlabeledImageReferences(this);
            var currentCount = references?.Count ?? 0;

            if (currentCount > effectiveMax)
            {
                for (int i = currentCount - 1; i >= 0 && currentCount > effectiveMax; i--)
                {
                    var r = references[i];
                    var isEmpty = !r.asset.IsValid() && r.doodle is not { Length: not 0 };
                    if (isEmpty)
                    {
                        this.Dispatch(GenerationSettingsActions.removeUnlabeledImageReference, i);
                        currentCount--;
                    }
                }
            }
        }
    }
}
