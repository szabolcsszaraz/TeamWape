namespace Unity.AI.Assistant.Editor
{
    /// <summary>
    /// Project plan.md path, content, and hash helpers for the plan execution prompt.
    /// </summary>
    internal interface IPlanPromptFile
    {
        string GetPlanPath();

        string GetPathForDisplay();

        bool TryReadTruncated(out string text);
    }
}
