using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    /// <summary>
    /// Groups consecutive reasoning content (thoughts and function calls) into a collapsible foldout,
    /// whose items are laid out beside it. Expanded it shows them all; collapsed it names the last one.
    /// </summary>
    class ChatElementReasoningSequence : ManagedTemplate
    {
        const string k_ThoughtsTitle = "Thoughts";

        Foldout m_Foldout;
        string m_LastTitle;

        public ChatElementReasoningSequence() : base(AssistantUIConstants.UIModulePath)
        {
            SetResourceName("ChatElementReasoningSequence");
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Foldout = view.Q<Foldout>("reasoningFoldout");
            m_Foldout.SetValueWithoutNotify(true); // Start expanded

            m_Foldout.RegisterValueChangedCallback(OnFoldoutChanged);
            UpdateFoldoutTitle();
        }

        /// <summary>
        /// Applies the expansion and the title of the last item the flattener put in a virtualized
        /// sequence, whose thoughts and calls are laid out as items of their own. Binding is not a user
        /// gesture, so the foldout is set without notification and no height change is reported.
        /// </summary>
        public void SetChromeState(bool expanded, string lastTitle)
        {
            m_LastTitle = lastTitle;
            m_Foldout.SetValueWithoutNotify(expanded);
            UpdateFoldoutTitle();
        }

        void OnFoldoutChanged(ChangeEvent<bool> evt)
        {
            // The title the bind computed no longer holds: what the sequence names depends on whether it
            // shows its items, and the re-flatten the report below asks for is a pass away.
            UpdateFoldoutTitle();
            this.ReportHeightChange(evt.newValue);
        }

        void UpdateFoldoutTitle()
        {
            m_Foldout.text = m_Foldout.value || string.IsNullOrEmpty(m_LastTitle)
                ? k_ThoughtsTitle
                : k_ThoughtsTitle + " - " + m_LastTitle;
        }
    }
}
