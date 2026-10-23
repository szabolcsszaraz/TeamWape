using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.AI.MCP.Editor.Settings.Utilities;
using Unity.AI.Toolkit;

namespace Unity.AI.MCP.Editor
{
    /// <summary>
    /// Persists connection history on a per-project basis.
    /// Uses ScriptableSingleton to save/load from Library folder.
    ///
    /// Runtime access goes through <see cref="ConnectionStore"/> (thread-safe, static).
    /// This class only handles serialization: hydrating the store on domain load
    /// and flushing it back before save.
    /// </summary>
    [FilePath("Library/AI.MCP/connections-v2.asset", FilePathAttribute.Location.ProjectFolder)]
    class ConnectionRegistry : ScriptableSingleton<ConnectionRegistry>
    {
        [InitializeOnLoadMethod]
        static void EnsureLoaded() => _ = instance;
        /// <summary>
        /// Serialized list used only for Unity persistence (save/load).
        /// At runtime, all access goes through <see cref="ConnectionStore"/>.
        /// </summary>
        [SerializeField]
        List<ConnectionRecord> connections = new();

        SaveManager m_SaveManager;

        void OnEnable()
        {
            // Populate runtime store from serialized list
            foreach (var record in connections)
            {
                if (record?.Identity?.CombinedIdentityKey != null)
                    ConnectionStore.ConnectionsByIdentity[record.Identity.CombinedIdentityKey] = record;
            }

            m_SaveManager = new SaveManager(() =>
            {
                FlushToSerializedList();
                Save(true);
            });

            // Wire up save callback so ConnectionStore can trigger persistence
            ConnectionStore.OnSaveRequested = () =>
            {
                m_SaveManager.MarkDirty();
                EditorTask.delayCall += () => m_SaveManager.SaveImmediately();
            };

        }

        /// <summary>
        /// Flush the runtime ConcurrentDictionary back into the serialized list for Unity persistence.
        /// Called on the main thread before Save (scene save or editor quit).
        /// </summary>
        void FlushToSerializedList()
        {
            connections.Clear();
            connections.AddRange(ConnectionStore.ConnectionsByIdentity.Values);
        }
    }
}
