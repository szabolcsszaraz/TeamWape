namespace Unity.AI.Assistant.Backend
{
    /// <summary>
    /// A Unity model profile available to the user: provider id, profile display name,
    /// and an optional tooltip string from the backend.
    /// </summary>
    record ModelProfile(string ProviderId, string ProfileName, string Tooltip);
}
