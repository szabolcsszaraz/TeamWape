using Unity.AI.Assistant.Editor.Utils.Event;

namespace Unity.AI.Assistant.Editor.Checkpoint.Events
{
    /// <summary>
    /// Asks the checkpoint discovery banner to re-evaluate its state. Raised when the user clears the
    /// "dismissed" flag from Preferences ("Show Banner Again"), so an open banner reappears immediately
    /// instead of waiting for the next window open.
    /// </summary>
    class EventCheckpointDiscoveryBannerRefreshRequested : IAssistantEvent
    {
    }
}
