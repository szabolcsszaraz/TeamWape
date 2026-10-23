using System;
using System.Collections.Generic;
using System.Threading;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Tools.Editor;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using Unity.AI.Assistant.UI.Editor.Scripts.Markup;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ExitPlanModeInteractionElement : InteractionContentView
    {
        public string Title => ExitPlanModeInteraction.Title;
        public event Action<bool> ExpandedStateChanged;

        readonly ExitPlanModeInteraction m_Interaction;

        public string PlanPath => m_Interaction.PlanPath;
        public string PlanContent => m_Interaction.PlanContent;
        public string PlanTitle => m_Interaction.PlanTitle;
        public IInteractionSource<string> Interaction => m_Interaction;

        const string k_CopyIconClass = "mui-icon-copy";
        const string k_CheckmarkIconClass = "mui-icon-checkmark";

        bool m_Completed;
        bool m_ExpandedPanelOpen;
        bool m_RestoreExpandedRequested;
        Button m_CopyIconButton;
        Image m_CopyIconImage;
        CancellationTokenSource m_CopyActiveTokenSource;
        Label m_StatusLabel;
        PlanReviewHeaderActions m_HeaderActions;
        BaseEventSubscriptionTicket m_ExpandedPanelOpenedSubscription;
        BaseEventSubscriptionTicket m_ExpandedPanelClosedSubscription;

        public ExitPlanModeInteractionElement(ExitPlanModeInteraction interaction)
        {
            m_Interaction = interaction;
        }

        protected override void InitializeView(TemplateContainer view)
        {
            view.Q<Label>("planTitleLabel").text = m_Interaction.PlanTitle;

            var pathLabel = view.Q<Label>("pathLabel");
            pathLabel.text = m_Interaction.PlanPath;

            var scrollView = view.Q<ScrollView>("planContentScroll");
            var markdownElements = new List<VisualElement>();
            MarkdownAPI.MarkupText(Context, m_Interaction.PlanContent, null, markdownElements, null);
            foreach (var el in markdownElements)
                scrollView.Add(el);

            m_CopyIconButton = view.SetupButton("planCopyButton", _ => OnCopyClicked());
            m_CopyIconImage = view.Q<Image>("planCopyIcon");
            view.SetupButton("expandButton", _ => OnExpandClicked());

            m_StatusLabel = view.Q<Label>("statusLabel");
            m_StatusLabel.SetDisplay(false);

            RegisterAttachEvents(OnAttach, OnDetach);
        }

        void OnAttach(AttachToPanelEvent evt)
        {
            m_ExpandedPanelOpenedSubscription = AssistantEvents.Subscribe<EventExpandedPanelOpened>(OnExpandedPanelOpened);
            m_ExpandedPanelClosedSubscription = AssistantEvents.Subscribe<EventExpandedPanelClosed>(OnExpandedPanelClosed);
            m_Interaction.CancelRequested += OnInteractionCancelled;

            if (m_RestoreExpandedRequested)
            {
                m_RestoreExpandedRequested = false;
                if (!m_ExpandedPanelOpen && !m_Completed)
                    RequestExpand();
            }
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            AssistantEvents.Unsubscribe(ref m_ExpandedPanelOpenedSubscription);
            AssistantEvents.Unsubscribe(ref m_ExpandedPanelClosedSubscription);
            m_Interaction.CancelRequested -= OnInteractionCancelled;

            m_CopyActiveTokenSource?.Cancel();
            m_CopyActiveTokenSource?.Dispose();
            m_CopyActiveTokenSource = null;
        }

        void OnExpandedPanelOpened(EventExpandedPanelOpened evt)
        {
            if (m_HeaderActions != null)
            {
                m_ExpandedPanelOpen = true;
                ExpandedStateChanged?.Invoke(true);
            }
        }

        void OnExpandedPanelClosed(EventExpandedPanelClosed evt)
        {
            // Gate on m_HeaderActions so a different element's expanded panel closing does not
            // overwrite our persisted expanded=true with false.
            if (m_HeaderActions == null) return;

            m_ExpandedPanelOpen = false;
            ExpandedStateChanged?.Invoke(false);
            m_HeaderActions.DenyClicked -= OnDeny;
            m_HeaderActions.ApproveClicked -= OnApprove;
            m_HeaderActions = null;
        }

        void OnCopyClicked()
        {
            GUIUtility.systemCopyBuffer = m_Interaction.PlanContent;
            m_CopyIconImage.RemoveFromClassList(k_CopyIconClass);
            m_CopyIconImage.AddToClassList(k_CheckmarkIconClass);
            m_CopyIconButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, true);
            TimerUtils.DelayedAction(ref m_CopyActiveTokenSource, () =>
            {
                m_CopyIconButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, false);
                m_CopyIconImage.RemoveFromClassList(k_CheckmarkIconClass);
                m_CopyIconImage.AddToClassList(k_CopyIconClass);
            });
        }

        void OnExpandClicked() => RequestExpand();

        void RequestExpand()
        {
            if (m_Completed || m_ExpandedPanelOpen)
                return;

            var expandedContent = new PlanReviewExpandedContent(m_Interaction.PlanContent);
            expandedContent.Initialize(Context);

            m_HeaderActions = new PlanReviewHeaderActions(m_Interaction.PlanContent);
            m_HeaderActions.Initialize(Context);
            m_HeaderActions.DenyClicked += OnDeny;
            m_HeaderActions.ApproveClicked += OnApprove;

            AssistantEvents.Send(new EventExpandedViewRequested(
                m_Interaction.PlanTitle,
                expandedContent,
                m_HeaderActions,
                ScrollViewMode.Vertical));
        }

        internal void MarkRestoreExpanded() => m_RestoreExpandedRequested = true;

        internal void OnApprove()
        {
            if (m_Completed)
                return;
            m_Completed = true;

            ShowCompletionState("✓ Plan approved");

            AIAssistantAnalytics.ReportUITriggerLocalPlanReviewApprovedEvent(Context.Blackboard.ActiveConversationId, m_Interaction.PlanPath);

            m_Interaction.Approve();
            InvokeCompleted();
            AssistantEvents.Send(new EventExpandedPanelCloseRequested());
        }

        internal void OnDeny()
        {
            if (m_Completed)
                return;
            m_Completed = true;

            ShowCompletionState("Plan denied");

            AIAssistantAnalytics.ReportUITriggerLocalPlanReviewDeniedEvent(Context.Blackboard.ActiveConversationId, m_Interaction.PlanPath);

            m_Interaction.Deny();
            InvokeCompleted();
            AssistantEvents.Send(new EventExpandedPanelCloseRequested());
        }

        internal void OnRevise(string feedback)
        {
            if (m_Completed) return;
            m_Completed = true;

            ShowCompletionState("Plan revision requested");

            m_Interaction.Revise(feedback);
            InvokeCompleted();
            AssistantEvents.Send(new EventExpandedPanelCloseRequested());
        }

        void ShowCompletionState(string statusText)
        {
            m_StatusLabel.text = statusText;
            m_StatusLabel.SetDisplay(true);
        }

        void OnInteractionCancelled()
        {
            if (m_Completed) return;
            m_Completed = true;

            InvokeCompleted();
            AssistantEvents.Send(new EventExpandedPanelCloseRequested());
        }
    }
}
