using System;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class TodoTaskRow : ManagedTemplate
    {
        const string k_PendingIcon = "\u2610";     // ☐
        const string k_CompletedIcon = "\u2713";    // ✓
        const string k_CancelledIcon = "\u2014";    // —

        VisualElement m_SpinnerSlot;
        Label m_IconLabel;
        Label m_DescLabel;
        LoadingSpinner m_Spinner;

        public TodoTaskRow()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_SpinnerSlot = view.Q<VisualElement>("spinnerSlot");
            m_IconLabel = view.Q<Label>("iconLabel");
            m_DescLabel = view.Q<Label>("descLabel");

            m_Spinner = new LoadingSpinner();
            m_SpinnerSlot.Add(m_Spinner);

            m_SpinnerSlot.SetDisplay(false);
            m_IconLabel.SetDisplay(false);
        }

        public void SetData(TodoItem item)
        {
            var status = item.Status?.ToLowerInvariant() ?? "pending";

            if (status == "in_progress")
            {
                m_SpinnerSlot.SetDisplay(true);
                m_IconLabel.SetDisplay(false);
                m_Spinner.Show();
            }
            else
            {
                m_SpinnerSlot.SetDisplay(false);
                m_IconLabel.SetDisplay(true);
                m_IconLabel.text = GetStatusIcon(status);
                m_IconLabel.EnableInClassList("todo-progress-task-icon--completed", status == "completed");
                m_IconLabel.EnableInClassList("todo-progress-task-icon--cancelled", status == "cancelled");
                m_IconLabel.EnableInClassList("todo-progress-task-icon--pending",
                    status != "completed" && status != "cancelled");
            }

            m_DescLabel.text = item.Description ?? "";
            m_DescLabel.EnableInClassList("todo-progress-task-desc--cancelled", status == "cancelled");
            m_DescLabel.EnableInClassList("todo-progress-task-desc--in_progress", status == "in_progress");
        }

        static string GetStatusIcon(string status) => status switch
        {
            "completed" => k_CompletedIcon,
            "cancelled" => k_CancelledIcon,
            _ => k_PendingIcon
        };
    }
}
