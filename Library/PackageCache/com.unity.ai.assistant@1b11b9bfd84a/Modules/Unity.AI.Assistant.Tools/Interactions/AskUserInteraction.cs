using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.Tools.Editor
{
    [Serializable]
    class AskUserQuestion
    {
        [JsonProperty("question")] public string Question;
        [JsonProperty("header")] public string Header;
        [JsonProperty("type")] public string Type;
        [JsonProperty("options")] public List<AskUserOption> Options;
        [JsonProperty("multiSelect")] public bool MultiSelect;
        [JsonProperty("placeholder")] public string Placeholder;
    }

    [Serializable]
    class AskUserOption
    {
        [JsonProperty("label")] public string Label;
        [JsonProperty("description")] public string Description;
    }

    /// <summary>
    /// Tools-side data carrier and completion source for the AskUser interaction.
    /// The UI-side renderer (AskUserInteractionElement) constructs from this and forwards
    /// user actions back through Complete/CompleteCancelled. JSON wire-format ownership lives
    /// here so the LLM-facing contract stays co-located with the tool that returns it.
    /// </summary>
    class AskUserInteraction : IInteractionSource<string>
    {
        const string k_CancelledMessage = "User dismissed the ask_user dialog without answering. Do not ask these questions again. Proceed with your best judgment or wait for the user to provide new direction.";

        readonly List<AskUserQuestion> m_Questions;

        public IReadOnlyList<AskUserQuestion> Questions => m_Questions;
        public string Title => m_Questions.Count == 1 ? "clarify a question" : $"clarify {m_Questions.Count} questions";

        public event Action<string> OnCompleted;
        public event Action CancelRequested;
        public TaskCompletionSource<string> TaskCompletionSource { get; } = new();

        bool m_Completed;

        public AskUserInteraction(List<AskUserQuestion> questions)
        {
            m_Questions = questions;
        }

        public void Complete(IReadOnlyDictionary<int, string> answers, IReadOnlyCollection<int> skipped, string notes)
        {
            if (m_Completed) return;
            m_Completed = true;

            var result = JsonConvert.SerializeObject(new { answers, skipped, notes });
            SetResult(result);
        }

        public void CompleteCancelled()
        {
            if (m_Completed) return;
            m_Completed = true;

            SetResult(k_CancelledMessage);
        }

        void SetResult(string result)
        {
            TaskCompletionSource.TrySetResult(result);
            OnCompleted?.Invoke(result);
        }

        public void CancelInteraction()
        {
            if (m_Completed) return;
            m_Completed = true;

            TaskCompletionSource.TrySetCanceled();
            CancelRequested?.Invoke();
        }
    }
}
