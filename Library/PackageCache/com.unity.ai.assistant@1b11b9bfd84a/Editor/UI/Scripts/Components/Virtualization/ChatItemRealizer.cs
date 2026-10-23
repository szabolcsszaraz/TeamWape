using System;
using System.Collections.Generic;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.Utils.Perf;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// Turns a window of display items into positioned slots: rent a slot per item, bind its data, write
    /// each slot's top from measured heights walked outward from the anchor, measure what was laid out,
    /// seal what has settled, and keep alive the items that must not leave the tree. Model heights reach
    /// the scrollbar and the window calculation; only measured heights reach a slot's top, so a wrong
    /// estimate can delay an item by one pass but can never show it in the wrong place.
    /// </summary>
    class ChatItemRealizer : IDisposable
    {
        // Roughly one viewport of lookahead: enough that a wheel tick lands on realized items, small
        // enough that a p90 block just outside the viewport is not built for nothing.
        const float k_BufferPx = 600f;

        const int k_MaxRealizePerPass = 8;

        readonly ChatScrollSurface m_Surface;
        readonly AssistantUIContext m_Context;
        readonly ChatScrollModel m_Model;
        readonly ItemHeightModel m_Heights;
        readonly ChatItemBinder m_Binder;
        readonly ChatItemPins m_Pins;
        readonly ChatSlotMeasurer m_Measurer;
        readonly ChatSlotPool m_Pool;
        readonly int m_MaxRealizePerPass;

        readonly Dictionary<int, ChatSlot> m_Realized = new();
        readonly Dictionary<int, ChatSlot> m_Remapped = new();
        readonly IdentityIndex m_Identities = new();
        readonly List<int> m_ChromeIndices = new();
        readonly List<int> m_Order = new();
        readonly List<int> m_Deferred = new();
        readonly List<int> m_Released = new();
        readonly List<SlotPlacement> m_Placements = new();

        IReadOnlyList<MessageModel> m_Messages = Array.Empty<MessageModel>();
        IReadOnlyList<DisplayItem> m_Items = Array.Empty<DisplayItem>();

        bool m_InPass;

        public ChatItemRealizer(ChatScrollSurface surface, AssistantUIContext context, int maxRealizePerPass = k_MaxRealizePerPass)
        {
            m_Surface = surface ?? throw new ArgumentNullException(nameof(surface));
            m_Context = context;
            m_Model = surface.Model;
            m_Heights = surface.Heights;
            m_MaxRealizePerPass = Mathf.Max(1, maxRealizePerPass);
            m_Binder = new ChatItemBinder(context);
            m_Pins = new ChatItemPins(context);
            m_Measurer = new ChatSlotMeasurer(m_Heights, m_Model, m_Realized, m_Surface.RequestPass, () => m_Surface.PumpTick);
            m_Pool = new ChatSlotPool(m_Binder.Create);

            m_Surface.PassRequested += RunPass;
        }

        public ChatScrollModel Model => m_Model;

        public ItemHeightModel Heights => m_Heights;

        public float ViewportHeight => m_Surface.ViewportHeight;

        public int RealizedCount => m_Realized.Count;

        /// <summary>
        /// Rebinds the realizer to a freshly flattened stream. A realized slot follows its item's
        /// identity across the re-flatten, so a streamed chunk rebinds only what actually changed.
        /// </summary>
        public void SetItems(IReadOnlyList<MessageModel> messages, IReadOnlyList<DisplayItem> items, IReadOnlyList<ChatItemChrome> chrome)
        {
            Remap(items);

            m_Messages = messages ?? Array.Empty<MessageModel>();
            m_Items = items ?? Array.Empty<DisplayItem>();
            m_Binder.SetSource(m_Messages, m_Items, chrome ?? Array.Empty<ChatItemChrome>());

            m_Pins.Rebuild(m_Messages, m_Items, m_Context.Blackboard.IsAPIWorking);
            m_Deferred.Clear();
            m_Surface.RequestPass();
        }

        public bool TryGetSlot(int index, out ChatSlot slot) => m_Realized.TryGetValue(index, out slot);

        /// <summary>Re-applies the bind state the API's working flag drives on the realized items that
        /// show it. No content key covers it, so no rebind would otherwise notice it changed.</summary>
        public void ApplyApiState(bool isWorking)
        {
            // The binder decides what an API state change touches, so every pin goes: a spinner or a
            // cancelled call changes an element's height, and a pinned slot would clip the difference.
            foreach (var pair in m_Realized)
            {
                pair.Value.Unseal();
                m_Measurer.ForgetPin(pair.Value);
            }

            // A call the turn never came back to is only pinned while the turn can still complete it, so
            // the flag going down is what releases the slots of a conversation that was interrupted.
            m_Pins.Rebuild(m_Messages, m_Items, isWorking);

            m_Binder.ApplyApiState(m_Realized, isWorking);
            m_Surface.RequestPass();
        }

        /// <summary>Binds a message's realized items again, for content its item key does not carry.</summary>
        public void RebindMessage(int messageIndex)
        {
            foreach (var pair in m_Realized)
            {
                if (m_Items[pair.Key].MessageIndex == messageIndex)
                    Rebind(pair.Value, pair.Key);
            }

            m_Surface.RequestPass();
        }

        public void RunPass()
        {
            if (m_InPass || m_Items.Count == 0)
                return;

            // The legacy per-frame visibility pass reported under this marker, which is what makes the
            // before and after profiler captures comparable on the headline number.
            using var perfScope = AssistantPerf.Measure(PerfProbe.NotifyVisibleElements);
            var startTicks = AssistantPerf.Now;
            m_InPass = true;

            try
            {
                var viewportHeight = m_Surface.ViewportHeight;
                AnchorTheWindow(viewportHeight, out var anchor, out var range, out var viewport);

                ReleaseOutside(range);
                RealizeWithinBudget(range, viewport, anchor.Item);
                RebindKeptSlots();

                // Read before anything writes a height: once the tail has been measured shorter, a view
                // that fell short of the bottom and one that sits exactly on it are the same reading.
                var wasAtBottom = m_Model.IsAtBottom(viewportHeight);

                // Measured before positioned, or a height recorded from the last layout would be spent
                // on this frame's positions before it is corrected — one frame of a visible gap.
                if (m_Measurer.MeasureAndSeal())
                {
                    m_Surface.RequestPass();

                    // The heights the bottom is defined by have just moved, so the anchor that was walked
                    // back from the tail no longer reaches it. Re-anchoring here is what keeps a streamed
                    // chunk from leaving the view one measurement short of the tail.
                    AnchorTheWindow(viewportHeight, out anchor, out range, out viewport, snapIfTheTailDetached: wasAtBottom);
                }

                Position(range, anchor);
                RevealWhenRealized(viewport);

                if (AssistantPerf.Enabled)
                {
                    AssistantPerf.MarkElapsed("chat.realize_pass",
                        $"realized={m_Realized.Count};range={range.First}-{range.LastExclusive};deferred={m_Deferred.Count}", startTicks);
                }

                AssistantPerf.SetGauge(PerfGauge.RetainedChatElements, m_Realized.Count);
            }
            finally
            {
                m_InPass = false;
            }
        }

        /// <summary>
        /// Puts the anchor where the mode says it belongs and derives the window from it. Called again
        /// whenever a pass changes the heights it was derived from, so everything the pass writes comes
        /// from one snapshot of the model. <paramref name="snapIfTheTailDetached"/> carries whether the
        /// view was at the bottom before those heights moved: a view that was belongs at the bottom after
        /// them, while one that was not keeps the anchor the user put there.
        /// </summary>
        void AnchorTheWindow(float viewportHeight, out ItemAnchor anchor, out ItemRange range, out ItemRange viewport,
            bool snapIfTheTailDetached = false)
        {
            if (m_Model.StickToBottom
                || (snapIfTheTailDetached && m_Model.TailEndsAboveTheViewport(viewportHeight)))
                m_Model.MoveToEnd(viewportHeight);

            anchor = new ItemAnchor(m_Model.AnchorItem, m_Model.AnchorOffset);
            range = ItemRangeCalculator.Compute(anchor, m_Heights, viewportHeight, k_BufferPx);
            viewport = ItemRangeCalculator.Compute(anchor, m_Heights, viewportHeight, 0f);
            ItemRangeCalculator.ExpandToChromeChain(range, m_Items, m_ChromeIndices);
        }

        /// <summary>Realizes an item and its chrome chain ignoring the frame budget.</summary>
        public void Flush(int index)
        {
            if (index < 0 || index >= m_Items.Count)
                return;

            for (var parent = m_Items[index].Parent; parent != DisplayItem.NoIndex; parent = m_Items[parent].Parent)
                Realize(parent);

            Realize(index);
            m_Deferred.Remove(index);
            m_Surface.RequestPass();
        }

        /// <summary>Returns every slot to the pool and forgets the stream. The slots stay in the content
        /// layer, hidden, ready for the next conversation.</summary>
        public void Clear()
        {
            foreach (var pair in m_Realized)
                Release(pair.Value);

            m_Realized.Clear();
            m_Pins.Clear();
            m_Deferred.Clear();
            m_ChromeIndices.Clear();
            m_Messages = Array.Empty<MessageModel>();
            m_Items = Array.Empty<DisplayItem>();
            m_Binder.SetSource(m_Messages, m_Items, Array.Empty<ChatItemChrome>());
        }

        public void Dispose()
        {
            Clear();
            m_Surface.PassRequested -= RunPass;

            foreach (var slot in m_Pool.All)
            {
                slot.HeightChanged -= OnSlotHeightChanged;
                m_Measurer.Unwatch(slot);
            }
        }

        void ReleaseOutside(ItemRange range)
        {
            m_Pins.TrackFocus(m_Surface.panel);
            m_Released.Clear();

            foreach (var pair in m_Realized)
            {
                var keep = range.Contains(pair.Key)
                    || m_ChromeIndices.Contains(pair.Key)
                    || m_Pins.Contains(pair.Key);

                if (!keep)
                    m_Released.Add(pair.Key);
            }

            foreach (var index in m_Released)
            {
                var slot = m_Realized[index];
                m_Realized.Remove(index);
                Release(slot);
            }
        }

        void RealizeWithinBudget(ItemRange range, ItemRange viewport, int anchorItem)
        {
            m_Deferred.Clear();
            m_Order.Clear();

            foreach (var index in m_ChromeIndices)
                Realize(index);

            var anchorIndex = Mathf.Clamp(anchorItem, range.First, Mathf.Max(range.First, range.LastExclusive - 1));
            Realize(anchorIndex);

            ItemRangeCalculator.AppendOutward(viewport, anchorIndex, default, m_Order);
            ItemRangeCalculator.AppendOutward(range, anchorIndex, viewport, m_Order);

            var budget = m_MaxRealizePerPass;

            foreach (var index in m_Order)
            {
                if (m_Realized.ContainsKey(index))
                    continue;

                if (budget > 0)
                {
                    Realize(index);
                    budget--;
                }
                else
                {
                    m_Deferred.Add(index);
                }
            }

            if (m_Deferred.Count > 0)
                m_Surface.RequestPass();
        }

        void Realize(int index)
        {
            if (index < 0 || index >= m_Items.Count || m_Realized.ContainsKey(index))
                return;

            var startTicks = AssistantPerf.Now;
            var item = m_Items[index];
            var slot = m_Pool.Rent(item);

            if (slot.parent == null)
                Adopt(slot);

            m_Realized.Add(index, slot);
            Bind(slot, index);

            // Per item and chatty by design: this is what attributes a slow pass to one giant answer block.
            if (AssistantPerf.Enabled)
                AssistantPerf.MarkElapsed("chat.realize_item", "kind=" + item.Kind + ";type=" + (item.ElementType?.Name ?? "none"), startTicks);
        }

        // The one hierarchy write in the realizer, and it runs once per slot ever: from here on the slot
        // is a permanent member of the content layer, which is what keeps a scroll frame free of churn.
        void Adopt(ChatSlot slot)
        {
            m_Measurer.Watch(slot);
            slot.HeightChanged += OnSlotHeightChanged;
            m_Surface.Content.Add(slot);
        }

        // Nothing reflows for us, so a foldout's size change arrives here instead: the pin comes off, the
        // measurer is told the slot's layout no longer describes it, and the next pass re-places it.
        void OnSlotHeightChanged(ChatSlot slot, bool? expanded)
        {
            var index = slot.BoundIndex;

            if (index < 0 || index >= m_Items.Count || !m_Realized.TryGetValue(index, out var bound) || bound != slot)
                return;

            // Re-anchor to the toggled item at the offset it is showing at, so the control the user clicked
            // does not move under the cursor. An item below the anchor keeps its top without any of this.
            if (index <= m_Model.AnchorItem && slot.Top > ChatSlot.ParkedTop)
                m_Model.Set(index, Mathf.Max(0f, -slot.Top), m_Surface.ViewportHeight);

            slot.Unseal();

            // The pin the slot was measured at goes with it, the way a rebind's does: a report raised
            // between two passes is one the layout has not run since, so the pass that follows it reads
            // the height the pin was holding and would settle it straight back onto that.
            m_Measurer.ForgetPin(slot);
            m_Measurer.MarkAwaitingRelayout(slot);
            m_Surface.RequestPass();

            if (expanded.HasValue)
                m_Surface.SetExpanded(m_Items[index].Identity, expanded.Value);
        }

        void Release(ChatSlot slot)
        {
            m_Measurer.Forget(slot);
            m_Pool.Return(slot);
        }

        void Bind(ChatSlot slot, int index)
        {
            var item = m_Items[index];

            if (slot.BoundIndex == index && slot.BoundContent == item.Content)
                return;

            var itemChanged = slot.BoundIndex != index;

            slot.Unseal();
            slot.BoundIndex = index;
            slot.BoundContent = item.Content;

            // A content update in place is deliberately not gated: the layout it leaves behind is the same
            // item's and one chunk stale at worst, while gating it would let a stream that rebinds every
            // frame re-stamp the wait forever and never measure the item it is growing.
            if (itemChanged)
                m_Measurer.MarkShowingForeignItem(slot);

            BindContent(slot, index);
        }

        /// <summary>
        /// Runs the binder over a slot's content and contains what it does not survive. The slot is
        /// recorded as bound before its element is given anything, and that element is foreign code, so a
        /// bind that fails would leave the slot showing the item it held before with nothing ever coming
        /// back to it. Which content it failed on is latched on the slot, so <see cref="Rebind"/> stops
        /// offering it and the item is tried again the moment it carries something else.
        /// </summary>
        void BindContent(ChatSlot slot, int index)
        {
            var content = m_Items[index].Content;

            try
            {
                if (m_Binder.Bind(slot.Content, index))
                {
                    slot.FailedContent = default;
                    return;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            slot.BoundContent = default;
            slot.FailedContent = content;
            m_Measurer.MarkShowingForeignItem(slot);
        }

        /// <summary>
        /// Pushes a re-flatten's content changes into the slots that survived it. A slot whose item keeps
        /// its identity keeps the slot, and realizing an item — the one path that binds it — skips what is
        /// realized already, so without this a streamed chunk reaches the stream and never the element.
        /// Inside the pass, not at the re-flatten: the anchor, the heights and the tops all have to come
        /// out of one snapshot, and a rebind moves the heights.
        /// </summary>
        void RebindKeptSlots()
        {
            foreach (var pair in m_Realized)
            {
                if (m_Items[pair.Key].Content != pair.Value.BoundContent)
                    Rebind(pair.Value, pair.Key);
            }
        }

        /// <summary>
        /// Pushes an already-realized slot's data in again, releasing the pin: a height pinned before the
        /// content changed cannot follow what replaced it, and a pinned slot raises no geometry event of
        /// its own to say so. Content the slot's element already refused is not offered again: it would
        /// fail the same way, and the slot would spend every pass of the item's life failing it.
        /// </summary>
        void Rebind(ChatSlot slot, int index)
        {
            if (slot.FailedToBind(m_Items[index].Content))
                return;

            slot.Unseal();
            m_Measurer.ForgetPin(slot);
            slot.BoundContent = m_Items[index].Content;
            BindContent(slot, index);
        }

        void Position(ItemRange range, ItemAnchor anchor)
        {
            m_Placements.Clear();
            SlotLayout.Place(range, anchor, m_Heights, m_Placements);

            foreach (var placement in m_Placements)
            {
                if (m_Realized.TryGetValue(placement.Index, out var slot))
                {
                    slot.SetTop(placement.Top);
                    slot.SetVisible(placement.Visible
                        && !m_Measurer.IsShowingForeignLayout(slot)
                        && !slot.FailedToBind(m_Items[placement.Index].Content));
                }
            }

            foreach (var pair in m_Realized)
            {
                if (range.Contains(pair.Key))
                    continue;

                // Chrome above the window and pinned items keep their slots, their subscriptions and
                // their focus. The viewport clips the parked position, so hiding them on top of that
                // would only cost us the focus inside a pinned item the moment the window left it.
                pair.Value.Park();
            }
        }

        void RevealWhenRealized(ItemRange viewport)
        {
            // The surface checks the heights; whether the items exist and have laid out with what they
            // hold now is ours to check, or a deferred item would be revealed as a hole.
            for (var index = viewport.First; index < viewport.LastExclusive; index++)
            {
                if (!m_Realized.TryGetValue(index, out var slot) || m_Measurer.IsShowingForeignLayout(slot))
                    return;
            }

            m_Surface.TryReveal(viewport);
        }

        void Remap(IReadOnlyList<DisplayItem> items)
        {
            if (m_Realized.Count == 0)
            {
                m_Pins.FocusedIndex = DisplayItem.NoIndex;
                return;
            }

            m_Identities.Rebuild(items);
            m_Pins.FocusedIndex = m_Identities.MovedIndexOf(m_Items, m_Pins.FocusedIndex);
            m_Remapped.Clear();

            foreach (var pair in m_Realized)
            {
                var slot = pair.Value;
                var moved = m_Identities.MovedIndexOf(m_Items, pair.Key);

                // First claim keeps the index: a second slot landing there is dropped and would never be returned.
                if (moved != DisplayItem.NoIndex && SlotKey.For(items[moved]) == slot.Key && !m_Remapped.ContainsKey(moved))
                {
                    slot.BoundIndex = moved;
                    m_Remapped[moved] = slot;
                }
                else
                {
                    Release(slot);
                }
            }

            m_Realized.Clear();
            foreach (var pair in m_Remapped)
                m_Realized.Add(pair.Key, pair.Value);
        }
    }
}
