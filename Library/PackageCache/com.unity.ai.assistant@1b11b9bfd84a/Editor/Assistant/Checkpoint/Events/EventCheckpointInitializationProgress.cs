using Unity.AI.Assistant.Editor.Utils.Event;

namespace Unity.AI.Assistant.Editor.Checkpoint.Events
{
    /// <summary>
    /// Raised as checkpoint initialization moves through its phases (repository init, asset save,
    /// staging, commit, verification). Git does not report fine-grained progress for staging/committing
    /// a local working tree, so <see cref="Progress"/> advances at real phase boundaries rather than
    /// continuously. The discovery banner uses it to drive a determinate progress bar.
    /// </summary>
    class EventCheckpointInitializationProgress : IAssistantEvent
    {
        public EventCheckpointInitializationProgress(float progress, string phase)
        {
            Progress = progress;
            Phase = phase;
        }

        /// <summary>Overall initialization progress in the range [0, 1].</summary>
        public float Progress { get; }

        /// <summary>Human-readable label for the current phase.</summary>
        public string Phase { get; }
    }
}
