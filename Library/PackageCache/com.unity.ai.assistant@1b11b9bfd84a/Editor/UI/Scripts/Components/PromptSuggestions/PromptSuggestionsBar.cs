using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.PromptSuggestions
{
    class PromptSuggestionsBar : PromptSuggestionsBarBase
    {
        VisualElement m_TabContainer;
        VisualElement m_CloseButton;
        VisualElement m_ExpandedContent;
        VisualElement m_PromptList;
        bool m_IsExpanded;

        public PromptSuggestionsBar() : base(new TabData[]
        {
            new PromptTab
            {
                Label  = "Image to scene",
                Prompts = new[]
                {
                    new PromptData("Generate scene using primitives from the attached image", "Upload or drag & drop a file"),
                    new PromptData("Generate game assets inspired by the attached image", "Upload or drag & drop a file"),
                }
            }
        }) { }

        protected override void InitializeView(TemplateContainer view)
        {
            m_TabContainer = view.Q<VisualElement>("tabContainer");
            m_CloseButton = view.Q<VisualElement>("closeButton");
            m_ExpandedContent = view.Q<VisualElement>("expandedContent");
            m_PromptList = view.Q<VisualElement>("promptList");

            m_CloseButton.AddManipulator(new Clickable(Collapse));

            BuildButtons(m_TabContainer, "mui-prompt-suggestions-tab", SetActiveTab);
            RefreshChatInputState();
        }

        void SetActiveTab(int index)
        {
            m_ActiveTabIndex = index;
            RefreshPromptList(m_PromptList);
            m_IsExpanded = true;
            RefreshChatInputState();
        }

        void RefreshChatInputState()
        {
            m_CloseButton.SetDisplay(m_IsExpanded);
            m_ExpandedContent.SetDisplay(m_IsExpanded);
            RefreshTabStyles();
        }

        void RefreshTabStyles() => RefreshButtonStyles("mui-prompt-suggestions-tab-active");

        internal override void Collapse()
        {
            m_ActiveTabIndex = -1;
            m_IsExpanded = false;
            RefreshChatInputState();
        }
    }
}
