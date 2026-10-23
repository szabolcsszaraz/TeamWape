using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.Utils;
using UnityEngine;

namespace Unity.AI.Assistant.Editor
{
    internal sealed class PlanPromptFile : IPlanPromptFile
    {
        public string GetPlanPath() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "plan.md"));

        /// <summary>
        /// Path relative to project for UI (e.g. /plan.md when under Assets).
        /// </summary>
        public string GetPathForDisplay()
        {
            var planPath = GetPlanPath();
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (planPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                var rel = planPath.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                rel = rel.Replace('\\', '/');
                const string assetsPrefix = "Assets/";
                if (rel.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
                    rel = rel.Substring(assetsPrefix.Length);
                return string.IsNullOrEmpty(rel) ? "/" + Path.GetFileName(planPath) : "/" + rel;
            }

            return planPath.Replace('\\', '/');
        }

        /// <summary>
        /// Reads plan.md if it exists and is non-empty after trim; truncates to prompt limit.
        /// </summary>
        public bool TryReadTruncated(out string text)
        {
            text = null;
            var path = GetPlanPath();
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                var raw = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(raw))
                    return false;

                raw = raw.Trim();
                if (raw.Length > AssistantMessageSizeConstraints.PromptLimit)
                    raw = raw.Substring(0, AssistantMessageSizeConstraints.PromptLimit);

                text = raw;
                return true;
            }
            catch
            {
                Debug.LogError($"Failed to read plan file: {path}");
                return false;
            }
        }
    }
}
