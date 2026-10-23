namespace Unity.AI.Assistant.FunctionCalling
{
    /// <summary>
    /// A user-facing approval prompt that asks the user to allow or deny an action
    /// initiated by a tool.
    /// </summary>
    public interface IApprovalInteraction
    {
        /// <summary>
        /// Short description of the action being requested (for example, "Delete file").
        /// </summary>
        string Action { get; }

        /// <summary>
        /// Longer description of the action, providing the user with the details needed
        /// to make an informed decision.
        /// </summary>
        string Detail { get; }

        /// <summary>
        /// Label displayed on the allow button.
        /// </summary>
        string AllowLabel { get; }

        /// <summary>
        /// Label displayed on the deny button.
        /// </summary>
        string DenyLabel { get; }

        /// <summary>
        /// When <c>true</c>, the prompt exposes the "always/once" scope choice, allowing
        /// the user to grant or deny the permission persistently. When <c>false</c>, only
        /// once-scoped answers are accepted.
        /// </summary>
        bool ShowScope { get; }

        /// <summary>
        /// Submits the user's response to the approval prompt.
        /// </summary>
        /// <param name="answer">The user's answer, including its scope.</param>
        void Respond(PermissionUserAnswer answer);
    }
}
