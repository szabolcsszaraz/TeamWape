using System.Collections.Generic;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// The display items whose slot must survive the window moving away from them. The set is rebuilt
    /// once per flatten rather than scanned per pass, and it replaces the build-time carve-outs that
    /// used to decide, while a message was being built, which of its blocks could be thrown away.
    /// </summary>
    class ChatItemPins
    {
        // The tail pin keeps the streaming end of the conversation and the context right above it out of
        // the pool. Counted in items rather than messages: one message can be hundreds of them, and a
        // pin is never released, so a whole-message tail retains a giant answer for the session.
        const int k_TailItems = 16;

        readonly AssistantUIContext m_Context;
        readonly HashSet<int> m_Indices = new();

        public ChatItemPins(AssistantUIContext context)
        {
            m_Context = context;
        }

        /// <summary>The item the focus was last seen in, kept as a pin of its own. Re-flattening moves
        /// it, so the realizer owns the index.</summary>
        public int FocusedIndex { get; set; } = DisplayItem.NoIndex;

        public bool Contains(int index) => index == FocusedIndex || m_Indices.Contains(index);

        public void Clear()
        {
            m_Indices.Clear();
            FocusedIndex = DisplayItem.NoIndex;
        }

        /// <summary>
        /// Follows the focus into the realized set. A panel drops focus for reasons the surface does not
        /// control — an element being hidden, the window losing focus — so a lost focus keeps the pin
        /// where it was, and only focus landing somewhere else releases it.
        /// </summary>
        public void TrackFocus(IPanel panel)
        {
            if (panel?.focusController?.focusedElement is not VisualElement focused)
                return;

            for (var element = focused; element != null; element = element.parent)
            {
                if (element is ChatSlot slot)
                {
                    FocusedIndex = slot.BoundIndex;
                    return;
                }
            }

            FocusedIndex = DisplayItem.NoIndex;
        }

        /// <summary>
        /// Rebuilds the pin set. <paramref name="isApiWorking"/> is what tells a call the turn can still
        /// come back to from one it was interrupted on, and it is not derivable from the stream, so a
        /// caller that changes it has to rebuild.
        /// </summary>
        public void Rebuild(IReadOnlyList<MessageModel> messages, IReadOnlyList<DisplayItem> items, bool isApiWorking)
        {
            m_Indices.Clear();
            var tailFrom = items.Count - k_TailItems;

            for (var index = 0; index < items.Count; index++)
            {
                if (index >= tailFrom || IsSacred(messages, items[index], isApiWorking))
                    m_Indices.Add(index);
            }
        }

        /// <summary>Tearing one of these out of the tree cancels its token source and stalls the turn.</summary>
        bool IsSacred(IReadOnlyList<MessageModel> messages, in DisplayItem item, bool isApiWorking)
        {
            if (item.Kind != DisplayItemKind.Block || !ChatItemBinder.TryGetBlock(messages, item, out var model))
                return false;

            switch (model)
            {
                case FunctionCallBlockModel call
                    when (IsTurnStillRunning(messages, item, isApiWorking) && call.Call.Result is not { IsDone: true })
                        || m_Context?.PendingInlineInteractions?.ContainsKey(call.Call.CallId) == true:
                case AcpToolCallBlockModel { HasPendingPermission: true }:
                case AcpToolCallBlockModel { IsDone: false }:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether the turn that owns the item can still complete what it started. Nothing ever writes a
        /// result into a classic call the turn was interrupted on — a result carries a payload, and only
        /// the ACP calls are rewritten on cancel — so an unfinished one reads as running for the rest of
        /// the conversation's life, historical conversations included. Bounded here, the pin it takes is
        /// released with the turn instead of retaining its slot for the session.
        /// </summary>
        static bool IsTurnStillRunning(IReadOnlyList<MessageModel> messages, in DisplayItem item, bool isApiWorking)
            => isApiWorking && !ChatItemBinder.MessageOf(messages, item).IsComplete;
    }
}
