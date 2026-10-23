using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementResponseSection : ManagedTemplate
    {
        const string k_ReasoningHiddenClass = "mui-reasoning-hidden";

        VisualElement m_ReasoningSection;
        VisualElement m_ReasoningTitle;
        Foldout m_ReasoningFoldout;
        VisualElement m_ReasoningContainer;
        VisualElement m_ReasoningLoadingSpinnerContainer;
        LoadingSpinner m_ReasoningLoadingSpinner;
        bool m_IsToggleEnabled;
        bool m_IsWorking = true;
        bool m_HasAnswerContent;

        public ChatElementResponseSection() : base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_ReasoningSection = view.Q("reasoningContainer");
            m_ReasoningSection.AddToClassList(k_ReasoningHiddenClass);

            m_ReasoningTitle = view.Q("reasoningTitle");
            m_ReasoningTitle.RegisterCallback<ClickEvent>(OnTitleClicked);
            m_ReasoningFoldout = view.Q<Foldout>("reasoningFoldout");
            m_ReasoningFoldout.value = true;

            m_ReasoningFoldout.RegisterValueChangedCallback(OnFoldoutValueChanged);

            var toggle = m_ReasoningFoldout.Q<Toggle>();
            toggle.RegisterCallback<ClickEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);

            m_ReasoningFoldout.SetDisplay(false);
            m_IsToggleEnabled = false;

            m_ReasoningContainer = view.Q("reasoningContent");

            m_ReasoningLoadingSpinnerContainer = view.Q("reasoningLoadingSpinnerContainer");
            m_ReasoningLoadingSpinner = new LoadingSpinner();
            m_ReasoningLoadingSpinner.style.marginRight = 4;
            m_ReasoningLoadingSpinner.Show();
            m_ReasoningLoadingSpinnerContainer.Add(m_ReasoningLoadingSpinner);
        }

        /// <summary>
        /// Applies the reasoning chrome a virtualized section is bound with, whose reasoning blocks and
        /// answers are laid out as items of their own. Binding is not a user gesture, so the foldout is
        /// set without notification and no height change is reported.
        /// </summary>
        public void SetChromeState(bool hasReasoning, bool toggleEnabled, bool expanded)
        {
            m_ReasoningSection.EnableInClassList(k_ReasoningHiddenClass, !hasReasoning);
            m_ReasoningFoldout.SetDisplay(toggleEnabled);
            m_IsToggleEnabled = toggleEnabled;
            m_ReasoningFoldout.SetValueWithoutNotify(expanded);
            m_ReasoningContainer.SetDisplay(expanded);
        }

        /// <summary>
        /// Sets whether this section's turn is still running, and whether it has already produced answer
        /// content — which a virtualized section renders as items beside it rather than as children of
        /// <c>#answerContent</c>, so its own container can no longer answer that.
        /// </summary>
        public void SetIsWorkingState(bool isWorking, bool hasAnswerContent)
        {
            if (m_IsWorking == isWorking && m_HasAnswerContent == hasAnswerContent)
                return;

            m_IsWorking = isWorking;
            m_HasAnswerContent = hasAnswerContent;
            UpdateLoadingSpinner();
        }

        void UpdateLoadingSpinner()
        {
            if (m_IsWorking && !m_HasAnswerContent)
                m_ReasoningLoadingSpinner.Show();
            else
                m_ReasoningLoadingSpinner.Hide();
        }

        void OnTitleClicked(ClickEvent evt)
        {
            // Only allow toggling if response block has been created
            if (!m_IsToggleEnabled)
                return;

            DisplayReasoning(!m_ReasoningFoldout.value);
        }

        void DisplayReasoning(bool isVisible)
        {
            m_ReasoningFoldout.value = isVisible;
            m_ReasoningContainer.SetDisplay(isVisible);
        }

        void OnFoldoutValueChanged(ChangeEvent<bool> evt)
        {
            if (!m_IsToggleEnabled)
                return;

            DisplayReasoning(evt.newValue);

            // Both gestures land here: OnTitleClicked flips the foldout's value rather than displaying
            // the reasoning itself, so this is the one place a user-driven toggle can be reported from.
            this.ReportHeightChange(evt.newValue);
        }
    }
}
