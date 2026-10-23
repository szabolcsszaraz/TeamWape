using System;
using System.Text.RegularExpressions;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    /// <summary>
    /// UI rendering for execution logs from RunCommand tool results.
    /// Shared between <see cref="RunCommandFunctionCallElement"/> and <see cref="AcpRunCommandRenderer"/>.
    /// For log parsing utilities, see <see cref="ExecutionLogUtils"/>.
    /// </summary>
    static class ExecutionLogFormatter
    {
        static readonly Color k_WarningColor = new(1f, 0.92f, 0.016f); // Yellow
        static readonly Color k_ErrorColor = new(1f, 0.32f, 0.29f);    // Red

        /// <summary>
        /// Creates a log row <see cref="VisualElement"/> with clickable object/asset links.
        /// </summary>
        public static VisualElement CreateLogRow(string displayText, LogType logType)
        {
            var row = new VisualElement();
            row.AddToClassList("mui-execution-log-row");

            var matches = ExecutionLogUtils.s_BracketedSegmentPattern.Matches(displayText);
            if (matches.Count == 0)
            {
                var label = new Label(displayText.Trim());
                label.AddToClassList("mui-execution-log-label");
                ApplyLogTypeColor(label, logType);
                row.Add(label);
            }
            else
            {
                var lastEnd = 0;
                foreach (Match match in matches)
                {
                    var prefix = displayText.Substring(lastEnd, match.Index - lastEnd);
                    if (!string.IsNullOrEmpty(prefix))
                    {
                        var prefixLabel = new Label(prefix);
                        prefixLabel.AddToClassList("mui-execution-log-label");
                        ApplyLogTypeColor(prefixLabel, logType);
                        row.Add(prefixLabel);
                    }

                    var content = match.Groups[1].Value;
                    var idMatch = ExecutionLogUtils.s_ObjectReferencePattern.Match(content);
                    if (idMatch.Success && long.TryParse(idMatch.Groups[2].Value, out var instanceId))
                    {
                        var linkText = idMatch.Groups[1].Value;
                        var linkButton = new Button(() => ExecutionLogUtils.PingObjectByInstanceId(instanceId)) { text = linkText };
                        linkButton.AddToClassList("mui-execution-log-object-link");
                        row.Add(linkButton);
                    }
                    else if (ExecutionLogUtils.TryGetRelativeAssetPath(content, out var assetPath))
                    {
                        var linkButton = new Button(() => ExecutionLogUtils.PingAssetAtPath(assetPath)) { text = content };
                        linkButton.AddToClassList("mui-execution-log-object-link");
                        row.Add(linkButton);
                    }
                    else
                    {
                        var textLabel = new Label(content);
                        textLabel.AddToClassList("mui-execution-log-label");
                        textLabel.AddToClassList("mui-execution-log-inline-value");
                        ApplyLogTypeColor(textLabel, logType);
                        row.Add(textLabel);
                    }

                    lastEnd = match.Index + match.Length;
                }

                var suffix = displayText.Substring(lastEnd);
                if (!string.IsNullOrEmpty(suffix))
                {
                    var suffixLabel = new Label(suffix);
                    suffixLabel.AddToClassList("mui-execution-log-label");
                    ApplyLogTypeColor(suffixLabel, logType);
                    row.Add(suffixLabel);
                }
            }

            return row;
        }

        /// <summary>
        /// Populates a container with formatted log rows parsed from a multi-line log string.
        /// </summary>
        public static void PopulateLogContainer(VisualElement container, string logs)
        {
            container.Clear();

            var lines = logs.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var (displayText, logType) = ExecutionLogUtils.ParseLogLine(line);
                container.Add(CreateLogRow(displayText, logType));
            }
        }

        /// <summary>
        /// Applies color styling based on log type.
        /// </summary>
        public static void ApplyLogTypeColor(VisualElement element, LogType logType)
        {
            switch (logType)
            {
                case LogType.Warning:
                    element.style.color = k_WarningColor;
                    break;
                case LogType.Error:
                    element.style.color = k_ErrorColor;
                    break;
            }
        }
    }
}
