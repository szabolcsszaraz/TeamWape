using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Utils.Perf;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    internal class AssistantUISessionState : ScriptableSingleton<AssistantUISessionState>
    {
        const string k_Prefix = "AssistantUserSession_";

        const string k_HistoryOpen = k_Prefix + "HistoryOpen";
        const string k_LastActiveConversationId = k_Prefix + "LastActiveConversationId";
        const string k_LastActiveProviderId = k_Prefix + "LastActiveProviderId";
        const string k_LastActiveMode = k_Prefix + "LastActiveMode";
        const string k_IncompleteMessageId = k_Prefix + "IncompleteMessageId";
        const string k_ProgressStartTime = k_Prefix + "ProgressStartTime";
        const string k_Prompt = k_Prefix + "Prompt";
        const string k_Command = k_Prefix + "Command";
        const string k_Context = k_Prefix + "Context";
        const string k_AvailableCommands = k_Prefix + "AvailableCommands_";
        const string k_TodoStateByConversation = k_Prefix + "TodoStateByConversation";

        // Serialized backing for the prompt so Undo.RecordObject can snapshot/restore it.
        // ScriptableSingleton survives domain reloads in memory (no [FilePath] needed).
        // Like SessionState, the value is lost when the Editor is closed.
        [SerializeField] string m_Prompt = "";

        [Serializable]
        class TodoStateEntry
        {
            public string conversationId;
            public List<TodoItem> items = new();
            public string planPath;
            public bool expanded = true;
        }

        [Serializable]
        class TodoStateCache
        {
            public List<TodoStateEntry> entries = new();
        }

        [Serializable]
        class AvailableCommandsCache
        {
            public List<CommandEntry> commands = new();
        }

        [Serializable]
        struct CommandEntry
        {
            public string name;
            public string description;
        }

        public bool IsHistoryOpen
        {
            get => SessionState.GetBool(k_HistoryOpen, false);
            set => SessionState.SetBool(k_HistoryOpen, value);
        }

        public string LastActiveConversationId
        {
            get => SessionState.GetString(k_LastActiveConversationId, null);
            set => SessionState.SetString(k_LastActiveConversationId, value);
        }

        public string LastActiveProviderId
        {
            get => SessionState.GetString(k_LastActiveProviderId, null);
            set => SessionState.SetString(k_LastActiveProviderId, value);
        }

        public AssistantMode LastActiveMode
        {
            get
            {
                var stored = SessionState.GetString(k_LastActiveMode, null);
                if (Enum.TryParse<AssistantMode>(stored, out var mode))
                    return mode;

                return AssistantMode.Agent;
            }
            set => SessionState.SetString(k_LastActiveMode, value.ToString());
        }

        public string IncompleteMessageId
        {
            get => SessionState.GetString(k_IncompleteMessageId, null);
            set => SessionState.SetString(k_IncompleteMessageId, value);
        }

        public float ProgressStartTime
        {
            get => SessionState.GetFloat(k_ProgressStartTime, 0f);
            set => SessionState.SetFloat(k_ProgressStartTime, value);
        }

        public string Context
        {
            get => SessionState.GetString(k_Context, null);
            set => SessionState.SetString(k_Context, value);
        }

        public string Prompt
        {
            get => m_Prompt;
            set => m_Prompt = value;
        }

        public string Command
        {
            get => SessionState.GetString(k_Prompt, null);
            set => SessionState.SetString(k_Prompt, value);
        }

        public void SetTodoState(string conversationId, List<TodoItem> items, string planPath, bool expanded)
        {
            using var perfScope = AssistantPerf.Measure(PerfProbe.TodoStateSave);

            var cache = SessionStateJsonCache.Load(k_TodoStateByConversation, () => new TodoStateCache());
            var existing = cache.entries.Find(e => e.conversationId == conversationId);
            if (existing != null)
            {
                existing.items = items ?? new List<TodoItem>();
                existing.planPath = planPath;
                existing.expanded = expanded;
            }
            else
            {
                cache.entries.Add(new TodoStateEntry { conversationId = conversationId, items = items ?? new List<TodoItem>(), planPath = planPath, expanded = expanded });
            }
            SessionStateJsonCache.Save(k_TodoStateByConversation, cache, cache.entries.Count == 0);

            AssistantPerf.SetGauge(PerfGauge.TodoStateEntries, cache.entries.Count);
        }

        public (List<TodoItem> items, string planPath, bool expanded) GetTodoState(string conversationId)
        {
            using var perfScope = AssistantPerf.Measure(PerfProbe.TodoStateLoad);

            var cache = SessionStateJsonCache.Load(k_TodoStateByConversation, () => new TodoStateCache());
            var entry = cache.entries.Find(e => e.conversationId == conversationId);
            if (entry == null || entry.items == null || entry.items.Count == 0)
                return (null, null, true);
            return (entry.items, entry.planPath, entry.expanded);
        }

        public void ClearTodoState(string conversationId)
        {
            var cache = SessionStateJsonCache.Load(k_TodoStateByConversation, () => new TodoStateCache());
            if (cache.entries.RemoveAll(e => e.conversationId == conversationId) > 0)
                SessionStateJsonCache.Save(k_TodoStateByConversation, cache, cache.entries.Count == 0);
        }

        public void SetAvailableCommands(string providerId, IReadOnlyList<(string name, string description)> commands)
        {
            if (string.IsNullOrEmpty(providerId))
                return;

            var key = k_AvailableCommands + providerId;
            if (commands == null || commands.Count == 0)
            {
                SessionState.EraseString(key);
                return;
            }

            var cache = new AvailableCommandsCache
            {
                commands = commands
                    .Select(c => new CommandEntry { name = c.name, description = c.description })
                    .ToList()
            };

            SessionState.SetString(key, JsonUtility.ToJson(cache));
        }

        public (string name, string description)[] GetAvailableCommands(string providerId)
        {
            if (string.IsNullOrEmpty(providerId))
                return null;

            var json = SessionState.GetString(k_AvailableCommands + providerId, null);
            if (string.IsNullOrEmpty(json))
                return null;

            var cache = JsonUtility.FromJson<AvailableCommandsCache>(json);
            if (cache?.commands == null || cache.commands.Count == 0)
                return null;

            return cache.commands
                .Select(c => (c.name, c.description))
                .ToArray();
        }

        public void ClearProgressStartTime() => SessionState.EraseFloat(k_ProgressStartTime);
    }
}
