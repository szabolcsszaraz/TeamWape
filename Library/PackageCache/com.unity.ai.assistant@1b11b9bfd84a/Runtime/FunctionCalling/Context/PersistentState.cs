using System;
using System.IO;
using Newtonsoft.Json;
using Unity.AI.Assistant.Utils;
using UnityEngine;

namespace Unity.AI.Assistant.FunctionCalling
{
    /// <summary>
    /// Key/value storage that persists across editor sessions, scoped to a single
    /// conversation. Values are serialized as JSON and saved under
    /// <c>Library/AI.Conversations/{conversationId}.json</c>. Tools and link handlers
    /// can use this to remember state between calls within the same conversation.
    /// </summary>
    public sealed class PersistentStorage
    {
        const string k_StoragePath = "Library/AI.Conversations";

        readonly string m_ConversationId;
        SerializableDictionary<string, string> m_Cache;

        internal PersistentStorage(string conversationId)
        {
            m_ConversationId = conversationId;
        }

        /// <summary>
        /// Attempts to retrieve a previously stored value.
        /// </summary>
        /// <param name="key">A key unique within the scope of <typeparamref name="T"/>.</param>
        /// <param name="state">When this method returns <c>true</c>, the deserialized value; otherwise <c>default</c>.</param>
        /// <typeparam name="T">The type the value was stored as. Values are scoped by type, so the same key can hold different values for different <typeparamref name="T"/>.</typeparam>
        /// <returns><c>true</c> if a value was found and deserialized successfully; <c>false</c> otherwise.</returns>
        public bool TryGetState<T>(string key, out T state)
        {
            EnsureLoaded();

            try
            {
                var stateKey = GetStateKey<T>(key);
                if (m_Cache.TryGetValue(stateKey, out var json))
                {
                    state = JsonConvert.DeserializeObject<T>(json);
                    return true;
                }
            }
            catch (Exception e)
            {
                InternalLog.LogException(e);
            }

            state = default;
            return false;
        }

        /// <summary>
        /// Stores a value, overwriting any existing value for the same key and type.
        /// The store is saved to disk immediately.
        /// </summary>
        /// <param name="key">A key unique within the scope of <typeparamref name="T"/>.</param>
        /// <param name="state">The value to store. It is serialized as JSON.</param>
        /// <typeparam name="T">The type used to scope the value.</typeparam>
        public void SetState<T>(string key, T state)
        {
            EnsureLoaded();

            try
            {
                var stateKey = GetStateKey<T>(key);
                m_Cache[stateKey] = JsonConvert.SerializeObject(state);
            }
            catch (Exception e)
            {
                InternalLog.LogException(e);
            }

            Save();
        }

        /// <summary>
        /// Removes the value stored under the given key and type. Has no effect if no
        /// value was stored.
        /// </summary>
        /// <param name="key">A key unique within the scope of <typeparamref name="T"/>.</param>
        /// <typeparam name="T">The type used to scope the value.</typeparam>
        public void ClearState<T>(string key)
        {
            EnsureLoaded();

            var stateKey = GetStateKey<T>(key);
            if (m_Cache.Remove(stateKey))
                Save();
        }

        internal void Clear()
        {
            m_Cache?.Clear();
            Delete(m_ConversationId);
        }

        internal static void Delete(string conversationId)
        {
            try
            {
                var path = GetFilePath(conversationId);
                if (path == null)
                    return;

                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                InternalLog.LogException(e);
            }
        }

        void EnsureLoaded()
        {
            if (m_Cache != null)
                return;

            try
            {
                var path = GetFilePath(m_ConversationId);
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    m_Cache = JsonConvert.DeserializeObject<SerializableDictionary<string, string>>(json);
                }
                else
                {
                    m_Cache = new SerializableDictionary<string, string>();
                }
            }
            catch (Exception e)
            {
                InternalLog.LogException(e);
                m_Cache = new SerializableDictionary<string, string>();
            }
        }

        void Save()
        {
            try
            {
                var path = GetFilePath(m_ConversationId);
                if (path == null)
                    return;

                var directory = Path.GetDirectoryName(path);
                if (directory != null && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var json = JsonConvert.SerializeObject(m_Cache);
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                InternalLog.LogException(e);
            }
        }

        static string GetFilePath(string conversationId)
        {
            return Path.Combine(Application.dataPath, $"../{k_StoragePath}", $"{conversationId}.json");
        }

        static string GetStateKey<T>(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            return $"{typeof(T).FullName}_{key}";
        }
    }

}
