namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// What the call that spawned a subagent has come back with. It runs under the agent that asked for
    /// one rather than inside the group it spawned, so the group can only be told: its own calls are
    /// finished as often as not while the subagent runs, and this is the outcome it settles on.
    /// </summary>
    enum SpawnCallState
    {
        /// <summary>No spawn call in the group's sequence named its role — a conversation recorded
        /// before the tool existed, or a role nobody asked for. The group settles on its counts.</summary>
        None,
        Pending,
        Succeeded,
        Failed
    }

    /// <summary>
    /// The bind state a chrome item cannot read off a block model, because today's elements accumulate
    /// it while they build: the last item a sequence holds or a subagent group's agent name, both of
    /// which the element formats for display, a subagent header's progress, whether the section's
    /// reasoning toggle is live, what a subagent's spawn call came back with, and what each foldout is
    /// toggled to. The flattener emits one of these per display item.
    /// </summary>
    readonly struct ChatItemChrome
    {
        public ChatItemChrome(string foldoutTitle, int completedCount, int totalCount, bool toggleEnabled, bool expanded,
            SpawnCallState spawnState = SpawnCallState.None)
        {
            FoldoutTitle = foldoutTitle;
            CompletedCount = completedCount;
            TotalCount = totalCount;
            ToggleEnabled = toggleEnabled;
            Expanded = expanded;
            SpawnState = spawnState;
        }

        public string FoldoutTitle { get; }

        public int CompletedCount { get; }

        public int TotalCount { get; }

        public bool ToggleEnabled { get; }

        public bool Expanded { get; }

        public SpawnCallState SpawnState { get; }
    }
}
