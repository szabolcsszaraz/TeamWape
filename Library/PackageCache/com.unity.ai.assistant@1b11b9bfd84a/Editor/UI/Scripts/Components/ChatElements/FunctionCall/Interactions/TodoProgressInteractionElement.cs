using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class TodoProgressInteractionElement : InteractionContentView
    {
        const string k_MinimizeIconClass = "mui-icon-caret-up-down-minimize";
        const string k_InProgressStatus = "in_progress";

        string m_PlanSubtitle;

        Label m_SubtitleLabel;
        Label m_CurrentStepLabel;
        VisualElement m_SpinnerSlot;
        VisualElement m_CurrentStepRow;
        ScrollView m_TaskListScroll;
        VisualElement m_TaskList;
        Button m_ExpandButton;
        VisualElement m_ExpandIcon;
        LoadingSpinner m_Spinner;

        bool m_Expanded;
        List<TodoItem> m_CurrentItems;

        public bool IsExpanded => m_Expanded;
        public List<TodoItem> CurrentItems => m_CurrentItems;
        public string PlanPath { get; private set; }
        public event Action<bool> ExpandedChanged;

        public TodoProgressInteractionElement(string planPath, bool expanded)
        {
            PlanPath = planPath;
            m_PlanSubtitle = GetPlanDisplayName(planPath);
            m_Expanded = expanded;
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_SubtitleLabel = view.Q<Label>("subtitleLabel");
            m_CurrentStepLabel = view.Q<Label>("currentStepLabel");
            m_SpinnerSlot = view.Q<VisualElement>("spinnerSlot");
            m_CurrentStepRow = view.Q<VisualElement>("currentStepRow");
            m_TaskListScroll = view.Q<ScrollView>("taskListScroll");
            m_TaskListScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            m_TaskListScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            m_TaskList = view.Q<VisualElement>("taskList");
            m_ExpandButton = view.SetupButton("expandButton", _ => ToggleExpanded());
            m_ExpandIcon = view.Q<VisualElement>("expandIcon");

            UpdateSubtitleLabel();

            m_Spinner = new LoadingSpinner();
            m_SpinnerSlot.Add(m_Spinner);

            ApplyExpandedState();

            if (m_CurrentItems != null)
                RefreshView();
        }

        public void UpdateTodos(List<TodoItem> items, string planPath)
        {
            m_CurrentItems = items;
            PlanPath = planPath;
            m_PlanSubtitle = GetPlanDisplayName(planPath);

            if (IsInitialized)
            {
                UpdateSubtitleLabel();
                RefreshView();
            }
        }

        void RefreshView()
        {
            if (m_CurrentItems == null || m_CurrentItems.Count == 0)
                return;

            var inProgress = m_CurrentItems.FirstOrDefault(t =>
                string.Equals(t.Status, k_InProgressStatus, StringComparison.OrdinalIgnoreCase));

            // Current step row — only visible when collapsed (expanded task list already shows it)
            if (inProgress != null)
            {
                m_CurrentStepLabel.text = inProgress.Description ?? string.Empty;
                m_Spinner.Show();
            }
            else
            {
                m_Spinner.Hide();
            }
            ApplyExpandedState();

            // Rebuild task list
            m_TaskList.Clear();
            foreach (var item in m_CurrentItems)
            {
                var row = new TodoTaskRow();
                row.Initialize(Context);
                row.SetData(item);
                m_TaskList.Add(row);
            }

            // All items terminal — plan is done
            if (m_CurrentItems.All(t =>
                string.Equals(t.Status, "completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.Status, "cancelled", StringComparison.OrdinalIgnoreCase)))
            {
                InvokeCompleted();
            }
        }

        void ToggleExpanded()
        {
            m_Expanded = !m_Expanded;
            ApplyExpandedState();
            ExpandedChanged?.Invoke(m_Expanded);
        }

        void UpdateSubtitleLabel()
        {
            m_SubtitleLabel.text = m_PlanSubtitle;
            m_SubtitleLabel.SetDisplay(!string.IsNullOrEmpty(m_PlanSubtitle));
        }

        void ApplyExpandedState()
        {
            m_TaskListScroll.SetDisplay(m_Expanded);
            // Current step row is a collapsed summary; only show it when collapsed and a task is in progress
            m_CurrentStepRow.SetDisplay(!m_Expanded && m_CurrentItems?.Any(t =>
                string.Equals(t.Status, k_InProgressStatus, StringComparison.OrdinalIgnoreCase)) == true);

            m_ExpandIcon.EnableInClassList(k_MinimizeIconClass, m_Expanded);
        }

        // Strip directory components from LLM-provided path before display.
        static string GetPlanDisplayName(string planPath)
        {
            if (string.IsNullOrEmpty(planPath)) return string.Empty;
            return Path.GetFileNameWithoutExtension(planPath);
        }
    }
}
