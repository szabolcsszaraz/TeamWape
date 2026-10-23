using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction
{
    class UserInteractionBar : ManagedTemplate
    {
        VisualElement m_BarRoot;
        AssistantImage m_TitleIcon;
        VisualElement m_BarHeader;
        Label m_TitleLabel;
        Label m_DetailLabel;
        Label m_CounterLabel;
        Button m_NavPrevButton;
        Button m_NavNextButton;
        VisualElement m_ContentSlot;

        BaseEventSubscriptionTicket m_QueueChangedSubscription;
        UserInteractionQueue m_ExplicitQueue;
        UserInteractionId m_LastDisplayedEntryId;
        InteractionContentView m_CurrentContentView;
        INavigableInteractionView m_CurrentNavigable;

        UserInteractionQueue Queue => m_ExplicitQueue ?? Context?.InteractionQueue;

        public UserInteractionBar()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        public UserInteractionBar(UserInteractionQueue queue)
            : base(AssistantUIConstants.UIModulePath)
        {
            m_ExplicitQueue = queue;
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_BarRoot = view.Q<VisualElement>("userInteractionBarRoot");
            m_TitleIcon = new AssistantImage(view.Q<Image>("titleIconElement"), autoHide: true);
            m_BarHeader = view.Q<VisualElement>(className: "interaction-bar-header");
            m_TitleLabel = view.Q<Label>("titleLabel");
            m_DetailLabel = view.Q<Label>("detailLabel");
            m_CounterLabel = view.Q<Label>("counterLabel");
            m_NavPrevButton = view.SetupButton("navPrevButton", _ => m_CurrentNavigable?.NavigatePrev());
            m_NavNextButton = view.SetupButton("navNextButton", _ => m_CurrentNavigable?.NavigateNext());
            m_ContentSlot = view.Q<VisualElement>("contentSlot");

            m_NavPrevButton.SetDisplay(false);
            m_NavNextButton.SetDisplay(false);

            RegisterAttachEvents(OnAttach, OnDetach);
            RefreshDisplay();
        }

        void OnAttach(AttachToPanelEvent evt)
        {
            m_QueueChangedSubscription = AssistantEvents.Subscribe<EventInteractionQueueChanged>(OnQueueChanged);
            RefreshDisplay();
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            AssistantEvents.Unsubscribe(ref m_QueueChangedSubscription);
        }

        void OnQueueChanged(EventInteractionQueueChanged evt)
        {
            RefreshDisplay();
        }

        void RefreshDisplay()
        {
            var queue = Queue;

            if (m_BarRoot == null || queue == null)
                return;

            if (!queue.HasPending)
            {
                m_BarRoot.SetDisplay(false);
                return;
            }

            m_BarRoot.SetDisplay(true);
            
            var entry = queue.Current;

            m_BarHeader?.SetDisplay(!entry.HideHeader);

            m_TitleLabel.enableRichText = true;
            m_TitleLabel.text = SanitizeSingleLine(entry.TitleOverride ?? entry.Title ?? "");

            m_TitleIcon.SetIconClassName(entry.TitleIcon);

            m_DetailLabel.text = SanitizeSingleLine(entry.Detail ?? "");
            m_DetailLabel.SetDisplay(!string.IsNullOrEmpty(entry.Detail) && !entry.HideHeader);

            if (m_LastDisplayedEntryId != entry.Id)
            {
                m_LastDisplayedEntryId = entry.Id;
                SetContentView(entry);
            }

            RefreshCounter(queue, entry);
        }

        void RefreshCounter(UserInteractionQueue queue, UserInteractionEntry entry)
        {
            if (m_CurrentNavigable != null && m_CurrentNavigable.NavigationCount > 1)
            {
                // Multi-question AskUser: show question progress and nav buttons
                m_CounterLabel.text = $"{m_CurrentNavigable.NavigationIndex + 1} of {m_CurrentNavigable.NavigationCount}";
                m_CounterLabel.SetDisplay(true);
                m_NavPrevButton.SetDisplay(true);
                m_NavNextButton.SetDisplay(true);
                m_NavPrevButton.SetEnabled(m_CurrentNavigable.NavigationIndex > 0);
                m_NavNextButton.SetEnabled(m_CurrentNavigable.NavigationIndex < m_CurrentNavigable.NavigationCount - 1);
            }
            else
            {
                // Single-question AskUser or non-navigable entry: show pending queue count
                m_CounterLabel.text = $"{queue.CurrentIndex} of {queue.Total}";
                m_CounterLabel.SetDisplay(!entry.HideCounter);
                m_NavPrevButton.SetDisplay(false);
                m_NavNextButton.SetDisplay(false);
            }
        }

        void SetContentView(UserInteractionEntry entry)
        {
            if (m_CurrentContentView != null)
                m_CurrentContentView.Completed -= OnContentCompleted;

            if (m_CurrentNavigable != null)
                m_CurrentNavigable.NavigationChanged -= OnNavigationChanged;

            m_ContentSlot.Clear();
            m_CurrentContentView = entry?.ContentView;
            m_CurrentNavigable = m_CurrentContentView as INavigableInteractionView;

            if (m_CurrentNavigable != null)
                m_CurrentNavigable.NavigationChanged += OnNavigationChanged;

            if (m_CurrentContentView != null)
            {
                if (!m_CurrentContentView.IsInitialized)
                    m_CurrentContentView.Initialize(Context);

                m_CurrentContentView.Completed += OnContentCompleted;
                m_ContentSlot.Add(m_CurrentContentView);

                if (m_CurrentContentView is ApprovalInteractionContent approval && entry?.ExpandedContentFactory != null)
                {
                    var expandedTitle = string.IsNullOrEmpty(entry.ExpandedTitle)
                        ? StripRichText(entry.Title) ?? ""
                        : entry.ExpandedTitle;
                    approval.SetExpandedContent(expandedTitle, entry.ExpandedContentFactory);
                }
            }
            else if (entry.CustomContent != null)
            {
                m_ContentSlot.Add(entry.CustomContent);
            }
        }

        static string SanitizeSingleLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            return text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        }

        static string StripRichText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            return System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", string.Empty);
        }

        void OnNavigationChanged()
        {
            var queue = Queue;
            if (queue != null)
                RefreshCounter(queue, queue.Current);
        }

        void OnContentCompleted()
        {
            var queue = Queue;
            if (queue == null || !queue.HasPending)
                return;

            var entry = queue.Current;
            if (entry != null)
                queue.Complete(entry);
        }
    }
}
