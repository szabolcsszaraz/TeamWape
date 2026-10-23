using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.Tools.Editor
{
    /// <summary>
    /// Tools-side data carrier and completion source for the EnterPlanMode interaction.
    /// Backs a Yes/No approval banner asking the user to confirm the switch into Plan mode. On
    /// approval it returns the strict JSON the backend uses to trigger the swap; on denial it tells
    /// the model to stay in Agent mode. The UI renderer (EnterPlanModeFooterContent) forwards the
    /// button clicks back through <see cref="Approve"/> / <see cref="Deny"/>.
    /// </summary>
    class EnterPlanModeInteraction : IInteractionSource<string>
    {
        public static string Title => "Switch to Plan mode";
        public static string Subtitle => "Plan mode is read-only — the assistant explores and drafts a plan for your approval before making changes.";

        /// <summary>True once the user approved the switch into Plan mode.</summary>
        public bool Approved { get; private set; }

        public event Action<string> OnCompleted;
        public TaskCompletionSource<string> TaskCompletionSource { get; } = new();

        bool m_Completed;

        public void Approve()
        {
            if (m_Completed) return;
            m_Completed = true;
            Approved = true;

            var result = JsonConvert.SerializeObject(new { entered = true });
            SetResult(result);
        }

        public void Deny()
        {
            if (m_Completed) return;
            m_Completed = true;

            var result = JsonConvert.SerializeObject(new
            {
                entered = false,
                message = "The user declined to enter Plan mode and wants to stay in Agent mode. " +
                          "Do not call Unity.EnterPlanMode again for this request; proceed directly in Agent mode."
            });
            SetResult(result);
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
        }
    }
}
