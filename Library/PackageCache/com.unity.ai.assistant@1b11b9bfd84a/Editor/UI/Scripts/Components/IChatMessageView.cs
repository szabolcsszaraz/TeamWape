using System;
using System.Collections.Generic;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    /// <summary>
    /// What the conversation panel and the search helper need from whatever renders the message list.
    /// A scroll position is named by the item it anchors rather than by a pixel offset, because under
    /// block virtualization a pixel offset stops being authoritative the moment a height is measured.
    /// </summary>
    interface IChatMessageView
    {
        /// <summary>The element to place in the visual tree.</summary>
        VisualElement AsVisualElement { get; }

        /// <summary>The realized, on-screen content roots, in item order. What a search highlight walks:
        /// the visual tree also holds the items that are recycled or kept alive off-window, which carry
        /// the text they last rendered and are on no reader's screen.</summary>
        IEnumerable<VisualElement> HighlightRoots { get; }

        /// <summary>The displayed message list, including the synthetic checkpoint and reverted-link
        /// entries the panel injects.</summary>
        IList<MessageModel> Data { get; }

        bool HasContent { get; }

        bool IsAtBottom { get; }

        bool CanScrollDown { get; }

        /// <summary>Fires once per <see cref="BeginUpdate"/>, when the conversation is on screen.</summary>
        event Action ElementsPopulated;

        event Action UserScrolled;

        event Action GeometryChanged;

        /// <summary>Mandatory, and before <see cref="AsVisualElement"/> reaches a panel.</summary>
        void Initialize(AssistantUIContext context);

        void AddData(MessageModel item);

        void UpdateData(int index, MessageModel data);

        void RemoveData(int index);

        void ClearData();

        /// <summary>
        /// Rebuilds what a message renders although its model has not changed, for validity that lives
        /// outside the model: a checkpoint's git tag is written after the turn it belongs to ends, so the
        /// element has to read the world again to find out it became restorable.
        /// </summary>
        void RefreshMessage(int index);

        void BeginUpdate();

        void EndUpdate(bool scrollToEnd = true);

        void ScrollToEnd();

        /// <summary>Scrolls to the end unless the user has scrolled away from it.</summary>
        void ScrollToEndIfNotLocked();

        ChatScrollPosition CapturePosition();

        void RestorePosition(ChatScrollPosition position);

        /// <summary>
        /// The topmost message on screen, counted the way <see cref="IsBlockPopulated"/> and
        /// <see cref="PopulateBlock"/> count: every index this seam takes or returns names one of the
        /// conversation's own messages, never an entry of <see cref="Data"/>, so that a search result
        /// and a viewport position can be compared without either side translating.
        /// </summary>
        int GetFirstItemInView();

        /// <summary>
        /// Whether the block is on screen with its text elements built, so a highlight pass can find
        /// them. A negative <paramref name="blockIndex"/> asks about any block of the message.
        /// </summary>
        bool IsBlockPopulated(int messageIndex, int blockIndex);

        /// <summary>
        /// Builds the block and brings it into view, ignoring the frame budget — a search jump is a
        /// user-initiated single-item cost. A block hidden inside a collapsed foldout is revealed by
        /// opening it. A negative <paramref name="blockIndex"/> targets the message's first block.
        /// </summary>
        void PopulateBlock(int messageIndex, int blockIndex);

        void ScrollDownBy(float positionY);
    }
}
