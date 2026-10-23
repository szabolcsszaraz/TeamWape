using System.Threading.Tasks;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.Tools.Editor
{
    class EnterPlanModeTools
    {
        const string k_ToolName = "Unity.EnterPlanMode";

        [AgentTool(
            "Switch the session into Plan mode to align with the user on approach before execution.\n\n" +
            "WHEN TO USE THIS TOOL:\n" +
            "Call this tool when the user's request would benefit from agreeing on an approach before writing code. Specifically:\n" +
            "- Building a new feature, system, or prototype from scratch.\n" +
            "- Changes that span multiple systems, scenes, or many files at once.\n" +
            "- Architecturally significant work: state machines, save systems, networking, swapping core controllers or render pipelines.\n" +
            "- Requests phrased as goals, not instructions (\"make the game feel better\", \"add multiplayer\").\n" +
            "- Substantial scope where reasonable approaches diverge — the user should choose before code is written.\n\n" +
            "WHEN NOT TO USE THIS TOOL:\n" +
            "Do not call this tool for:\n" +
            "- A single clear edit: bug fix, typo, rename, small tweak.\n" +
            "- Tasks where the user gave specific step-by-step instructions.\n" +
            "- Read-only tasks: explanation, exploration, search.\n" +
            "- Tasks where an approved plan file is already in your context — execute against it.\n" +
            "- Tasks where the user explicitly said to skip planning (\"just do it\", \"no plan needed\").\n\n" +
            "DO NOT OVER-CALL. A bias toward planning on small tasks is friction, not safety. The trigger is scope and divergence of valid approaches, not whether code is involved. When in doubt on a medium task, lean toward executing with an early check-in rather than entering Plan mode.\n\n" +
            "Behavior: Calling this tool asks the user to confirm the switch into Plan mode. If they approve, the session switches to Plan mode, you become read-only, and your next response should begin planning — explore the project, ask the user clarifying questions if needed, and draft a plan for approval. If they decline, stay in Agent mode and proceed with the request directly.",
            k_ToolName)]
        [AgentToolSettings(
            assistantMode: AssistantMode.Agent,
            toolCallEnvironment: ToolCallEnvironment.EditMode,
            tags: FunctionCallingUtilities.k_PlanningTag)]
        internal static async Task<string> EnterPlanMode(ToolExecutionContext context)
        {
            var interaction = new EnterPlanModeInteraction();

            return await context.Interactions.WaitForUser(interaction);
        }
    }
}
