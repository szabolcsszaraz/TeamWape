using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.PromptSuggestions
{
    class NewChatTopicBar : PromptSuggestionsBarBase
    {
        static readonly TabData[] k_Tabs =
        {
            new PromptTab
            {
                Label  = "Get started",
                Prompts = new[]
                {
                    new PromptData("Create a player controller script"),
                    new PromptData("Ask me questions about my game design idea"),
                    new PromptData("Build me a casual game I can play right away"),
                }
            },
            new PromptTab
            {
                Label  = "Troubleshoot",
                Prompts = new[]
                {
                    new PromptData("Help me fix the errors in my console"),
                    new PromptData("Review the scripts in my project for any issues"),
                    new PromptData("Fix the compile errors in my scripts"),
                }
            },
            new FigmaAuthTab
            {
                Label  = "Figma to UI",
                Prompts = new[]
                {
                    new PromptData("Create UI based on Figma design URL"),
                    new PromptData("List all screens in my Figma project"),
                    new PromptData("Download assets from a Figma design"),
                }
            },
            new PromptTab
            {
                Label  = "Image to scene",
                Prompts = new[]
                {
                    new PromptData("Generate scene using primitives from the attached image", "Upload or drag & drop a file"),
                    new PromptData("Generate game assets inspired by the attached image", "Upload or drag & drop a file"),
                }
            },
            new PromptTab
            {
                Label  = "Explore",
                Prompts = new[]
                {
                    new PromptData("Scan my project and give me improvement ideas"),
                    new PromptData("Help me plan the architecture for my game"),
                    new PromptData("Explain how Unity's physics system works"),
                }
            },
        };

        VisualElement m_ChipContainer;
        VisualElement m_SuggestionCard;
        VisualElement m_SuggestionList;

        public NewChatTopicBar() : base(k_Tabs)
        {
            SetResourceName("NewChatTopicBar");
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_ChipContainer = view.Q<VisualElement>("chipContainer");
            m_SuggestionCard = view.Q<VisualElement>("suggestionCard");
            m_SuggestionList = view.Q<VisualElement>("suggestionList");

            BuildButtons(m_ChipContainer, "mui-new-chat-chip", SetActiveTopic);
            RefreshNewChatState();
        }

        void SetActiveTopic(int index)
        {
            if (m_ActiveTabIndex == index)
            {
                Collapse();
                return;
            }

            m_ActiveTabIndex = index;
            AIAssistantAnalytics.ReportUITriggerLocalSuggestionCategorySelectedEvent(k_Tabs[index].Label);
            RefreshPromptList(m_SuggestionList);
            RefreshNewChatState();
        }

        void RefreshNewChatState()
        {
            var expanded = m_ActiveTabIndex >= 0;
            m_SuggestionCard.SetDisplay(expanded);
            RefreshChipStyles();
        }

        void RefreshChipStyles() => RefreshButtonStyles("mui-new-chat-chip-active");

        internal override void Collapse()
        {
            m_ActiveTabIndex = -1;
            RefreshNewChatState();
        }
    }
}
