namespace Unity.AI.Assistant.Editor.Analytics
{
    /// <summary>
    /// Discriminator values for the <c>ErrorType</c> field on <c>error_displayed</c> analytics events.
    /// Centralized so call sites cannot drift from the documented set in AIAssistantAnalytics.md.
    /// </summary>
    internal static class AIAssistantErrorType
    {
        public const string k_RelayStopped = "relay_stopped";
        public const string k_ServerIncompatible = "server_incompatible";
        public const string k_AcpSessionError = "acp_session_error";
        public const string k_AcpCredentialError = "acp_credential_error";
        public const string k_AcpProviderUnavailable = "acp_provider_unavailable";
        public const string k_ChatErrorBlock = "chat_error_block";
        public const string k_NoCapacity = "no_capacity";
    }
}
