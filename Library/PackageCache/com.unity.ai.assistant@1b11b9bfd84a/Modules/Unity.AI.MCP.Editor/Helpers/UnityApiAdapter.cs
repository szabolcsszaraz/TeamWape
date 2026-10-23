using UnityEditor;
using UnityEngine;

namespace Unity.AI.MCP.Editor.Helpers
{
    /// <summary>
    /// Provides a compatibility layer for Unity API changes across versions.
    /// Centralizes all version-specific API differences to avoid scattered #if directives.
    /// </summary>
    static class UnityApiAdapter
    {
#if UNITY_6000_3_OR_NEWER
        /// <summary>
        /// Gets a Unity Object from its ID (EntityId in 6.3+, InstanceID in earlier versions).
        /// </summary>
        /// <param name="id">The EntityId of the object.</param>
        /// <returns>The Unity Object associated with the EntityId, or null if not found.</returns>
        public static UnityEngine.Object GetObjectFromId(long id)
        {
#if UNITY_6000_5_OR_NEWER
            return EditorUtility.EntityIdToObject(EntityId.FromULong((ulong)id));
#else
            return EditorUtility.EntityIdToObject((int)id);
#endif
        }

        /// <summary>
        /// Gets the ID of the active selected object (EntityId in 6.3+, InstanceID in earlier versions).
        /// </summary>
        /// <returns>The EntityId of the currently selected object.</returns>
        public static long GetActiveSelectionId()
        {
#if UNITY_6000_5_OR_NEWER
            return (long)EntityId.ToULong(Selection.activeEntityId);
#else
            return Selection.activeEntityId;
#endif
        }

        /// <summary>
        /// Gets the field name for the LogEntry ID field used in reflection.
        /// Unity 6.3+ renamed "instanceID" to "entityId".
        /// </summary>
        /// <returns>The string "entityId" for Unity 6.3+.</returns>
        public static string GetLogEntryIdFieldName()
        {
            return "entityId";
        }
#else
        /// <summary>
        /// Gets a Unity Object from its ID (EntityId in 6.3+, InstanceID in earlier versions).
        /// </summary>
        /// <param name="id">The InstanceID of the object.</param>
        /// <returns>The Unity Object associated with the InstanceID, or null if not found.</returns>
        public static UnityEngine.Object GetObjectFromId(long id)
        {
            return EditorUtility.InstanceIDToObject((int)id);
        }

        /// <summary>
        /// Gets the ID of the active selected object (EntityId in 6.3+, InstanceID in earlier versions).
        /// </summary>
        /// <returns>The InstanceID of the currently selected object.</returns>
        public static long GetActiveSelectionId()
        {
            return Selection.activeInstanceID;
        }

        /// <summary>
        /// Gets the field name for the LogEntry ID field used in reflection.
        /// Unity 6.3+ renamed "instanceID" to "entityId".
        /// </summary>
        /// <returns>The string "instanceID" for Unity versions before 6.3.</returns>
        public static string GetLogEntryIdFieldName()
        {
            return "instanceID";
        }
#endif
    }
}
