namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// The authoritative scroll position as a snapshot: the anchored display item and how many pixels
    /// of it sit above the viewport top.
    /// </summary>
    readonly struct ItemAnchor
    {
        public ItemAnchor(int item, float offset)
        {
            Item = item;
            Offset = offset;
        }

        public int Item { get; }

        public float Offset { get; }
    }
}
