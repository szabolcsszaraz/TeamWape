using System;
using System.Globalization;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    /// <summary>
    /// Header row for a subagent in the chat stream.
    /// Displays role badge pill, status spinner/checkmark, and progress counter.
    /// </summary>
    class SubagentHeaderElement : ManagedTemplate
    {
        enum SubagentState { InProgress, Success, Failed }
        const string k_StatusSuccessClass = "mui-function-call-status-success";
        const string k_IconCheckmarkClass = "mui-icon-checkmark";
        const string k_IconTintSuccessClass = "mui-icon-tint-success";
        const string k_StatusFailedClass = "mui-function-call-status-failed";
        const string k_IconCounterClockwiseClass = "mui-icon-arrows-counter-clockwise";
        const string k_IconTintErrorClass = "mui-icon-tint-error";
        internal const string SubagentPrefix = "Subagent-";
        const string k_IconExpandedClass = "mui-icon-arrow-caret-down";
        const string k_IconCollapsedClass = "mui-icon-arrow-caret-right";

        VisualElement m_Chevron;
        Label m_Badge;
        VisualElement m_StatusContainer;
        LoadingSpinner m_LoadingSpinner;
        Label m_Progress;
        VisualElement m_ContentContainer;

        SubagentState m_State;
        bool m_IsExpanded = true;

        public string Agent { get; private set; }

        /// <summary>
        /// True only for backend agent strings that name a subagent (format: "Subagent-{role}-{index}").
        /// CoreAgent and unrecognized formats return false — no header is rendered for those.
        /// </summary>
        public static bool IsSubagent(string agentName)
        {
            return !string.IsNullOrEmpty(agentName) && agentName.StartsWith(SubagentPrefix, StringComparison.Ordinal);
        }

        public SubagentHeaderElement() : base(AssistantUIConstants.UIModulePath)
        {
            SetResourceName("SubagentHeaderElement");
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Chevron = view.Q<VisualElement>("subagentChevron");
            m_Badge = view.Q<Label>("subagentBadge");
            m_StatusContainer = view.Q("subagentStatusContainer");
            m_Progress = view.Q<Label>("subagentProgress");
            m_ContentContainer = view.Q("subagentContent");

            UpdateChevron();

            m_LoadingSpinner = new LoadingSpinner();
            m_StatusContainer.Add(m_LoadingSpinner);
            m_LoadingSpinner.Show();

            view.Q<VisualElement>("subagentHeaderRoot").RegisterCallback<ClickEvent>(OnHeaderClicked);
        }

        void OnHeaderClicked(ClickEvent evt)
        {
            m_IsExpanded = !m_IsExpanded;
            UpdateChevron();
            m_ContentContainer.SetDisplay(m_IsExpanded);
            this.ReportHeightChange(m_IsExpanded);
            evt.StopPropagation();
        }

        /// <summary>
        /// Applies the expansion the flattener computed for a virtualized group, whose calls are laid out
        /// as items of their own. Binding is not a user gesture, so no height change is reported.
        /// </summary>
        public void SetChromeState(bool expanded)
        {
            m_IsExpanded = expanded;
            UpdateChevron();
            m_ContentContainer.SetDisplay(expanded);
        }

        void UpdateChevron()
        {
            m_Chevron.EnableInClassList(k_IconExpandedClass, m_IsExpanded);
            m_Chevron.EnableInClassList(k_IconCollapsedClass, !m_IsExpanded);
        }

        /// <summary>
        /// Sets the agent name and parses it into a display-friendly role badge label.
        /// </summary>
        public void SetAgent(string agentName)
        {
            Agent = agentName;
            m_Badge.text = ParseAgentDisplayName(agentName);
        }

        /// <summary>
        /// Puts a header being bound back in progress, for the bind's own state to settle again. A pooled
        /// header carries the status of the group it showed last, and neither <see cref="UpdateProgress"/>
        /// nor <see cref="TryMarkFailed"/> leaves a settled one; the agent name cannot stand in for the
        /// group either, since its index restarts per conversation and one message can hold two groups of
        /// the same name.
        /// </summary>
        public void ResetState()
        {
            SetState(SubagentState.InProgress);
        }

        /// <summary>Shows how far the group's calls have got, and settles it complete once they all are.</summary>
        /// <param name="canSettleComplete">
        /// False while something outside the group is still to report on it. Its calls read complete in
        /// every window between one of them finishing and the next one arriving, so on their own they
        /// cannot tell a group that is done from one whose subagent is still working.
        /// </param>
        public void UpdateProgress(int completed, int total, bool canSettleComplete = true)
        {
            m_Progress.text = $"{completed} of {total} complete";

            if (m_State == SubagentState.Success && completed < total)
            {
                SetState(SubagentState.InProgress);
            }

            if (m_State != SubagentState.InProgress) return;

            if (canSettleComplete && completed >= total && total > 0)
            {
                SetState(SubagentState.Success);
            }
        }

        void SetState(SubagentState newState)
        {
            if (m_State == newState) return;
            m_State = newState;

            m_LoadingSpinner.Hide();
            m_StatusContainer.RemoveFromClassList(k_StatusSuccessClass);
            m_StatusContainer.RemoveFromClassList(k_IconCheckmarkClass);
            m_StatusContainer.RemoveFromClassList(k_IconTintSuccessClass);
            m_StatusContainer.RemoveFromClassList(k_StatusFailedClass);
            m_StatusContainer.RemoveFromClassList(k_IconCounterClockwiseClass);
            m_StatusContainer.RemoveFromClassList(k_IconTintErrorClass);

            switch (newState)
            {
                case SubagentState.Success:
                    m_StatusContainer.AddToClassList(k_StatusSuccessClass);
                    m_StatusContainer.AddToClassList(k_IconCheckmarkClass);
                    m_StatusContainer.AddToClassList(k_IconTintSuccessClass);
                    break;
                case SubagentState.Failed:
                    m_StatusContainer.AddToClassList(k_StatusFailedClass);
                    m_StatusContainer.AddToClassList(k_IconCounterClockwiseClass);
                    m_StatusContainer.AddToClassList(k_IconTintErrorClass);
                    break;
                case SubagentState.InProgress:
                    m_LoadingSpinner.Show();
                    break;
            }
        }

        public void TryMarkFailed()
        {
            if (m_State != SubagentState.InProgress) return;
            SetState(SubagentState.Failed);
        }

        /// <summary>
        /// Parses an agent string like "Subagent-explorer-1" into "Explorer 1".
        /// Falls back to the raw string if parsing fails.
        /// </summary>
        internal static string ParseAgentDisplayName(string agentName)
        {
            if (string.IsNullOrEmpty(agentName))
                return string.Empty;

            // Expected format: "{Prefix}-{role_with_underscores}-{index}"
            // e.g., "Subagent-explorer-1"
            var parts = agentName.Split('-');
            if (parts.Length < 3)
            {
                return agentName;
            }

            var lastPart = parts[parts.Length - 1];
            bool lastIsIndex = int.TryParse(lastPart, out _);

            int roleStart = 1;
            int roleEnd = lastIsIndex ? parts.Length - 1 : parts.Length;

            if (roleEnd <= roleStart)
            {
                return agentName;
            }

            // Join role segments and split on underscores for title-casing
            var roleRaw = string.Join("-", parts, roleStart, roleEnd - roleStart);
            var roleWords = roleRaw.Replace('_', ' ').Replace('-', ' ').Split(
                new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var textInfo = CultureInfo.InvariantCulture.TextInfo;
            for (int i = 0; i < roleWords.Length; i++)
            {
                roleWords[i] = textInfo.ToTitleCase(roleWords[i].ToLowerInvariant());
            }

            var displayName = string.Join(" ", roleWords);
            if (lastIsIndex)
            {
                displayName += " " + lastPart;
            }

            return displayName;
        }
    }
}
