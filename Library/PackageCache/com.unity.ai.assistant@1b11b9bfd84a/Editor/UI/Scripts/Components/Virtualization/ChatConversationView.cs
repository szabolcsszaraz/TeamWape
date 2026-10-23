using System;
using System.Collections.Generic;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Utils.Perf;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// The conversation the panel talks to: the displayed message list, the display-item stream
    /// flattened from it, and the surface and realizer that render a window of that stream. A data
    /// change only marks the stream dirty, so a turn that touches fifty messages in one event
    /// re-flattens once — on the pass that follows it — rather than once per message.
    /// </summary>
    class ChatConversationView : IChatMessageView, IDisposable
    {
        readonly List<MessageModel> m_Data = new();
        readonly ChatScrollSurface m_Surface = new();
        readonly Dictionary<(int Message, int Block), int> m_BlockItems = new();
        readonly Dictionary<int, (int First, int LastExclusive)> m_MessageRanges = new();

        AssistantUIContext m_Context;
        ChatItemRealizer m_Realizer;
        IReadOnlyList<DisplayItem> m_Items = Array.Empty<DisplayItem>();

        bool m_StreamDirty;
        bool m_BlockIndexDirty;
        bool m_InUpdate;
        int m_FlattensSincePopulate;

        public ChatConversationView()
        {
            m_Surface.PassStarting += EnsureFlattened;
            m_Surface.ReflattenRequested += MarkDirty;
            m_Surface.RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            m_Surface.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public VisualElement AsVisualElement => m_Surface;

        public IEnumerable<VisualElement> HighlightRoots => ChatSlot.ShownContentInReadingOrder(m_Surface.Content);

        public IList<MessageModel> Data => m_Data;

        public bool HasContent => m_Data.Count > 0;

        public bool IsAtBottom => m_Surface.Model.IsAtBottom(m_Surface.ViewportHeight);

        public bool CanScrollDown
            => m_Surface.Model.CanScrollDown(m_Surface.ViewportHeight, m_Context.Blackboard.IsAPIWorking);

        public event Action ElementsPopulated
        {
            add => m_Surface.FirstPaintCompleted += value;
            remove => m_Surface.FirstPaintCompleted -= value;
        }

        public event Action UserScrolled
        {
            add => m_Surface.UserScrolled += value;
            remove => m_Surface.UserScrolled -= value;
        }

        public event Action GeometryChanged
        {
            add => m_Surface.GeometryChanged += value;
            remove => m_Surface.GeometryChanged -= value;
        }

        public void Initialize(AssistantUIContext context)
        {
            m_Context = context;
            m_Realizer = new ChatItemRealizer(m_Surface, context);
        }

        public void AddData(MessageModel item)
        {
            m_Data.Add(item);
            MarkDirty();
        }

        public void UpdateData(int index, MessageModel data)
        {
            // Every conversation-changed event re-sends every message, so most updates during a load
            // carry what the stream already describes. Comparing one message is far cheaper than
            // flattening the conversation again for it.
            var sameStream = DescribesSameStream(m_Data[index], data);
            m_Data[index] = data;

            if (!sameStream)
                MarkDirty();
        }

        public void RemoveData(int index)
        {
            m_Data.RemoveAt(index);
            MarkDirty();
        }

        public void ClearData()
        {
            m_Data.Clear();
            m_Realizer.Clear();

            // The view state is per conversation: an identity is structural, so the next conversation's
            // items can key onto what the last one's user toggled.
            m_Surface.ViewState.Clear();
            m_Surface.Model.StickToBottom = true;
            Flatten();
        }

        public void RefreshMessage(int index)
        {
            // After any pending re-flatten, so the rebind lands on the items the message has now.
            EnsureFlattened();
            m_Realizer.RebindMessage(index);
        }

        public void BeginUpdate()
        {
            m_InUpdate = true;
            m_FlattensSincePopulate = 0;
            m_Surface.BeginFirstPaint();
        }

        public void EndUpdate(bool scrollToEnd = true)
        {
            m_InUpdate = false;

            if (scrollToEnd)
                m_Surface.Model.StickToBottom = true;

            Flatten();
        }

        public void ScrollToEnd() => m_Surface.ScrollToEnd();

        /// <summary>Lands the view on the newest message once and then leaves it to the reader. A snapshot
        /// has no tail to follow, and it is built before it is shown, so the landing has to outlive the
        /// first paint rather than be spent by whatever position the empty viewport implies.</summary>
        public void LandAtEndOnce() => m_Surface.LandAtEndOnce();

        /// <summary>Shows the conversation without letting anything in it be operated: the same disabled
        /// content the checkpoint panel has always shown, foldouts included.</summary>
        public void SetContentEnabled(bool enabled) => m_Surface.Content.SetEnabled(enabled);

        public void ScrollToEndIfNotLocked()
        {
            // Sticking to the bottom is what the legacy list called the unlocked state: a user who
            // scrolled away cleared it, and a streamed chunk must not drag them back down.
            if (m_Surface.Model.StickToBottom)
                m_Surface.ScrollToEnd();
        }

        public ChatScrollPosition CapturePosition()
        {
            EnsureFlattened();
            return m_Surface.Model.Capture();
        }

        public void RestorePosition(ChatScrollPosition position)
        {
            EnsureFlattened();
            m_Surface.RestorePosition(position, m_Items);
        }

        public int GetFirstItemInView()
        {
            EnsureFlattened();

            for (var index = m_Surface.Model.AnchorItem; index < m_Items.Count; index++)
            {
                if (m_Items[index].MessageIndex >= 0)
                    return FromDisplayIndex(m_Items[index].MessageIndex);
            }

            return FromDisplayIndex(m_Data.Count - 1);
        }

        public bool IsBlockPopulated(int messageIndex, int blockIndex)
        {
            EnsureFlattened();
            EnsureBlocksIndexed();

            messageIndex = ToDisplayIndex(messageIndex);

            if (blockIndex >= 0)
                return IsOnScreen(ItemOfBlock(messageIndex, blockIndex));

            if (!m_MessageRanges.TryGetValue(messageIndex, out var range))
                return false;

            for (var index = range.First; index < range.LastExclusive; index++)
            {
                if (IsOnScreen(index))
                    return true;
            }

            return false;
        }

        public void PopulateBlock(int messageIndex, int blockIndex)
        {
            EnsureFlattened();
            EnsureBlocksIndexed();

            messageIndex = ToDisplayIndex(messageIndex);
            var target = blockIndex >= 0 ? ItemOfBlock(messageIndex, blockIndex) : FirstItemOfMessage(messageIndex);

            if (target == DisplayItem.NoIndex && blockIndex >= 0 && messageIndex != DisplayItem.NoIndex)
                target = RevealBlock(messageIndex, blockIndex);

            // Already on screen means the highlighter can find its labels and will scroll to the match
            // itself, which is finer than anchoring the whole block to the viewport top.
            if (target == DisplayItem.NoIndex || IsOnScreen(target))
                return;

            m_Realizer.Flush(target);
            m_Surface.ScrollTo(target, 0f);
        }

        public void ScrollDownBy(float positionY) => m_Surface.ScrollBy(positionY);

        public void Dispose()
        {
            m_Surface.PassStarting -= EnsureFlattened;
            m_Surface.ReflattenRequested -= MarkDirty;
            m_Surface.UnregisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            m_Surface.UnregisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            if (m_Context != null)
                m_Context.API.APIStateChanged -= OnApiStateChanged;

            m_Realizer?.Dispose();
        }

        /// <summary>
        /// Where a message of the conversation sits in the displayed list. Search counts the
        /// conversation's own messages, while the panel prepends a synthetic initial checkpoint and
        /// injects a synthetic link per reverted group, so the two lists agree only on the order of the
        /// messages themselves: the n-th real entry of the display list is the n-th message.
        /// </summary>
        int ToDisplayIndex(int messageIndex)
        {
            if (messageIndex < 0)
                return DisplayItem.NoIndex;

            var remaining = messageIndex;

            for (var index = 0; index < m_Data.Count; index++)
            {
                if (m_Data[index].IsInitialCheckpoint || m_Data[index].IsRevertedTimeStampLink)
                    continue;

                if (remaining == 0)
                    return index;

                remaining--;
            }

            return DisplayItem.NoIndex;
        }

        /// <summary>
        /// The inverse of <see cref="ToDisplayIndex"/>: which of the conversation's own messages a
        /// displayed entry is. A synthetic entry is none of them, so it answers with the message that
        /// follows it, which is the nearest one a search can name.
        /// </summary>
        int FromDisplayIndex(int displayIndex)
        {
            if (displayIndex < 0)
                return DisplayItem.NoIndex;

            var messageIndex = 0;

            for (var index = 0; index < displayIndex && index < m_Data.Count; index++)
            {
                if (!m_Data[index].IsInitialCheckpoint && !m_Data[index].IsRevertedTimeStampLink)
                    messageIndex++;
            }

            return messageIndex;
        }

        int ItemOfBlock(int messageIndex, int blockIndex)
            => m_BlockItems.TryGetValue((messageIndex, blockIndex), out var index) ? index : DisplayItem.NoIndex;

        int FirstItemOfMessage(int messageIndex)
            => m_MessageRanges.TryGetValue(messageIndex, out var range) ? range.First : DisplayItem.NoIndex;

        bool IsOnScreen(int index)
            => index != DisplayItem.NoIndex && m_Realizer.TryGetSlot(index, out var slot) && slot.IsShowingContent;

        /// <summary>
        /// Opens the foldouts of the message that hide the block, and answers with the item that renders
        /// it. Collapsing prunes the subtree rather than hiding it, so a match inside a closed reasoning
        /// sequence — which is how every completed one is left — has no item to realize until the
        /// sequence is open. What the block turns out not to sit under is closed again, so a jump into
        /// one sequence does not unfold the rest of the message.
        /// </summary>
        int RevealBlock(int messageIndex, int blockIndex)
        {
            var opened = new List<ContentKey>();
            var target = DisplayItem.NoIndex;

            while (target == DisplayItem.NoIndex && OpenCollapsedFoldoutsOf(messageIndex, opened))
                target = ItemOfBlock(messageIndex, blockIndex);

            if (CloseFoldoutsBeside(opened, target))
                target = ItemOfBlock(messageIndex, blockIndex);

            return target;
        }

        /// <summary>Opens every collapsed foldout the message shows now, and answers whether it opened
        /// any. One call reaches one level: a subagent group inside a closed sequence is not in the
        /// stream to be found until the sequence itself is open.</summary>
        bool OpenCollapsedFoldoutsOf(int messageIndex, List<ContentKey> opened)
        {
            var before = opened.Count;

            for (var index = 0; index < m_Items.Count; index++)
            {
                var item = m_Items[index];

                // A container that covers nothing is a collapsed one: an emitted group always has a child.
                if (item.MessageIndex != messageIndex || !item.IsContainer || item.Span != 0)
                    continue;

                if (TryGetFoldoutKey(index, out var key) && !opened.Contains(key))
                    opened.Add(key);
            }

            if (opened.Count == before)
                return false;

            for (var index = before; index < opened.Count; index++)
                m_Surface.SetExpanded(opened[index], true);

            EnsureFlattened();
            EnsureBlocksIndexed();
            return true;
        }

        bool CloseFoldoutsBeside(List<ContentKey> opened, int target)
        {
            var closed = false;

            foreach (var key in opened)
            {
                if (Covers(target, key))
                    continue;

                m_Surface.SetExpanded(key, false);
                closed = true;
            }

            if (closed)
            {
                EnsureFlattened();
                EnsureBlocksIndexed();
            }

            return closed;
        }

        bool Covers(int target, ContentKey key)
        {
            for (var index = target; index != DisplayItem.NoIndex; index = m_Items[index].Parent)
            {
                if (TryGetFoldoutKey(index, out var ancestor) && ancestor == key)
                    return true;
            }

            return false;
        }

        /// <summary>Which view-state entry opens the container. A reasoning group is toggled by the
        /// foldout its section draws, so its state is keyed by the section rather than by itself.</summary>
        bool TryGetFoldoutKey(int index, out ContentKey key)
        {
            var item = m_Items[index];

            switch (item.Kind)
            {
                case DisplayItemKind.ReasoningSequence:
                case DisplayItemKind.SubagentGroup:
                    key = item.Identity;
                    return true;
                case DisplayItemKind.ReasoningGroup when item.Parent != DisplayItem.NoIndex:
                    key = m_Items[item.Parent].Identity;
                    return true;
                default:
                    key = default;
                    return false;
            }
        }

        /// <summary>
        /// Maps every block of the conversation to the item that renders it. Search asks about one block
        /// per match it found, which a scan of the stream would answer in time proportional to the whole
        /// conversation; the map is built when a search first asks rather than on every re-flatten, which
        /// a streamed chunk triggers and no search reads.
        /// </summary>
        void EnsureBlocksIndexed()
        {
            if (!m_BlockIndexDirty)
                return;

            m_BlockIndexDirty = false;
            m_BlockItems.Clear();
            m_MessageRanges.Clear();

            for (var index = 0; index < m_Items.Count; index++)
            {
                var item = m_Items[index];
                var messageIndex = item.MessageIndex;

                if (messageIndex < 0)
                    continue;

                m_MessageRanges[messageIndex] = m_MessageRanges.TryGetValue(messageIndex, out var range)
                    ? (range.First, index + 1)
                    : (index, index + 1);

                if (item.BlockIndex >= 0)
                    m_BlockItems[(messageIndex, item.BlockIndex)] = index;
                else if (RendersEveryBlock(item.Kind))
                    IndexEveryBlockOf(messageIndex, index);
            }
        }

        // A user prompt draws the whole message in one label rather than one item per block, so its item
        // is where a match in any of that message's blocks shows up.
        static bool RendersEveryBlock(DisplayItemKind kind)
            => kind is DisplayItemKind.UserPrompt or DisplayItemKind.Checkpoint or DisplayItemKind.RevertedLink;

        void IndexEveryBlockOf(int messageIndex, int index)
        {
            var blocks = m_Data[messageIndex].Blocks;

            for (var blockIndex = 0; blocks != null && blockIndex < blocks.Count; blockIndex++)
                m_BlockItems.TryAdd((messageIndex, blockIndex), index);
        }

        /// <summary>
        /// Whether the flattener would emit the same items for the incoming message as for the one it
        /// replaces. Every field of <see cref="MessageModel"/> is compared, because each one reaches
        /// either the display items themselves or the keys that carry their heights and view state.
        /// </summary>
        static bool DescribesSameStream(in MessageModel existing, in MessageModel incoming)
        {
            if (existing.Id != incoming.Id
                || existing.Role != incoming.Role
                || existing.IsComplete != incoming.IsComplete
                || existing.Timestamp != incoming.Timestamp
                || existing.RevertedTimeStamp != incoming.RevertedTimeStamp
                || existing.IsRevertedTimeStampLink != incoming.IsRevertedTimeStampLink
                || existing.IsInitialCheckpoint != incoming.IsInitialCheckpoint
                || existing.HasCheckpoint != incoming.HasCheckpoint
                || !existing.Feedback.Equals(incoming.Feedback)
                || !ArrayUtils.ArrayEquals(existing.Context, incoming.Context))
                return false;

            var existingBlocks = existing.Blocks;
            var incomingBlocks = incoming.Blocks;

            if (ReferenceEquals(existingBlocks, incomingBlocks))
                return true;

            if (existingBlocks == null || incomingBlocks == null || existingBlocks.Count != incomingBlocks.Count)
                return false;

            for (var index = 0; index < existingBlocks.Count; index++)
            {
                if (!existingBlocks[index].Equals(incomingBlocks[index]))
                    return false;
            }

            return true;
        }

        void OnAttachToPanel(AttachToPanelEvent evt) => m_Context.API.APIStateChanged += OnApiStateChanged;

        void OnDetachFromPanel(DetachFromPanelEvent evt) => m_Context.API.APIStateChanged -= OnApiStateChanged;

        void OnApiStateChanged()
        {
            using var perfScope = AssistantPerf.Measure(PerfProbe.ResponseApiStateChanged);

            m_Realizer.ApplyApiState(m_Context.Blackboard.IsAPIWorking);
        }

        void MarkDirty()
        {
            m_StreamDirty = true;
            m_Surface.RequestPass();
        }

        void EnsureFlattened()
        {
            // Mid-batch the list is incomplete by construction; EndUpdate flattens it once.
            if (!m_StreamDirty || m_InUpdate)
                return;

            Flatten();
        }

        void Flatten()
        {
            m_FlattensSincePopulate++;
            var startTicks = AssistantPerf.Now;

            m_Items = ConversationFlattener.Flatten(m_Data, m_Surface.ViewState, out var chrome);

            // Heights first: the model rebases its anchor onto the new stream and reads them, and the
            // realizer positions from them.
            var heightTicks = AssistantPerf.Now;
            m_Surface.Heights.SetItems(m_Items);

            if (AssistantPerf.Enabled)
                AssistantPerf.MarkElapsed("chat.height_setitems", "items=" + m_Items.Count + ";cached=" + CountMeasured(), heightTicks);

            var rebaseTicks = AssistantPerf.Now;
            m_Surface.Model.Rebase(m_Items);

            if (AssistantPerf.Enabled)
                AssistantPerf.MarkElapsed("chat.rebase", "anchor=" + m_Surface.Model.AnchorItem, rebaseTicks);

            m_Realizer.SetItems(m_Data, m_Items, chrome);
            m_BlockIndexDirty = true;

            // Last, and only on the way out: a pipeline that threw halfway has left the stream describing
            // neither the old conversation nor the new one, and a flag cleared up front would never ask
            // for the flatten again.
            m_StreamDirty = false;

            if (AssistantPerf.Enabled)
            {
                AssistantPerf.MarkElapsed("chat.flatten",
                    "messages=" + m_Data.Count + ";items=" + m_Items.Count + ";flattens=" + m_FlattensSincePopulate, startTicks);
            }
        }

        /// <summary>How many of the stream's items arrived with a height already, which is what tells a
        /// re-flatten that kept the height cache warm from one that has to measure the viewport again.</summary>
        int CountMeasured()
        {
            var measured = 0;

            for (var index = 0; index < m_Items.Count; index++)
            {
                if (m_Surface.Heights.IsMeasured(index))
                    measured++;
            }

            return measured;
        }
    }
}
