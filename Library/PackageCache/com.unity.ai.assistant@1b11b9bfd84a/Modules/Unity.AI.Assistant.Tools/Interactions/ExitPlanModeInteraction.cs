using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.FunctionCalling;
using UnityEngine;

namespace Unity.AI.Assistant.Tools.Editor
{
    /// <summary>
    /// Tools-side data carrier and completion source for the ExitPlanMode interaction.
    /// Owns the LLM-facing JSON wire format (Approve/Deny/Revise) and the static plan-file
    /// reader used by ExitPlanModeTools. The UI-side renderer (ExitPlanModeInteractionElement)
    /// constructs from this and forwards user actions back through these methods.
    /// </summary>
    class ExitPlanModeInteraction : IInteractionSource<string>
    {
        public static string Title => "Plan ready to execute";
        public string PlanPath { get; }
        public string PlanContent { get; }
        public string PlanTitle { get; }

        public event Action<string> OnCompleted;
        public event Action CancelRequested;
        public TaskCompletionSource<string> TaskCompletionSource { get; } = new();

        bool m_Completed;

        public ExitPlanModeInteraction(string planPath, string planContent, string title = null)
        {
            PlanPath = planPath ?? string.Empty;
            PlanTitle = string.IsNullOrWhiteSpace(title) ? "Implementation Plan" : title;
            PlanContent = planContent ?? string.Empty;
        }

        public void Approve()
        {
            if (m_Completed) return;
            m_Completed = true;

            const string approvalMode = "agent";
            const string modeDescription = "Agent mode";

            var result = JsonConvert.SerializeObject(new
            {
                approved = true,
                approvalMode,
                message = $"Plan approved. Switching to {modeDescription}.\n\n" +
                          $"The approved implementation plan is stored at: {PlanPath}\n" +
                          "Stop here — do not call any more tools or attempt implementation.\n" +
                          "Ask the user if they want to execute the entire plan. If yes, " +
                          "read and follow the plan strictly during implementation."
            });

            SetResult(result);
        }

        public void Deny()
        {
            if (m_Completed) return;
            m_Completed = true;

            var result = JsonConvert.SerializeObject(new
            {
                approved = false,
                message = "The user chose not to proceed with this plan. Treat this as a hard stop — do not revise, re-present, or re-attempt planning unless the user explicitly asks. Wait for the user to provide new direction."
            });
            SetResult(result);
        }

        public void Revise(string feedback)
        {
            if (m_Completed) return;
            m_Completed = true;

            var result = JsonConvert.SerializeObject(new
            {
                approved = false,
                revise = true,
                feedback,
                message = "The user wants to revise the plan with the feedback above. " +
                          $"Update the plan file at {PlanPath} accordingly, then call Unity.ExitPlanMode again with the revised plan."
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
            CancelRequested?.Invoke();
        }

        /// <summary>
        /// Reads a plan file from disk, trying both the raw path and a project-relative resolution.
        /// </summary>
        [ToolPermissionIgnore]
        internal static string ReadPlanFile(string planPath)
        {
            if (string.IsNullOrEmpty(planPath))
                return "(No plan path provided)";

            try
            {
                var fullPath = Path.GetFullPath(planPath);
                var fullDataPath = Path.GetFullPath(Application.dataPath);

                if (!fullPath.StartsWith(fullDataPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && !fullPath.Equals(fullDataPath, StringComparison.OrdinalIgnoreCase))
                    return "(Plan file outside project's Assets directory)";

                return File.Exists(fullPath) ? File.ReadAllText(fullPath) : $"(Plan file not found: {planPath})";
            }
            catch (Exception e)
            {
                return $"(Error reading plan file: {e.Message})";
            }
        }
    }
}
