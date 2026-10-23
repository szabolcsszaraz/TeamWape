using System.Collections.Generic;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// Where every display item of a stream sits, keyed by identity. Identity is structural and survives
    /// a content change, so this is what lets a realized slot and the focused item follow their item
    /// across the re-flatten a streamed chunk causes.
    /// </summary>
    class IdentityIndex
    {
        readonly Dictionary<long, int> m_IndexByIdentity = new();

        public void Rebuild(IReadOnlyList<DisplayItem> items)
        {
            m_IndexByIdentity.Clear();

            for (var index = 0; items != null && index < items.Count; index++)
                m_IndexByIdentity[items[index].Identity.Value] = index;
        }

        /// <summary>Index the item at <paramref name="index"/> of <paramref name="items"/> occupies in
        /// the indexed stream, or <see cref="DisplayItem.NoIndex"/> when it is gone from it.</summary>
        public int MovedIndexOf(IReadOnlyList<DisplayItem> items, int index)
        {
            if (index < 0 || index >= items.Count)
                return DisplayItem.NoIndex;

            return m_IndexByIdentity.TryGetValue(items[index].Identity.Value, out var moved) ? moved : DisplayItem.NoIndex;
        }
    }
}
