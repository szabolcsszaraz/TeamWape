using System.Threading.Tasks;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.Tools.Editor
{
    class ExitPlanModeTools
    {
        const string k_ToolName = "Unity.ExitPlanMode";

        [AgentTool(
            "Signals that the planning phase is complete and requests user approval to start implementation. " +
            "Call this ONLY after the plan file has been created with Unity.WritePlan or revised with Unity.EditPlan, " +
            "and saved under " + PlanModeTools.PlansRelativePath + "/. " +
            "The plan file must exist and contain a valid implementation plan before calling Unity.ExitPlanMode. " +
            "The user will be shown the plan content inline with options to: " +
            "(1) Approve and auto-accept edits, (2) Approve with manual edit confirmation, " +
            "(3) Send feedback for plan revision, or (4) Cancel. " +
            "If approved, you will receive instructions to execute the plan. " +
            "If feedback is provided, revise the plan with Unity.EditPlan and call Unity.ExitPlanMode again. " +
            "If cancelled, remain in plan mode and wait for further user input.",
            k_ToolName)]
        [AgentToolSettings(
            assistantMode: AssistantMode.Plan,
            toolCallEnvironment: ToolCallEnvironment.EditMode,
            tags: FunctionCallingUtilities.k_PlanningTag)]
        internal static async Task<string> ExitPlanMode(
            ToolExecutionContext context,
            [ToolParameter("Absolute or project-relative path to the plan file (e.g. " + PlanModeTools.PlansRelativePath + "/feature-x.md). The file must already exist.")]
            string planPath,
            [ToolParameter("Short title for the plan shown in the UI header. Summarize what is being built or changed. Max 80 characters.")]
            string title = null)
        {
            var resolvedPath = PlanModeTools.ResolveExistingPlanPath(
                planPath,
                "Use Unity.WritePlan to create new plan files before calling Unity.ExitPlanMode.");
            var planContent = ExitPlanModeInteraction.ReadPlanFile(resolvedPath);

            var conversationId = context.Conversation?.ConversationId;
            var callId = context.Call.CallId;

            var interaction = new ExitPlanModeInteraction(planPath, planContent, title);

            // Preserve the existing expanded flag on replay; a blind false would clobber it before restore re-expands.
            var existingExpanded = ExitPlanModeStateStore.instance.GetExpanded(callId);

            ExitPlanModeStateStore.instance.SetState(callId, conversationId, planPath, planContent, title, existingExpanded);

            try
            {
                return await context.Interactions.WaitForUser(interaction);
            }
            finally
            {
                ExitPlanModeStateStore.instance.ClearState(callId);
            }
        }
    }
}
