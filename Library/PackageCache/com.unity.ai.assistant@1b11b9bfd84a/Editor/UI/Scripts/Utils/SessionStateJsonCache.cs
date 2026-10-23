using System;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Utils
{
    static class SessionStateJsonCache
    {
        // Takes a factory delegate instead of a `where T : new()` constraint:
        // `new T()` compiles to Activator.CreateInstance<T>(), which the AI Assistant tool-permission
        // validator forbids in any method reachable from a tool. The factory keeps the call site
        // a direct constructor invocation that the validator is happy with.
        public static T Load<T>(string key, Func<T> factory) where T : class
        {
            var json = SessionState.GetString(key, null);
            if (string.IsNullOrEmpty(json)) return factory();
            return JsonUtility.FromJson<T>(json) ?? factory();
        }

        public static void Save<T>(string key, T cache, bool isEmpty) where T : class
        {
            if (isEmpty)
                SessionState.EraseString(key);
            else
                SessionState.SetString(key, JsonUtility.ToJson(cache));
        }
    }
}
