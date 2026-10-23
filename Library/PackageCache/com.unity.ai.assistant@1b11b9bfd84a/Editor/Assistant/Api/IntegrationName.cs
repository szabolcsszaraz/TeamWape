namespace Unity.AI.Assistant.Editor.Api
{
    /// <summary>
    /// Identifies a known integration that can open the AI Assistant via <see cref="AssistantApi.PromptThenRunInternal(UnityEngine.Rect, string, AttachedContext, AssistantMode, string, System.Threading.CancellationToken, IntegrationName?)"/>.
    /// Used as the analytics discriminator for the <c>opened_via_integration</c> event.
    /// </summary>
    internal enum IntegrationName
    {
        CpuProfiler,
        ProjectAuditor
    }
}
