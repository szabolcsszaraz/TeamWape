namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// A scroll position a caller can leave and come back to. It names an item rather than a pixel
    /// offset, so a restore is exact even after the conversation re-flattens under it.
    /// </summary>
    readonly struct ChatScrollPosition
    {
        public ChatScrollPosition(long identity, float offset, bool atEnd)
        {
            Identity = identity;
            Offset = offset;
            AtEnd = atEnd;
        }

        /// <summary>Identity key of the anchored item, or zero when nothing was anchored.</summary>
        public long Identity { get; }

        /// <summary>Pixels of the anchored item above the viewport top.</summary>
        public float Offset { get; }

        /// <summary>Captured at the bottom, so a restore returns to wherever the bottom is now
        /// rather than to the item that happened to be last.</summary>
        public bool AtEnd { get; }
    }
}
