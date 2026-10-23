using System.Collections.Generic;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using UnityEditor;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction
{
    class UserInteractionQueue
    {
        readonly List<UserInteractionEntry> m_Entries = new();
        int m_NonPersistentCount;

        // Total = number of non-persistent items currently pending (counts down as items are handled)
        // CurrentIndex = 1 always, since the bar shows the front item
        public int Total => m_NonPersistentCount;
        public int CurrentIndex => 1;
        public UserInteractionEntry Current => m_Entries.Count > 0 ? m_Entries[0] : null;
        public bool HasPending => m_Entries.Count > 0;

        public void Enqueue(UserInteractionEntry entry)
        {
            var id = UserInteractionId.Next();
            entry.Id = id;
            m_Entries.Add(entry);
            if (!entry.Persistent) m_NonPersistentCount++;
            AssistantEvents.Send(new EventInteractionQueueChanged());
        }

        public void EnqueueFront(UserInteractionEntry entry)
        {
            var id = UserInteractionId.Next();
            entry.Id = id;
            m_Entries.Insert(0, entry);
            if (!entry.Persistent) m_NonPersistentCount++;
            AssistantEvents.Send(new EventInteractionQueueChanged());
        }

        public void Complete(UserInteractionEntry entry)
        {
            if (!m_Entries.Remove(entry))
                return;

            if (!entry.Persistent) m_NonPersistentCount--;
            AssistantEvents.Send(new EventInteractionQueueChanged());

            // Defer auto-resolve to the next editor frame so ToolPermissions state settles first.
            EditorApplication.delayCall += TryAutoResolveNext;
        }

        public void CancelTransient()
        {
            var transient = m_Entries.FindAll(e => !e.Persistent);
            if (transient.Count == 0)
                return;

            foreach (var entry in transient)
            {
                m_Entries.Remove(entry);
                entry.OnCancel?.Invoke();
            }

            m_NonPersistentCount = 0;
            AssistantEvents.Send(new EventInteractionQueueChanged());

            // Defer auto-resolve to the next editor frame. ToolPermissions.Check*() updates
            // State (e.g. State.FileSystem.Allow) as an async continuation that runs after the
            // current synchronous call stack unwinds. Checking TryAutoResolve immediately here
            // would always see stale state and never auto-resolve.
            EditorApplication.delayCall += TryAutoResolveNext;
        }

        void TryAutoResolveNext()
        {
            var resolved = false;

            while (m_Entries.Count > 0 && m_Entries[0].TryAutoResolve?.Invoke() == true)
            {
                var removed = m_Entries[0];
                m_Entries.RemoveAt(0);
                if (!removed.Persistent) m_NonPersistentCount--;
                resolved = true;
            }

            if (resolved)
                AssistantEvents.Send(new EventInteractionQueueChanged());
        }

        public void CancelAll()
        {
            if (m_Entries.Count == 0)
                return;

            var entries = new List<UserInteractionEntry>(m_Entries);
            m_Entries.Clear();
            m_NonPersistentCount = 0;

            foreach (var entry in entries)
                entry.OnCancel?.Invoke();

            AssistantEvents.Send(new EventInteractionQueueChanged());
        }
    }
}
