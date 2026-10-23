using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Utils
{
    /// <summary>
    /// Shared utilities for parsing execution log output from RunCommand tool results.
    /// Used by both UI rendering (<see cref="Components.ChatElements.ExecutionLogFormatter"/>)
    /// and data extraction (<see cref="CompletedActionsExtractor"/>).
    /// </summary>
    static class ExecutionLogUtils
    {
        static readonly Regex s_LogPrefixPattern = new(@"^\[(Log|Warning|Error)\]\s*", RegexOptions.Compiled);
        internal static readonly Regex s_BracketedSegmentPattern = new(@"\[([^\]]+)\]", RegexOptions.Compiled);
        internal static readonly Regex s_ObjectReferencePattern = new(@"^(.+)\|InstanceID:(-?\d+)$", RegexOptions.Compiled);

        /// <summary>
        /// Parses a log line, extracting the log type from the prefix and stripping it from the display text.
        /// </summary>
        public static (string displayText, LogType logType) ParseLogLine(string line)
        {
            var logType = LogType.Log;
            var displayText = line;

            var prefixMatch = s_LogPrefixPattern.Match(line);
            if (prefixMatch.Success)
            {
                var typeStr = prefixMatch.Groups[1].Value;
                logType = typeStr switch
                {
                    "Warning" => LogType.Warning,
                    "Error" => LogType.Error,
                    _ => LogType.Log
                };

                displayText = line.Substring(prefixMatch.Length);
            }

            return (displayText.Trim(), logType);
        }

        /// <summary>
        /// Extracts structured references (object refs and asset paths) from bracketed segments in a display text line.
        /// </summary>
        public static List<ParsedReference> ExtractReferences(string displayText)
        {
            var results = new List<ParsedReference>();

            foreach (Match match in s_BracketedSegmentPattern.Matches(displayText))
            {
                var content = match.Groups[1].Value;
                var idMatch = s_ObjectReferencePattern.Match(content);
                if (idMatch.Success && long.TryParse(idMatch.Groups[2].Value, out var instanceId))
                {
                    results.Add(new ParsedReference
                    {
                        DisplayText = idMatch.Groups[1].Value,
                        InstanceId = instanceId
                    });
                }
                else if (TryGetRelativeAssetPath(content, out var assetPath))
                {
                    results.Add(new ParsedReference
                    {
                        DisplayText = content,
                        AssetPath = assetPath
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Returns true if the text is or contains a valid asset path, with the project-relative path in the out param.
        /// Accepts both relative paths (Assets/..., Packages/...) and absolute paths containing those segments.
        /// </summary>
        public static bool TryGetRelativeAssetPath(string text, out string path)
        {
            path = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var t = text.Trim();
            string relative;
            if (t.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || t.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
                relative = t;
            else
            {
                var lastAssets = t.LastIndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
                var lastPackages = t.LastIndexOf("Packages/", StringComparison.OrdinalIgnoreCase);
                var idx = lastAssets >= 0 ? lastAssets : -1;
                if (lastPackages >= 0 && lastPackages > idx)
                    idx = lastPackages;
                if (idx < 0)
                    return false;
                relative = t.Substring(idx);
            }
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(relative)))
                return false;
            path = relative;
            return true;
        }

        /// <summary>
        /// Strips bracketed reference syntax from display text, producing human-readable output.
        /// Replaces [Name|InstanceID:xxx] with Name, and [other] with other.
        /// </summary>
        public static string StripBracketedReferences(string displayText)
        {
            if (string.IsNullOrEmpty(displayText))
                return displayText;

            return s_BracketedSegmentPattern.Replace(displayText, match =>
            {
                var content = match.Groups[1].Value;
                var idMatch = s_ObjectReferencePattern.Match(content);
                return idMatch.Success ? idMatch.Groups[1].Value : content;
            });
        }

        /// <summary>
        /// Pings an asset at the given project-relative path in the Unity Editor.
        /// </summary>
        public static void PingAssetAtPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj != null)
                EditorGUIUtility.PingObject(obj);
        }

        /// <summary>
        /// Pings a Unity object by its instance ID in the Unity Editor.
        /// </summary>
        public static void PingObjectByInstanceId(long instanceId)
        {
#if UNITY_6000_5_OR_NEWER
            var obj = EditorUtility.EntityIdToObject(EntityId.FromULong((ulong)instanceId));
#elif UNITY_6000_3_OR_NEWER
            var obj = EditorUtility.EntityIdToObject((int)instanceId);
#else
            var obj = EditorUtility.InstanceIDToObject((int)instanceId);
#endif
            if (obj != null)
                EditorGUIUtility.PingObject(obj);
        }
    }
}
