using System;
using System.Collections.Generic;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Utils.Perf;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// Slots bucketed by <see cref="SlotKey"/>, so a rented slot always arrives with the wrapper chain
    /// and the element type its item needs. The pool grows to its high-water mark and never shrinks, and
    /// a returned slot is only hidden — it keeps its place in the content layer, which is what makes a
    /// scroll frame free of hierarchy churn.
    /// </summary>
    class ChatSlotPool
    {
        readonly Func<DisplayItem, VisualElement> m_ContentFactory;
        readonly Dictionary<SlotKey, Stack<ChatSlot>> m_Free = new();
        readonly HashSet<ChatSlot> m_Rented = new();
        readonly List<ChatSlot> m_All = new();

        public ChatSlotPool(Func<DisplayItem, VisualElement> contentFactory)
        {
            m_ContentFactory = contentFactory;
        }

        /// <summary>Slots ever built, rented or not. The high-water mark of the realized window.</summary>
        public int LiveCount => m_All.Count;

        public int RentedCount => m_Rented.Count;

        /// <summary>Every live slot, in creation order. The realizer walks it to place the free ones.</summary>
        public IReadOnlyList<ChatSlot> All => m_All;

        public ChatSlot Rent(in DisplayItem item)
        {
            var startTicks = AssistantPerf.Now;
            var slot = RentSlot(item);
            ReportContentElements();
            Mark("chat.pool_rent", slot, startTicks);
            return slot;
        }

        public void Return(ChatSlot slot)
        {
            var startTicks = AssistantPerf.Now;

            if (!m_Rented.Remove(slot))
                throw new InvalidOperationException("Slot " + slot.Key + " was returned twice, or was never rented.");

            slot.BoundIndex = DisplayItem.NoIndex;
            slot.BoundContent = default;
            slot.FailedContent = default;
            slot.Unseal();
            slot.SetVisible(false);
            slot.SetDisplay(false);

            if (!m_Free.TryGetValue(slot.Key, out var free))
            {
                free = new Stack<ChatSlot>();
                m_Free.Add(slot.Key, free);
            }

            free.Push(slot);
            ReportContentElements();
            Mark("chat.pool_return", slot, startTicks);
        }

        ChatSlot RentSlot(in DisplayItem item)
        {
            var key = SlotKey.For(item);

            if (m_Free.TryGetValue(key, out var free) && free.Count > 0)
            {
                var reused = free.Pop();
                m_Rented.Add(reused);
                reused.SetDisplay(true);
                return reused;
            }

            // Built before the slot exists: a factory that throws must not leave behind a slot the pool
            // has half-recorded, one leaked per pass for as long as its item stays in the window.
            var content = m_ContentFactory(item);
            var slot = new ChatSlot(key);

            if (content != null)
                slot.Attach(content);

            m_All.Add(slot);
            m_Rented.Add(slot);
            return slot;
        }

        /// <summary>A slot's content is attached once and never detached, so how many chat elements the
        /// content layer holds is the pool's own high-water mark. Reported from both transitions, so
        /// turning instrumentation on part-way through a session catches up on the next one.</summary>
        void ReportContentElements() => AssistantPerf.SetGauge(PerfGauge.SlotContentElements, m_All.Count);

        void Mark(string name, ChatSlot slot, long startTicks)
        {
            if (!AssistantPerf.Enabled)
                return;

            AssistantPerf.MarkElapsed(name, "key=" + slot.Key + ";live=" + m_All.Count, startTicks);
        }
    }
}
