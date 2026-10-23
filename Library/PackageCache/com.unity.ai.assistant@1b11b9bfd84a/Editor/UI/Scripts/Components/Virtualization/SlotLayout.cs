using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>Where one realized item's slot goes, in content coordinates.</summary>
    readonly struct SlotPlacement
    {
        public SlotPlacement(int index, float top, bool visible)
        {
            Index = index;
            Top = top;
            Visible = visible;
        }

        public int Index { get; }

        public float Top { get; }

        /// <summary>False until the item has been measured once, so an estimate can delay an item by a
        /// pass but can never show it at a provisional position.</summary>
        public bool Visible { get; }
    }

    /// <summary>
    /// Turns a window into absolute tops, walked outward from the anchor. The anchor lands on its own
    /// offset, so no other item's height — right or wrong — can move it.
    /// </summary>
    static class SlotLayout
    {
        public static void Place(ItemRange range, ItemAnchor anchor, IItemHeights heights, List<SlotPlacement> results)
        {
            if (range.Count == 0)
                return;

            var anchorIndex = Mathf.Clamp(anchor.Item, range.First, range.LastExclusive - 1);
            var anchorTop = -anchor.Offset;

            results.Add(new SlotPlacement(anchorIndex, Snap(anchorTop), heights.IsMeasured(anchorIndex)));

            var top = anchorTop;
            for (var index = anchorIndex + 1; index < range.LastExclusive; index++)
            {
                top += heights.Height(index - 1);
                results.Add(new SlotPlacement(index, Snap(top), heights.IsMeasured(index)));
            }

            top = anchorTop;
            for (var index = anchorIndex - 1; index >= range.First; index--)
            {
                top -= heights.Height(index);
                results.Add(new SlotPlacement(index, Snap(top), heights.IsMeasured(index)));
            }
        }

        // Positions are snapped to the physical pixel grid: a half-pixel top blurs text.
        static float Snap(float value)
        {
            var pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            return Mathf.Round(value * pixelsPerPoint) / pixelsPerPoint;
        }
    }
}
