namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// Read-only view over per-item heights, implemented by <see cref="ItemHeightModel"/>. The window
    /// calculator and the slot positioner take this rather than the model itself so neither can move a
    /// height while it is reading one.
    /// </summary>
    interface IItemHeights
    {
        int Count { get; }

        float Height(int index);

        /// <summary>Whether the height came from a real layout pass. An item that has never been
        /// measured may be positioned, but must not be shown at that position.</summary>
        bool IsMeasured(int index);
    }
}
