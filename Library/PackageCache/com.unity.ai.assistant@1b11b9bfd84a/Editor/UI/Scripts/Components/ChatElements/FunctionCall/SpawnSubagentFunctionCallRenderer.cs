using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Tools.Editor;
using Unity.AI.Assistant.UI.Editor.Scripts.Markup;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    [FunctionCallRenderer("Agent.SpawnSubagent")]
    class SpawnSubagentFunctionCallRenderer : DefaultFunctionCallRenderer, IAssistantUIContextAware
    {
        const string k_TaskParam = "task";
        const string k_RoleParam = "role";

        public AssistantUIContext Context { get; set; }

        public override void OnCallRequest(AssistantFunctionCall functionCall)
        {
            Clear();

            var role = functionCall.Parameters?[k_RoleParam]?.ToString();
            Title = string.IsNullOrEmpty(role) ? "Run Subagent" : $"Run Subagent: {TitleCase(role)}";

            var task = functionCall.Parameters[k_TaskParam]?.ToString();
            TitleDetails = !string.IsNullOrEmpty(task) ? Truncate(task, 80) : functionCall.GetDefaultTitleDetails();

            if (!string.IsNullOrEmpty(task))
            {
                Expanded = true;
                Add(CreateWrappingLabel(task));
            }
        }

        public override void OnCallSuccess(string functionId, Guid callId, FunctionCallResult result)
        {
            Clear();

            string resultText;
            try
            {
                resultText = result.GetTypedResult<string>()?.Trim();
            }
            catch (Exception)
            {
                resultText = result.Result?.ToString();
            }

            if (string.IsNullOrEmpty(resultText))
            {
                return;
            }

            Expanded = true;
            RenderMarkdown(resultText);
        }

        const string k_WrappingLabelClass = "mui-subagent-task-label";

        static Label CreateWrappingLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList(k_WrappingLabelClass);
            return label;
        }

        static string TitleCase(string role)
        {
            var words = role.Replace('_', ' ').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var textInfo = System.Globalization.CultureInfo.InvariantCulture.TextInfo;
            for (int i = 0; i < words.Length; i++)
            {
                words[i] = textInfo.ToTitleCase(words[i].ToLowerInvariant());
            }
            return string.Join(" ", words);
        }

        static string Truncate(string text, int maxLength)
        {
            if (text == null || text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength) + "\u2026";
        }

        void RenderMarkdown(string markdown)
        {
            var elements = new List<VisualElement>();
            MarkdownAPI.MarkupText(Context, markdown, null, elements, null);

            foreach (var element in elements)
            {
                Add(element);
            }
        }
    }
}
