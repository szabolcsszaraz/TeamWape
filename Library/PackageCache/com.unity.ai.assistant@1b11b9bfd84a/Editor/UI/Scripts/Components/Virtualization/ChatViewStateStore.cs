using System.Collections.Generic;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// Expanded/collapsed state for the block-virtualized conversation, keyed by structural identity
    /// rather than held in the element that renders it. Under block virtualization an element is
    /// recycled whenever it leaves the window, so state kept on the element is lost; keyed here it
    /// survives scrolling away and back, and the flattener can read it to prune collapsed subtrees.
    /// Owned by the scroll surface and cleared with the conversation.
    /// </summary>
    class ChatViewStateStore : IChatViewState
    {
        readonly Dictionary<ContentKey, bool> m_ExpandedByIdentity = new();

        /// <summary>
        /// The expanded state for an identity, or <paramref name="defaultExpanded"/> when the user has
        /// never toggled it. The caller owns the default, so a per-kind policy never lands here.
        /// </summary>
        public bool IsExpanded(ContentKey identity, bool defaultExpanded)
        {
            return m_ExpandedByIdentity.TryGetValue(identity, out var expanded)
                ? expanded
                : defaultExpanded;
        }

        public void SetExpanded(ContentKey identity, bool expanded)
        {
            m_ExpandedByIdentity[identity] = expanded;
        }

        public void Clear()
        {
            m_ExpandedByIdentity.Clear();
        }
    }
}
