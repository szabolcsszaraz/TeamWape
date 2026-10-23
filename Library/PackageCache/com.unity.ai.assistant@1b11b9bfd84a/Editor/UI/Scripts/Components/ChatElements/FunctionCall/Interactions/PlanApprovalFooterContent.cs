using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class PlanApprovalFooterContent : InteractionContentView
    {
        internal ExitPlanModeInteractionElement InlineElement { get; }

        public PlanApprovalFooterContent(ExitPlanModeInteractionElement inlineElement)
        {
            InlineElement = inlineElement;
        }

        protected override void InitializeView(TemplateContainer view)
        {
            view.SetupButton("denyButton", _ => InlineElement.OnDeny());
            view.SetupButton("approveButton", _ => InlineElement.OnApprove());
        }
    }
}
