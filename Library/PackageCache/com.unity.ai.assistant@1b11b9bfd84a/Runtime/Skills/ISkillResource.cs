namespace Unity.AI.Assistant.Skills
{
    /// <summary>
    /// Interface for skill resources that can provide their content on-demand.
    /// </summary>
    interface ISkillResource
    {
        /// <summary>
        /// The length of the text content.
        /// </summary>
        int Length { get; }

        /// <summary>
        /// Gets the content of the resource.
        /// </summary>
        /// <param name="maxChars">Content budget of the caller. Implementations return at most
        /// maxChars + 1 characters — one past the budget, so a caller that truncates can still
        /// detect the overflow — and may avoid materializing anything beyond that.</param>
        /// <returns>The resource content as a string</returns>
        string GetContent(int maxChars = int.MaxValue);
    }
}
