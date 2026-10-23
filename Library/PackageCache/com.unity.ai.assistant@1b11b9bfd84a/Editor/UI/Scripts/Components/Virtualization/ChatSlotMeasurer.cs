using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Utils.Perf;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// The only writer of the height model while a conversation is realized, and the only place a slot is
    /// pinned or released. A slot's layout describes the epoch before the pass, so a slot bound in this
    /// epoch is skipped: measuring it would record the item it used to hold. Geometry events do not
    /// measure either — an event cannot tell whether the layout it reports describes the current binding
    /// — but a slot laying out at a new height is proof that it laid out with what it holds now, so that
    /// releases the wait.
    /// </summary>
    class ChatSlotMeasurer
    {
        /// <summary>A slot whose layout was computed before its current content.</summary>
        readonly struct PendingLayout
        {
            public PendingLayout(long epoch, bool itemChanged)
            {
                Epoch = epoch;
                ItemChanged = itemChanged;
            }

            /// <summary>The layout epoch the slot was bound in. The panel lays out between epochs, so
            /// only a later one can have laid the slot out with what it holds now.</summary>
            public long Epoch { get; }

            /// <summary>True when the layout describes a different item, which must never be shown.
            /// False for a content update in place, whose last height is one chunk stale at worst.</summary>
            public bool ItemChanged { get; }
        }

        const float k_SealSlackPx = 1f;

        readonly ItemHeightModel m_Heights;
        readonly ChatScrollModel m_Anchor;
        readonly IReadOnlyDictionary<int, ChatSlot> m_Realized;
        readonly Action m_RequestPass;
        readonly Func<long> m_LayoutEpoch;

        readonly Dictionary<ChatSlot, PendingLayout> m_AwaitingLayout = new();
        readonly Dictionary<ChatSlot, float> m_SealedExtent = new();
        readonly Dictionary<ChatSlot, ContentKey> m_LastObserved = new();

        public ChatSlotMeasurer(ItemHeightModel heights, ChatScrollModel anchor, IReadOnlyDictionary<int, ChatSlot> realized,
            Action requestPass, Func<long> layoutEpoch)
        {
            m_Heights = heights;
            m_Anchor = anchor;
            m_Realized = realized;
            m_RequestPass = requestPass;
            m_LayoutEpoch = layoutEpoch;
        }

        /// <summary>Subscribes a slot for its whole pooled life; slots are never re-parented, so this
        /// runs once per slot ever.</summary>
        public void Watch(ChatSlot slot)
        {
            slot.RegisterCallback<GeometryChangedEvent>(OnHeightMayHaveChanged);
            slot.Content?.RegisterCallback<GeometryChangedEvent>(OnHeightMayHaveChanged);
        }

        public void Unwatch(ChatSlot slot)
        {
            slot.UnregisterCallback<GeometryChangedEvent>(OnHeightMayHaveChanged);
            slot.Content?.UnregisterCallback<GeometryChangedEvent>(OnHeightMayHaveChanged);
            Forget(slot);
        }

        /// <summary>
        /// Marks a slot that is still laid out as the item it used to hold. Until a layout has run with
        /// what it holds now it is neither measured nor shown: no cached height makes another item's
        /// layout safe to record, and none makes it safe to show.
        /// </summary>
        public void MarkShowingForeignItem(ChatSlot slot)
            => m_AwaitingLayout[slot] = new PendingLayout(m_LayoutEpoch(), itemChanged: true);

        /// <summary>
        /// Marks a slot whose own height is on its way to changing — a pin released, a foldout toggled.
        /// It keeps showing what it already shows, but measuring the layout it is leaving would record
        /// the height it is leaving behind, and pin it straight back to it.
        /// </summary>
        public void MarkAwaitingRelayout(ChatSlot slot)
            => m_AwaitingLayout[slot] = new PendingLayout(m_LayoutEpoch(), itemChanged: false);

        /// <summary>Whether the slot is still laid out as the item it used to hold, which no estimate and
        /// no cached height makes safe to show.</summary>
        public bool IsShowingForeignLayout(ChatSlot slot)
            => m_AwaitingLayout.TryGetValue(slot, out var pending) && pending.ItemChanged;

        public void Forget(ChatSlot slot)
        {
            m_AwaitingLayout.Remove(slot);
            ForgetPin(slot);
        }

        /// <summary>
        /// Drops what the measurer knows about a slot's pinned height, without touching what it is waiting
        /// for. A caller that has just replaced a slot's content owns the first half and not the second:
        /// the old occupancy and the old settled height are gone, but a slot still showing another item's
        /// layout is still showing it.
        /// </summary>
        public void ForgetPin(ChatSlot slot)
        {
            m_SealedExtent.Remove(slot);
            m_LastObserved.Remove(slot);
        }

        /// <summary>
        /// Records what every realized slot laid out at, pins the ones that have settled, and releases a
        /// sealed one whose content no longer fits the pinned height. Returns whether a height moved,
        /// which asks for another pass.
        /// </summary>
        public bool MeasureAndSeal()
        {
            var startTicks = AssistantPerf.Now;
            var moved = false;
            var measured = 0;
            var changed = 0;

            foreach (var pair in m_Realized)
            {
                var slot = pair.Value;
                var height = slot.layout.height;

                // Checked before the gate is opened: a slot that has never been laid out has nothing to
                // record, and opening the gate would let the item be shown on its estimate. The pass has
                // to come back for it, and nothing else will.
                if (float.IsNaN(height))
                {
                    moved = true;
                    continue;
                }

                if (m_AwaitingLayout.TryGetValue(slot, out var pending))
                {
                    if (pending.Epoch >= m_LayoutEpoch())
                    {
                        // Still waiting on a layout, so the surface has to keep passing: nothing else
                        // will come back to this slot, and an idle Editor raises no events of its own.
                        moved = true;
                        continue;
                    }

                    m_AwaitingLayout.Remove(slot);
                }

                var index = pair.Key;
                var recorded = IsRecorded(index, height);
                measured++;

                // A height counts as settled only once it has been observed twice for the same content:
                // right after a rebind the layout still reports what the slot held before, the model
                // agrees with it, and pinning that would clip the content on its way in.
                var settled = m_LastObserved.TryGetValue(slot, out var observed) && observed == slot.BoundContent;
                m_LastObserved[slot] = slot.BoundContent;

                if (!recorded)
                {
                    Record(index, height);
                    changed++;
                    moved = true;
                }

                if (slot.IsSealed)
                    moved |= ReleaseIfContentMoved(slot);
                else if (recorded && settled)
                    moved |= SealSettled(slot, index, height);
            }

            if (AssistantPerf.Enabled)
                AssistantPerf.MarkElapsed("chat.measure", "measured=" + measured + ";changed=" + changed, startTicks);

            return moved;
        }

        bool IsRecorded(int index, float height)
            => m_Heights.IsMeasured(index) && Mathf.Approximately(height, m_Heights.Height(index));

        /// <summary>The one place a height reaches the model, so the one place the anchor can be told that
        /// the item it sits in is not the size it was.</summary>
        void Record(int index, float height)
        {
            var previous = m_Heights.Height(index);
            m_Heights.Measure(index, height);
            m_Anchor.KeepAnchorBottom(index, previous, height);
        }

        /// <summary>Pins a height that held for a whole pass, and records what the content occupied at
        /// that height so growth underneath the pin can be spotted.</summary>
        bool SealSettled(ChatSlot slot, int index, float height)
        {
            slot.Seal();

            if (!slot.IsSealed)
                return false;

            m_SealedExtent[slot] = ContentExtent(slot);
            var pinned = Mathf.Ceil(height);

            if (Mathf.Approximately(pinned, height))
                return false;

            // Sealing rounds up so hidden overflow cannot clip a descender, and the model has to agree
            // with the height the slot now paints at.
            Record(index, pinned);
            return true;
        }

        bool ReleaseIfContentMoved(ChatSlot slot)
        {
            if (!m_SealedExtent.TryGetValue(slot, out var atSeal))
                return false;

            var extent = ContentExtent(slot);

            // The pin rounds the height up by less than a pixel, and the content settles into that; only
            // a larger move is content that no longer fits what the slot was pinned to.
            if (float.IsNaN(extent) || Mathf.Abs(extent - atSeal) <= k_SealSlackPx)
                return false;

            slot.Unseal();
            m_SealedExtent.Remove(slot);
            MarkAwaitingRelayout(slot);
            return true;
        }

        /// <summary>
        /// What the slot's content occupies. A sealed slot's own height cannot follow its content, so
        /// growth under the pin shows up either in the content's box or, when the content is stretched
        /// to the pinned height, in how far its children reach past it.
        /// </summary>
        static float ContentExtent(ChatSlot slot)
        {
            var content = slot.Content;

            if (content == null)
                return slot.layout.height;

            var extent = content.layout.height;

            for (var child = 0; child < content.childCount; child++)
                extent = Mathf.Max(extent, content[child].layout.yMax);

            return extent;
        }

        void OnHeightMayHaveChanged(GeometryChangedEvent evt)
        {
            if (Mathf.Approximately(evt.oldRect.height, evt.newRect.height))
                return;

            // A slot that just laid out at a new height has demonstrably laid out with what it holds now,
            // which releases the gate without waiting for the frame to turn over.
            if (evt.currentTarget is ChatSlot slot)
                m_AwaitingLayout.Remove(slot);

            m_RequestPass();
        }
    }
}
