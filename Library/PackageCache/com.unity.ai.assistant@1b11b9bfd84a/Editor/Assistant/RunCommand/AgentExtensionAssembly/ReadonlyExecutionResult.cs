using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.Agent.Dynamic.Extension.Editor
{
    /// <exclude />
    [Serializable]
    public class ReadonlyExecutionResult
    {
        public static readonly Regex PlaceholderRegex = new(@"\{(\d+)\}", RegexOptions.Compiled);

        public int Id = 1;
        public int MessageIndex = -1;

        public readonly string CommandName;

        public string FencedTag;

        public List<ExecutionLog> Logs = new();

        public bool SuccessfullyStarted;

        public ReadonlyExecutionResult(string commandName)
        {
            CommandName = commandName;
        }

        public virtual void Start()
        {
            SuccessfullyStarted = true;

            Application.logMessageReceived += HandleConsoleLog;
        }

        public virtual void End()
        {
            Application.logMessageReceived -= HandleConsoleLog;
        }

        public void Log(string log, params object[] references)
        {
            Logs.Add(new ExecutionLog(log, LogType.Log, references));
        }

        public void LogWarning(string log, params object[] references)
        {
            Logs.Add(new ExecutionLog(log, LogType.Warning, references));
        }

        public void LogError(string log, params object[] references)
        {
            Logs.Add(new ExecutionLog(log, LogType.Error, references));
        }

        protected virtual void HandleConsoleLog(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Warning)
            {
                Logs.Add(new ExecutionLog(logString, type, null));
            }
        }

        public List<string> GetFormattedLogs()
        {
            List<string> formattedLogs = new();

            if (Logs == null)
            {
                return formattedLogs;
            }

            foreach (var content in Logs)
            {
                if (string.IsNullOrEmpty(content.Log))
                {
                    continue;
                }

                string logTemplate = content.Log;
                var references = content.LoggedObjects;
                var names = content.LoggedObjectNames;

                string formattedLog = PlaceholderRegex.Replace(logTemplate, match =>
                {
                    if (int.TryParse(match.Groups[1].Value, out int index))
                    {
                        // Unity Object: [displayText|InstanceID:instanceId] for clickable link in UI
                        if (references != null && index >= 0 && index < references.Length)
                        {
                            var obj = references[index];
                            if (obj != null)
#if UNITY_6000_5_OR_NEWER
                                return "[" + obj.name + "|InstanceID:" + (long)EntityId.ToULong(obj.GetEntityId()) + "]";
#else
                                return "[" + obj.name + "|InstanceID:" + obj.GetInstanceID() + "]";
#endif
                        }

                        // Non-Object (string, int, etc.): [displayText] only; renderer may detect paths and make them clickable
                        if (names != null && index >= 0 && index < names.Length && names[index] != null)
                            return "[" + names[index] + "]";
                    }

                    return match.Value;
                });
                formattedLogs.Add($"[{content.LogType}] {formattedLog}");
            }
            return formattedLogs;
        }
    }
}
