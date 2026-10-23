using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Tools.Editor
{
    /// <summary>
    /// Persists pending ExitPlanMode interaction state across domain reloads via SessionState.
    /// Mirrors the JSON caching pattern previously embedded in AssistantUISessionState; the SessionState
    /// key is preserved verbatim ("AssistantUserSession_ExitPlanModeStateByCallId") so in-flight state
    /// survives the move from the UI assembly to the Tools assembly.
    /// </summary>
    sealed class ExitPlanModeStateStore : ScriptableSingleton<ExitPlanModeStateStore>
    {
        const string k_SessionStateKey = "AssistantUserSession_ExitPlanModeStateByCallId";

        [Serializable]
        class StateEntry
        {
            public string callId;
            public string conversationId;
            public string planPath;
            public string planContent;
            public string title;
            public bool expanded;
        }

        [Serializable]
        class StateCache
        {
            public List<StateEntry> entries = new();
        }

        public void SetState(
            Guid callId,
            string conversationId,
            string planPath,
            string planContent,
            string title,
            bool expanded)
        {
            var key = callId.ToString();
            var cache = LoadCache();
            var existing = cache.entries.Find(e => e.callId == key);
            if (existing != null)
            {
                existing.conversationId = conversationId;
                existing.planPath = planPath;
                existing.planContent = planContent;
                existing.title = title;
                existing.expanded = expanded;
            }
            else
            {
                cache.entries.Add(new StateEntry
                {
                    callId = key,
                    conversationId = conversationId,
                    planPath = planPath,
                    planContent = planContent,
                    title = title,
                    expanded = expanded
                });
            }
            SaveCache(cache);
        }

        public List<(
            Guid callId,
            string planPath,
            string planContent,
            string title,
            bool expanded)> GetStatesForConversation(string conversationId)
        {
            var result = new List<(Guid, string, string, string, bool)>();
            if (string.IsNullOrEmpty(conversationId))
                return result;

            var cache = LoadCache();
            foreach (var entry in cache.entries)
            {
                if (entry.conversationId != conversationId) continue;
                if (!Guid.TryParse(entry.callId, out var callId)) continue;
                result.Add((callId, entry.planPath, entry.planContent, entry.title, entry.expanded));
            }
            return result;
        }

        public string GetConversationId(Guid callId)
        {
            var key = callId.ToString();
            var cache = LoadCache();
            var entry = cache.entries.Find(e => e.callId == key);
            return entry?.conversationId;
        }

        public bool GetExpanded(Guid callId)
        {
            var key = callId.ToString();
            var cache = LoadCache();
            var entry = cache.entries.Find(e => e.callId == key);
            return entry?.expanded ?? false;
        }

        public void ClearState(Guid callId)
        {
            var key = callId.ToString();
            var cache = LoadCache();
            if (cache.entries.RemoveAll(e => e.callId == key) > 0)
                SaveCache(cache);
        }

        static StateCache LoadCache()
        {
            var json = SessionState.GetString(k_SessionStateKey, null);
            if (string.IsNullOrEmpty(json)) return new StateCache();
            try
            {
                return JsonUtility.FromJson<StateCache>(json) ?? new StateCache();
            }
            catch (ArgumentException)
            {
                return new StateCache();
            }
        }

        static void SaveCache(StateCache cache)
        {
            if (cache.entries.Count == 0)
                SessionState.EraseString(k_SessionStateKey);
            else
                SessionState.SetString(k_SessionStateKey, JsonUtility.ToJson(cache));
        }
    }
}
