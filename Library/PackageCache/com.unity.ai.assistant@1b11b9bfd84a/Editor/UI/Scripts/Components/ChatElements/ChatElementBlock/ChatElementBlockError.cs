using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementBlockError : ChatElementBlockBase<ErrorBlockModel>
    {
        Label m_ErrorText;
        string m_LastReportedError;

        protected override void InitializeView(TemplateContainer view)
        {
            base.InitializeView(view);

            m_ErrorText = view.Q<Label>("errorText");
            m_ErrorText.selection.isSelectable = true;

            RegisterAttachEvents(OnAttachToPanel, OnDetachFromPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt) => TryReportErrorDisplayed();
        void OnDetachFromPanel(DetachFromPanelEvent evt) => m_LastReportedError = null;

        protected override void OnBlockModelChanged()
        {
            m_ErrorText.text = BlockModel.Error;
            TryReportErrorDisplayed();
        }

        void TryReportErrorDisplayed()
        {
            if (panel == null ||
                BlockModel == null ||
                string.IsNullOrEmpty(BlockModel.Error))
                return;

            if (m_LastReportedError == BlockModel.Error) return;

            m_LastReportedError = BlockModel.Error;
            var conversationId = Context?.Blackboard?.ActiveConversationId ?? default;
            AIAssistantAnalytics.ReportUITriggerLocalErrorDisplayedEvent(AIAssistantErrorType.k_ChatErrorBlock, conversationId, BlockModel.Error);
        }
    }
}
