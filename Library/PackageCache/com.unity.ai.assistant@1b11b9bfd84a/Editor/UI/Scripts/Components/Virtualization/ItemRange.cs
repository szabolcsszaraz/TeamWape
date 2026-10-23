using System.Collections.Generic;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>Half-open window of display-item indices: <c>[First, LastExclusive)</c>.</summary>
    readonly struct ItemRange
    {
        public ItemRange(int first, int lastExclusive)
        {
            First = first;
            LastExclusive = lastExclusive;
        }

        public int First { get; }

        public int LastExclusive { get; }

        public int Count => Mathf.Max(0, LastExclusive - First);

        public bool Contains(int index) => index >= First && index < LastExclusive;
    }

    /// <summary>
    /// Picks the window of display items to realize. The walk starts at the anchor and stops one item
    /// past each edge of the viewport plus buffer, so the window is a function of the heights around
    /// the anchor only — never of a prefix sum over the whole stream, which is what let a wrong
    /// estimate teleport the viewport in the previous design.
    /// </summary>
    static class ItemRangeCalculator
    {
        public static ItemRange Compute(ItemAnchor anchor, IItemHeights heights, float viewportHeight, float bufferPx)
        {
            var count = heights.Count;
            if (count == 0)
                return new ItemRange(0, 0);

            var item = Mathf.Clamp(anchor.Item, 0, count - 1);
            var target = viewportHeight + bufferPx;

            var last = item;
            var covered = heights.Height(item) - anchor.Offset;

            while (last + 1 < count)
            {
                var next = heights.Height(last + 1);
                if (covered >= target)
                    break;

                covered += next;
                last++;
            }

            var first = item;
            var above = anchor.Offset;

            while (first > 0)
            {
                var previous = heights.Height(first - 1);
                if (above + previous > bufferPx)
                    break;

                above += previous;
                first--;
            }

            return new ItemRange(first, last + 1);
        }

        /// <summary>
        /// Collects the containers that must be realized alongside the window: a container is realized
        /// when any descendant is, and pre-order flattening makes that the ancestor chain of the
        /// window's first item. Ancestors already inside the window are skipped. Indices come out in
        /// stream order, outermost first.
        /// </summary>
        public static void ExpandToChromeChain(ItemRange range, IReadOnlyList<DisplayItem> items, List<int> chromeIndices)
        {
            chromeIndices.Clear();

            if (range.Count == 0 || range.First < 0 || range.First >= items.Count)
                return;

            var parent = items[range.First].Parent;

            while (parent != DisplayItem.NoIndex)
            {
                if (!range.Contains(parent))
                    chromeIndices.Add(parent);

                parent = items[parent].Parent;
            }

            chromeIndices.Reverse();
        }

        /// <summary>
        /// Appends the indices of <paramref name="bounds"/> to <paramref name="order"/> walking outward
        /// from the anchor, so the items the user is looking at come before the buffer. Indices inside
        /// <paramref name="skip"/> are left out, which is how a second call adds only the buffer.
        /// </summary>
        public static void AppendOutward(ItemRange bounds, int anchorIndex, ItemRange skip, List<int> order)
        {
            for (var step = 1; ; step++)
            {
                var below = anchorIndex + step;
                var above = anchorIndex - step;

                if (below >= bounds.LastExclusive && above < bounds.First)
                    return;

                if (bounds.Contains(below) && !skip.Contains(below))
                    order.Add(below);

                if (bounds.Contains(above) && !skip.Contains(above))
                    order.Add(above);
            }
        }
    }
}
