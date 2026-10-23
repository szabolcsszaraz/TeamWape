using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class AskUserQuestionPane : ManagedTemplate
    {
        const string k_PaneClass = "ask-user-tab-pane";
        const string k_PaneActiveClass = "ask-user-tab-pane-active";
        const string k_SkippedChipVisibleClass = "ask-user-skipped-chip--visible";

        Label m_QuestionLabel;
        Label m_SkippedLabel;

        public VisualElement ContentSlot { get; private set; }

        public AskUserQuestionPane()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            AddToClassList(k_PaneClass);
            m_QuestionLabel = view.Q<Label>("questionLabel");
            m_SkippedLabel = view.Q<Label>("skippedLabel");
            ContentSlot = view.Q<VisualElement>("questionContent");
        }

        public void SetQuestion(string question)
        {
            m_QuestionLabel.text = question;
        }

        public void SetActive(bool active)
        {
            EnableInClassList(k_PaneActiveClass, active);
        }

        public void SetSkipped(bool skipped)
        {
            m_SkippedLabel?.EnableInClassList(k_SkippedChipVisibleClass, skipped);
        }
    }
}
