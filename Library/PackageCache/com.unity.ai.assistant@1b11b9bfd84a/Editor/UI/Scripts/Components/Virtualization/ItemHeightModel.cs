using System;
using System.Collections.Generic;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// Per-item height store for the block-virtualized conversation. Items start at a per-kind
    /// estimate taken from the height census and are replaced by measured heights once realized.
    /// Measurements are cached by content key, so a height survives the re-flatten that every
    /// streamed chunk causes while nothing index-based does; the one item whose content the chunk
    /// changed carries its last height over by identity, until it is measured again. A width change
    /// drops every measurement.
    /// </summary>
    class ItemHeightModel : IItemHeights
    {
        const float k_TopPaddingEstimate = 18f;
        const float k_UserPromptEstimate = 43f;
        const float k_CheckpointEstimate = 23f;
        const float k_RevertedLinkEstimate = 40f;
        const float k_MessageFrameEstimate = 8f;
        const float k_SectionEstimate = 60f;
        const float k_ReasoningSequenceEstimate = 40f;
        const float k_SubagentGroupEstimate = 30f;
        const float k_ThoughtEstimate = 30f;
        const float k_CompletedActionsEstimate = 40f;
        const float k_FeedbackFooterEstimate = 27f;
        const float k_AnswerBlockEstimate = 560f;
        const float k_FunctionCallBlockEstimate = 421f;
        const float k_BlockEstimate = 100f;

        // Exact, transcribed from the USS the footers stand in for, so a footer never delays a reveal.
        const float k_MessageFooterHeight = 18f;
        const float k_ReasoningGroupFooterHeight = 10f;
        const float k_ReasoningSequenceFooterHeight = 10f;
        const float k_ReasoningSeparatorFooterHeight = 9f;

        const int k_MaxCachedMeasurements = 4096;

        const long k_NoKey = 0L;

        readonly CumulativeHeightTree m_Tree = new(0);

        // The heights the stream being replaced was carrying, by identity. Refilled in place on every
        // rebind, so a stream that keeps its items keeps this map's capacity too.
        readonly Dictionary<long, float> m_PreviousByIdentity = new();

        Dictionary<long, float> m_MeasuredByContent = new();

        long[] m_ContentKeys = Array.Empty<long>();
        long[] m_Identities = Array.Empty<long>();
        float[] m_Estimates = Array.Empty<float>();
        bool[] m_Measured = Array.Empty<bool>();

        float m_Width = float.NaN;

        public int Count => m_Tree.Count;

        public float TotalHeight => m_Tree.Total;

        public float Height(int index) => m_Tree.GetHeight(index);

        public float OffsetOf(int index) => m_Tree.PrefixSum(index);

        public float RangeHeight(int from, int toExclusive) => m_Tree.RangeHeight(from, toExclusive);

        public int ItemAtOffset(float offset) => m_Tree.FindIndexAtOffset(offset);

        public bool IsMeasured(int index)
            => index >= 0 && index < m_Measured.Length && m_Measured[index];

        /// <summary>
        /// Rebinds the model to a freshly flattened stream. Items whose content key was measured
        /// before keep that height wherever they now sit; an item the last stream carried under the
        /// same identity keeps the height it had there, pending re-measurement; everything else falls
        /// back to its estimate.
        /// </summary>
        public void SetItems(IReadOnlyList<DisplayItem> items)
        {
            var count = items?.Count ?? 0;

            CapturePreviousHeights();

            // A streamed chunk re-flattens the conversation to change one item, and a stream that kept
            // its length keeps every per-item slot it had: the arrays are rewritten entry by entry below,
            // and rebuilding the tree from heights it already holds would only re-derive its own sums.
            if (count != m_ContentKeys.Length)
            {
                m_ContentKeys = new long[count];
                m_Identities = new long[count];
                m_Estimates = new float[count];
                m_Measured = new bool[count];
                m_Tree.Resize(count);
            }

            for (var i = 0; i < count; i++)
            {
                var item = items[i];
                var key = item.Content.Value;
                var identity = item.Identity.Value;
                var estimate = EstimateFor(item.Kind, item.ElementType, item.FooterStyle);

                m_ContentKeys[i] = key;
                m_Identities[i] = identity;
                m_Estimates[i] = estimate;

                if (item.Kind == DisplayItemKind.GroupFooter)
                {
                    m_Measured[i] = true;
                    m_Tree.SetHeight(i, estimate);
                }
                else if (key != k_NoKey && m_MeasuredByContent.TryGetValue(key, out var measured))
                {
                    m_Measured[i] = true;
                    m_Tree.SetHeight(i, measured);
                }
                else if (identity != k_NoKey && m_PreviousByIdentity.TryGetValue(identity, out var lastKnown))
                {
                    // Every chunk of a streamed answer moves its content key, and the cache is keyed by
                    // that: read back as the per-kind estimate, a measured 2000 px answer would collapse
                    // under the reader on every chunk. Invalidate's rule instead — the last height is a
                    // far better guess than the estimate, and the re-measurement corrects it.
                    m_Measured[i] = false;
                    m_Tree.SetHeight(i, lastKnown);
                }
                else
                {
                    m_Measured[i] = false;
                    m_Tree.SetHeight(i, estimate);
                }
            }

            PruneCache();
        }

        public void Measure(int index, float height)
        {
            if (index < 0 || index >= m_Tree.Count)
                return;

            m_Measured[index] = true;
            m_Tree.SetHeight(index, height);

            var key = m_ContentKeys[index];
            if (key != k_NoKey)
                m_MeasuredByContent[key] = height;
        }

        /// <summary>
        /// Marks an item as needing re-measurement while keeping its last known height, which is a
        /// better estimate than the per-kind default and avoids a visible jump before the remeasure.
        /// </summary>
        public void Invalidate(int index)
        {
            if (index < 0 || index >= m_Tree.Count)
                return;

            m_Measured[index] = false;

            var key = m_ContentKeys[index];
            if (key != k_NoKey)
                m_MeasuredByContent.Remove(key);
        }

        /// <summary>
        /// Drops every measurement when the content width no longer matches the one the heights were
        /// measured at. The first recorded width adopts the existing measurements.
        /// </summary>
        public void InvalidateForWidth(float width)
        {
            if (float.IsNaN(m_Width) || m_Width == width)
            {
                m_Width = width;
                return;
            }

            m_Width = width;
            m_MeasuredByContent.Clear();

            for (var i = 0; i < m_Tree.Count; i++)
            {
                m_Measured[i] = false;
                m_Tree.SetHeight(i, m_Estimates[i]);
            }
        }

        public static float EstimateFor(DisplayItemKind kind, Type elementType, GroupFooterStyle footerStyle)
        {
            switch (kind)
            {
                case DisplayItemKind.TopPadding:
                    return k_TopPaddingEstimate;
                case DisplayItemKind.UserPrompt:
                    return k_UserPromptEstimate;
                case DisplayItemKind.Checkpoint:
                    return k_CheckpointEstimate;
                case DisplayItemKind.RevertedLink:
                    return k_RevertedLinkEstimate;
                case DisplayItemKind.MessageFrame:
                    return k_MessageFrameEstimate;
                case DisplayItemKind.Section:
                    return k_SectionEstimate;
                case DisplayItemKind.ReasoningGroup:
                case DisplayItemKind.ReasoningSequence:
                    return k_ReasoningSequenceEstimate;
                case DisplayItemKind.SubagentGroup:
                    return k_SubagentGroupEstimate;
                case DisplayItemKind.Thought:
                    return k_ThoughtEstimate;
                case DisplayItemKind.CompletedActions:
                    return k_CompletedActionsEstimate;
                case DisplayItemKind.FeedbackFooter:
                    return k_FeedbackFooterEstimate;
                case DisplayItemKind.Block:
                    return BlockEstimate(elementType);
                case DisplayItemKind.GroupFooter:
                    return FooterHeight(footerStyle);
                default:
                    return k_BlockEstimate;
            }
        }

        static float FooterHeight(GroupFooterStyle footerStyle)
        {
            return footerStyle switch
            {
                GroupFooterStyle.Message => k_MessageFooterHeight,
                GroupFooterStyle.ReasoningGroup => k_ReasoningGroupFooterHeight,
                GroupFooterStyle.ReasoningSequence => k_ReasoningSequenceFooterHeight,
                GroupFooterStyle.ReasoningSeparator => k_ReasoningSeparatorFooterHeight,
                _ => 0f
            };
        }

        static float BlockEstimate(Type elementType)
        {
            if (elementType == typeof(ChatElementBlockAnswer))
                return k_AnswerBlockEstimate;

            if (elementType == typeof(ChatElementBlockFunctionCall))
                return k_FunctionCallBlockEstimate;

            return k_BlockEstimate;
        }

        void CapturePreviousHeights()
        {
            m_PreviousByIdentity.Clear();

            for (var i = 0; i < m_Identities.Length; i++)
            {
                var identity = m_Identities[i];
                if (identity != k_NoKey)
                    m_PreviousByIdentity[identity] = m_Tree.GetHeight(i);
            }
        }

        void PruneCache()
        {
            if (m_MeasuredByContent.Count <= k_MaxCachedMeasurements)
                return;

            // Content keys churn on every streamed chunk, so drop what the current stream can no longer use.
            var live = new Dictionary<long, float>(m_ContentKeys.Length);
            foreach (var key in m_ContentKeys)
            {
                if (key != k_NoKey && m_MeasuredByContent.TryGetValue(key, out var height))
                    live[key] = height;
            }

            m_MeasuredByContent = live;
        }
    }
}
