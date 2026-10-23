using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.ModelSelector.Services.Stores.States;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Panel
{
    [UxmlElement]
    partial class GeneratePanel : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/Modules/Unity.AI.Image/Panels/GeneratePanel/GeneratePanel.uxml";

        readonly ScrollView m_ScrollView;
        readonly VisualElement m_UnlabeledImageReferenceList;

        IVisualElementScheduledItem m_ScheduledScroll;

        public GeneratePanel()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_ScrollView = this.Q<ScrollView>("generatePanelScrollView");
            m_UnlabeledImageReferenceList = this.Q<VisualElement>("unlabeled-image-reference-list");

            this.Use(state => state.SelectPendingPing(this), OnPingPending);
            this.Use(state => state.SelectModelSizingMode(this), OnSizingModeChanged);
            this.Use(state => state.SelectSupportsMultiReferenceImages(this), _ => UpdateUnlabeledListVisibility());
            this.Use(state => state.SelectUnlabeledImageReferences(this), _ => UpdateUnlabeledListVisibility());
        }

        void UpdateUnlabeledListVisibility()
        {
            if (m_UnlabeledImageReferenceList == null)
                return;

            var supportsMultiRef = this.GetState()?.SelectSupportsMultiReferenceImages(this) ?? false;
            var hasUnlabeledRefs = (this.GetState()?.SelectUnlabeledImageReferences(this)?.Count ?? 0) > 0;
            m_UnlabeledImageReferenceList.style.display = supportsMultiRef || hasUnlabeledRefs ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void OnSizingModeChanged(string sizingMode)
        {
            var dimComponent = this.Q<VisualElement>("dimensions-component");
            var aspectComponent = this.Q<VisualElement>("aspect-ratio-component");
            
            if (dimComponent != null && aspectComponent != null)
            {
                if (ModelConstants.SchemaKeys.IsSizingModeAspectRatio(sizingMode))
                {
                    dimComponent.style.display = DisplayStyle.None;
                    aspectComponent.style.display = DisplayStyle.Flex;
                }
                else
                {
                    dimComponent.style.display = DisplayStyle.Flex;
                    aspectComponent.style.display = DisplayStyle.None;
                }
            }
        }

        void OnPingPending(string newReference)
        {
            if (!string.IsNullOrEmpty(newReference))
            {
                var item = m_ScrollView.Q<VisualElement>(newReference);
                if (item != null)
                {
                    // need to schedule the scroll because the splitter will be reset at this current frame.
                    m_ScheduledScroll?.Pause();
                    m_ScheduledScroll = schedule.Execute(() => DelayedScrollTo(item));
                }
                this.Dispatch(GenerationSettingsActions.setPendingPing, string.Empty);
            }
        }

        void DelayedScrollTo(VisualElement item)
        {
            // need to delay the scroll to the next frame to ensure the layout of the scrollview is now correct.
            m_ScrollView.schedule.Execute(() =>
            {
                m_ScrollView.ScrollTo(item);
                item.AddToClassList(PingManipulator.pingUssClassName);
            });
        }
    }
}
