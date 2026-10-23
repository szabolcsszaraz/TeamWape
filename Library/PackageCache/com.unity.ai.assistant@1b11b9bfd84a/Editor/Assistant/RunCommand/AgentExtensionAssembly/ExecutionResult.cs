using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.Agent.Dynamic.Extension.Editor
{
    /// <exclude />
    [Serializable]
    public struct ExecutionLog
    {
        public string Log;
        public LogType LogType;
        public long[] LoggedObjectInstanceIds;
        public string[] LoggedObjectNames;

        public GameObject[] LoggedObjects
        {
            get
            {
                if (LoggedObjectInstanceIds == null || LoggedObjectInstanceIds.Length == 0)
                    return null;

                var loggedObjects = new GameObject[LoggedObjectInstanceIds.Length];
                for (var i = 0; i < LoggedObjectInstanceIds.Length; i++)
                {
                    var instanceId = LoggedObjectInstanceIds[i];
#if UNITY_6000_5_OR_NEWER
                    var obj = EditorUtility.EntityIdToObject(EntityId.FromULong((ulong)instanceId)) as GameObject;
#elif UNITY_6000_3_OR_NEWER
                    var obj = EditorUtility.EntityIdToObject((int)instanceId) as GameObject;
#else
                    var obj = EditorUtility.InstanceIDToObject((int)instanceId) as GameObject;
#endif

                    // There is a chance for a clash, make sure the object is a GameObject and the name matches:
                    if (obj != null && LoggedObjectNames?.Length > i && LoggedObjectNames?[i] == obj.name)
                    {
                        loggedObjects[i] = obj;
                    }
                }
                return loggedObjects;
            }
        }

        public ExecutionLog(string formattedLog, LogType logType, object[] loggedObjects = null)
        {
            Log = formattedLog;
            LogType = logType;

            if (loggedObjects != null)
            {
                LoggedObjectInstanceIds = new long[loggedObjects.Length];
                for (var i = 0; i < loggedObjects.Length; i++)
                {
                    var loggedObject = loggedObjects[i];
                    var obj = loggedObject as Object;
                    if (obj != null)
                    {
#if UNITY_6000_5_OR_NEWER
                        LoggedObjectInstanceIds[i] = (long)EntityId.ToULong(obj.GetEntityId());
#else
                        LoggedObjectInstanceIds[i] = obj.GetInstanceID();
#endif
                    }
                }
            }
            else
            {
                LoggedObjectInstanceIds = null;
            }

            LoggedObjectNames = loggedObjects != null ? new string[loggedObjects.Length] : null;

            if (LoggedObjectNames != null)
            {
                for (int i = 0; i < loggedObjects.Length; i++)
                {
                    LoggedObjectNames[i] = loggedObjects[i] is Object obj ? obj.name : loggedObjects[i]?.ToString();
                }
            }
        }
    }

    /// <exclude />
    [Serializable]
    public class ExecutionResult : ReadonlyExecutionResult
    {
        public static readonly string LinkTextColor = EditorGUIUtility.isProSkin ? "#8facef" : "#055b9f";
        public static readonly string WarningTextColor = EditorGUIUtility.isProSkin ? "#DFB33D" : "#B76300";

        int UndoGroup;

        public string ConsoleLogs;

        public ExecutionResult(string commandName) : base(commandName)
        {
        }

        public void RegisterObjectCreation(Object objectCreated)
        {
            if (objectCreated != null)
                Undo.RegisterCreatedObjectUndo(objectCreated, $"{objectCreated.name} was created");
        }

        public void RegisterObjectCreation(Component component)
        {
            if (component != null)
                Undo.RegisterCreatedObjectUndo(component, $"{component} was attached to {component.gameObject.name}");
        }

        public void RegisterObjectModification(Object objectToRegister, string operationDescription = "")
        {
            if (!string.IsNullOrEmpty(operationDescription))
                Undo.RecordObject(objectToRegister, operationDescription);
            else
                Undo.RegisterCompleteObjectUndo(objectToRegister, $"{objectToRegister.name} was modified");
        }

        public void DestroyObject(Object objectToDestroy)
        {
            if (EditorUtility.IsPersistent(objectToDestroy))
            {
                var path = AssetDatabase.GetAssetPath(objectToDestroy);
                AssetDatabase.DeleteAsset(path);
            }
            else
            {
                if (!EditorApplication.isPlaying)
                    Undo.DestroyObjectImmediate(objectToDestroy);
                else
                    Object.Destroy(objectToDestroy);
            }
        }

        public override void Start()
        {
            base.Start();

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(CommandName ?? "Run command execution");
            UndoGroup = Undo.GetCurrentGroup();
        }

        public override void End()
        {
            base.End();

            Undo.CollapseUndoOperations(UndoGroup);
        }

        protected override void HandleConsoleLog(string logString, string stackTrace, LogType type)
        {
            base.HandleConsoleLog(logString, stackTrace, type);

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Warning)
            {
                ConsoleLogs += $"{type}: {logString}\n";
            }
        }
    }
}
