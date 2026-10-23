using System.Collections.Generic;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// The authoritative scroll position of the virtualized conversation: the item anchored to the top
    /// of the viewport plus the pixels of it that sit above the viewport top. Movement is a local walk
    /// over the heights between the old and the new anchor, so a wrong estimate anywhere else in the
    /// stream can move the scrollbar readout but never the viewport.
    /// </summary>
    class ChatScrollModel
    {
        // Carried over from the legacy chat list so the surface keeps its thresholds exactly.
        const float k_ScrollEndThreshold = 5f;
        const float k_ScrollEndDisplayThreshold = 30f;
        const float k_ScrollEndDisplayThresholdDuringRun = 100f;
        const float k_MinScrollableContentRatio = 0.25f;

        const long k_NoIdentity = 0L;

        readonly ItemHeightModel m_Heights;

        IReadOnlyList<DisplayItem> m_Items;

        int m_AnchorItem;
        float m_AnchorOffset;
        float m_AnchorHeight = float.NaN;
        bool m_AnchorHeightMeasured;
        ContentKey m_AnchorIdentity;

        public ChatScrollModel(ItemHeightModel heights, IReadOnlyList<DisplayItem> items = null)
        {
            m_Heights = heights;
            m_Items = items;
        }

        public ItemHeightModel Heights => m_Heights;

        public int AnchorItem => m_AnchorItem;

        public float AnchorOffset => m_AnchorOffset;

        public bool StickToBottom { get; set; }

        /// <summary>The viewport every correction here is measured against, so that a rebase or a capture
        /// has a height to read. Only <see cref="SetViewportHeight"/> changes it from outside: a height
        /// that arrives without correcting the anchor is what leaves a strip below the last message.</summary>
        public float ViewportHeight { get; private set; }

        public float EstimatedOffset => m_Heights.OffsetOf(m_AnchorItem) + m_AnchorOffset;

        public float EstimatedTotal => m_Heights.TotalHeight;

        public void Set(int item, float offset, float viewportHeight)
        {
            ViewportHeight = viewportHeight;
            Correct(item, offset);
        }

        /// <summary>
        /// Takes a viewport that changed size. A view that was at the bottom stays there, because the
        /// newest message is what a chat is read from; anything else keeps the item at the viewport top,
        /// corrected — a viewport that grew past the tail below the anchor would otherwise end the
        /// conversation above the viewport bottom and leave a strip of nothing under it.
        /// </summary>
        public void SetViewportHeight(float viewportHeight)
        {
            if (Mathf.Approximately(ViewportHeight, viewportHeight))
                return;

            // Read against the height the view was laid out at, which this is about to replace.
            if (StickToBottom || IsAtBottom(ViewportHeight))
            {
                MoveToEnd(viewportHeight);
                return;
            }

            Set(m_AnchorItem, m_AnchorOffset, viewportHeight);
        }

        /// <summary>
        /// Takes the anchor item's own height changing. A placed slot's top is minus the anchor offset plus
        /// the heights between the anchor and that slot, so a height that changes above the anchor is
        /// scrollbar error only, one below it moves that slot alone, and one on the anchor itself moves
        /// everything on screen. Two rules: the anchor's bottom edge is held still when the offset was
        /// taken against a guess of the height being replaced, so an estimate spends itself off-screen
        /// above the viewport; and an offset the new height can no longer contain is never left there,
        /// whatever the height it was taken against, because the next walk would spend it travelling back
        /// down the stream.
        /// </summary>
        public void KeepAnchorBottom(int item, float previousHeight, float height)
        {
            if (item != m_AnchorItem)
                return;

            // Per-kind estimates are constants, so the value alone cannot tell a height the offset was
            // taken against from one that merely reads the same: a width change drops a measurement back
            // to the estimate it started from, and a bottom edge computed from that would be fiction.
            var offsetWasTakenAgainstThis = Mathf.Approximately(previousHeight, m_AnchorHeight);
            var offsetWasTakenAgainstAGuess = offsetWasTakenAgainstThis && !m_AnchorHeightMeasured;

            m_AnchorHeight = height;
            m_AnchorHeightMeasured = m_Heights.IsMeasured(item);

            // At offset zero the anchor's top is the viewport top: there is no part of it above the
            // viewport to spend a correction on, and keeping the top is what the reader expects there.
            if (m_AnchorOffset <= 0f || Mathf.Approximately(previousHeight, height))
                return;

            var strandedOutsideTheItem = m_AnchorOffset >= height;

            if (!offsetWasTakenAgainstAGuess && !strandedOutsideTheItem)
                return;

            if (m_Heights.Count == 0 || m_Heights.TotalHeight <= ViewportHeight)
            {
                Assign(0, 0f);
                return;
            }

            if (!offsetWasTakenAgainstThis)
            {
                // Nothing to preserve and nothing to spend: make the state legal where it stands rather
                // than walk an offset that describes a height no longer in the model.
                Assign(item, Mathf.Min(m_AnchorOffset, height));
                return;
            }

            var offset = height - (previousHeight - m_AnchorOffset);

            while (offset < 0f && item > 0)
            {
                item--;
                offset += m_Heights.Height(item);
            }

            while (offset > m_Heights.Height(item) && item < m_Heights.Count - 1)
            {
                offset -= m_Heights.Height(item);
                item++;
            }

            Assign(item, Mathf.Clamp(offset, 0f, m_Heights.Height(item)));
        }

        public void MoveBy(float delta, float viewportHeight)
        {
            var item = m_AnchorItem;
            var offset = m_AnchorOffset + delta;

            while (offset > 0f && item < m_Heights.Count - 1)
            {
                var height = m_Heights.Height(item);
                if (offset < height)
                    break;

                offset -= height;
                item++;
            }

            while (offset < 0f && item > 0)
            {
                item--;
                offset += m_Heights.Height(item);
            }

            Set(item, offset, viewportHeight);
        }

        public void MoveToEnd(float viewportHeight)
        {
            ViewportHeight = viewportHeight;

            var count = m_Heights.Count;
            if (count == 0 || m_Heights.TotalHeight <= viewportHeight)
            {
                Assign(0, 0f);
                return;
            }

            var item = count - 1;
            var remaining = viewportHeight;

            while (true)
            {
                var height = m_Heights.Height(item);
                if (height >= remaining || item == 0)
                {
                    Assign(item, Mathf.Max(0f, height - remaining));
                    return;
                }

                remaining -= height;
                item--;
            }
        }

        public void MoveToStart()
        {
            // Left on, the stick mode would put the anchor straight back at the end on the next pass.
            StickToBottom = false;
            Assign(0, 0f);
        }

        public float DistanceToEnd(float viewportHeight)
            => Mathf.Max(0f, TailBelow(m_AnchorItem, m_AnchorOffset) - viewportHeight);

        public bool IsAtBottom(float viewportHeight) => DistanceToEnd(viewportHeight) <= k_ScrollEndThreshold;

        /// <summary>
        /// Whether the conversation now ends above the viewport bottom, leaving a strip of nothing under
        /// the last message. <see cref="DistanceToEnd"/> cannot answer this — it floors at zero, so a tail
        /// that fills the viewport exactly and one that falls short of it both read as being at the end.
        /// </summary>
        public bool TailEndsAboveTheViewport(float viewportHeight)
            => TailBelow(m_AnchorItem, m_AnchorOffset) < viewportHeight;

        public bool CanScrollDown(float viewportHeight, bool isApiWorking)
        {
            var threshold = isApiWorking ? k_ScrollEndDisplayThresholdDuringRun : k_ScrollEndDisplayThreshold;
            var scrollableHeight = Mathf.Max(0f, EstimatedTotal - viewportHeight);
            return scrollableHeight > viewportHeight * k_MinScrollableContentRatio && DistanceToEnd(viewportHeight) > threshold;
        }

        /// <summary>
        /// Moves the anchor onto the index the anchored identity now occupies in a freshly flattened
        /// stream, keeping its offset. An identity that no longer exists falls back to the end.
        /// </summary>
        public void Rebase(IReadOnlyList<DisplayItem> items)
        {
            m_Items = items;

            if (m_AnchorIdentity.Value == k_NoIdentity)
            {
                Correct(m_AnchorItem, m_AnchorOffset);
                return;
            }

            if (TryFindIdentity(m_AnchorIdentity.Value, out var index))
            {
                Correct(index, m_AnchorOffset);
                return;
            }

            MoveToEnd(ViewportHeight);
        }

        public ChatScrollPosition Capture()
            => new(m_AnchorIdentity.Value, m_AnchorOffset, IsAtBottom(ViewportHeight));

        public void Restore(ChatScrollPosition position, IReadOnlyList<DisplayItem> items)
        {
            m_Items = items;

            if (!position.AtEnd && TryFindIdentity(position.Identity, out var index))
            {
                Correct(index, position.Offset);
                return;
            }

            MoveToEnd(ViewportHeight);
        }

        /// <summary>
        /// Anchors on an item after the corrections every position has to pass: an index and an offset
        /// inside the stream, and the end snap that keeps the tail from stopping short of the viewport
        /// bottom. Everything that names a position goes through here, including a rebase — a re-flatten
        /// that prunes the tail leaves the surviving anchor too far down to fill the viewport.
        /// </summary>
        void Correct(int item, float offset)
        {
            var count = m_Heights.Count;
            if (count == 0 || m_Heights.TotalHeight <= ViewportHeight)
            {
                Assign(0, 0f);
                return;
            }

            item = Mathf.Clamp(item, 0, count - 1);
            offset = Mathf.Clamp(offset, 0f, m_Heights.Height(item));

            if (TailBelow(item, offset) < ViewportHeight)
            {
                MoveToEnd(ViewportHeight);
                return;
            }

            Assign(item, offset);
        }

        // A range sum over the tail rather than Total - OffsetOf(anchor): the tail is realized whenever
        // this can bite, so it stays exact where the total is only an estimate.
        float TailBelow(int item, float offset) => m_Heights.RangeHeight(item, m_Heights.Count) - offset;

        void Assign(int item, float offset)
        {
            m_AnchorItem = item;
            m_AnchorOffset = offset;
            m_AnchorIdentity = IdentityAt(item);

            // The height the offset was taken against, and whether it was a guess, so a later correction
            // can tell whether it still means anything.
            var known = item >= 0 && item < m_Heights.Count;
            m_AnchorHeight = known ? m_Heights.Height(item) : float.NaN;
            m_AnchorHeightMeasured = known && m_Heights.IsMeasured(item);
        }

        ContentKey IdentityAt(int index)
            => m_Items != null && index >= 0 && index < m_Items.Count ? m_Items[index].Identity : default;

        bool TryFindIdentity(long identity, out int index)
        {
            if (m_Items != null && identity != k_NoIdentity)
            {
                for (var i = 0; i < m_Items.Count; i++)
                {
                    if (m_Items[i].Identity.Value == identity)
                    {
                        index = i;
                        return true;
                    }
                }
            }

            index = 0;
            return false;
        }
    }
}
