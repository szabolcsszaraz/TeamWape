using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.Data
{
    /// <summary>
    /// Static event for broadcasting todo list updates from the write_todos tool
    /// to the chat UI for rendering.
    /// </summary>
    static class TodoUpdateEvent
    {
        /// <summary>
        /// Fired when the todo list is updated. Subscribers receive the full list of todo items,
        /// the plan path, and the originating conversation ID.
        /// </summary>
        public static event Action<List<TodoItem>, string, string> OnTodoListUpdated;

        /// <summary>
        /// Raise the todo update event on the main thread (fire-and-forget). Subscribers touch
        /// Unity UI, so the dispatch must always land on the main thread regardless of the caller.
        /// </summary>
        public static void Raise(List<TodoItem> items, string planPath, string conversationId)
        {
            MainThread.DispatchAndForget(() => OnTodoListUpdated?.Invoke(items, planPath, conversationId));
        }
    }
}
