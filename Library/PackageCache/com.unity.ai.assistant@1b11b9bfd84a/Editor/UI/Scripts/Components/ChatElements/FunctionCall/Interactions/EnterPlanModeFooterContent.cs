using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Tools.Editor;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    /// <summary>
    /// Approval footer for Unity.EnterPlanMode: two right-aligned buttons asking the user to
    /// confirm the switch into Plan mode or stay in Agent mode. Forwards clicks to the
    /// tools-side <see cref="EnterPlanModeInteraction"/>.
    /// </summary>
    class EnterPlanModeFooterContent : InteractionContentView
    {
        readonly EnterPlanModeInteraction m_Interaction;
        bool m_Completed;

        public EnterPlanModeFooterContent(EnterPlanModeInteraction interaction)
        {
            m_Interaction = interaction;
        }

        protected override void InitializeView(TemplateContainer view)
        {
            view.SetupButton("denyButton", _ => OnDeny());
            view.SetupButton("approveButton", _ => OnApprove());
        }

        void OnApprove()
        {
            if (m_Completed) return;
            m_Completed = true;

            AIAssistantAnalytics.ReportUITriggerLocalEnterPlanModeApprovedEvent(Context.Blackboard.ActiveConversationId);
            m_Interaction.Approve();
        }

        void OnDeny()
        {
            if (m_Completed) return;
            m_Completed = true;

            AIAssistantAnalytics.ReportUITriggerLocalEnterPlanModeDeclinedEvent(Context.Blackboard.ActiveConversationId);
            m_Interaction.Deny();
        }
    }
}
