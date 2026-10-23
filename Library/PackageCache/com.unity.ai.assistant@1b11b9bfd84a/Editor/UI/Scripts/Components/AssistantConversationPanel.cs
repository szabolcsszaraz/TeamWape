using System;
using System.Linq;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.Checkpoint.Events;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization;
using Unity.AI.Assistant.UI.Editor.Scripts.ConversationSearch;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Utils;
using Unity.AI.Assistant.Utils.Perf;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class AssistantConversationPanel : ManagedTemplate
    {
        VisualElement m_ViewRoot;
        VisualElement m_ConversationRoot;
        IChatMessageView m_ConversationList;

        VisualElement m_OverlayElements;

        Button m_ScrollToBottomButton;

        ResponseFeedbackQueue m_FeedbackQueue;

        BaseEventSubscriptionTicket m_RevertedTimeStampFilterRequestedSubscription;
        BaseEventSubscriptionTicket m_ExpandedViewRequestedSubscription;
        BaseEventSubscriptionTicket m_ExpandedPanelCloseRequestedSubscription;

        AssistantExpandedPanel m_ExpandedPanel;

        // Conversation scroll position captured when the checkpoint panel opens, restored on close.
        bool m_RestoreScrollOnExpandedPanelClose;
        ChatScrollPosition m_SavedScrollPosition;

        // The checkpoint panel's own view, built per open and disposed with the panel: it subscribes to
        // its surface and to the API, and the panel it lives in is discarded on close.
        ChatConversationView m_CheckpointList;

        /// <summary>
        /// Fires once after a <see cref="Populate"/> call, when the conversation is on screen: the
        /// items overlapping the viewport are built, measured and positioned.
        /// </summary>
        public event Action Populated;

        public AssistantConversationPanel() : base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_ViewRoot = view;
            m_OverlayElements = view.Q<VisualElement>("conversationOverlayElements");

            m_ConversationRoot = view.Q<VisualElement>("conversationRoot");
            m_ConversationList = new ChatConversationView();

            m_ConversationList.UserScrolled += UpdateOverlayButtons;
            m_ConversationList.GeometryChanged += UpdateOverlayButtons;
            m_ConversationList.ElementsPopulated += OnConversationElementsPopulated;

            m_ConversationList.Initialize(Context);
            m_ConversationRoot.Add(m_ConversationList.AsVisualElement);

            Context.SearchHelper = new AssistantViewSearchHelper(m_ConversationList, Context);

            m_ScrollToBottomButton = view.SetupButton("conversationScrollToBottomButton", _ =>
            {
                ScrollToBottom();
                AIAssistantAnalytics.ReportUITriggerLocalScrollToBottomEvent();
            });

            Context.ConversationScrollToEndRequested += () => ScrollToBottom(true);

            Context.API.ConversationChanged += OnConversationChanged;

            m_FeedbackQueue = new ResponseFeedbackQueue(Context);
            m_FeedbackQueue.LoadedFeedback += OnFeedbackLoaded;

            m_ExpandedPanel = new AssistantExpandedPanel();
            m_ExpandedPanel.Initialize(Context);
            m_ExpandedPanel.SetDisplay(false);
            m_ViewRoot.Add(m_ExpandedPanel);

            UpdateVisibility();

            RegisterAttachEvents(OnAttach, OnDetach);
        }

        void OnConversationElementsPopulated()
        {
            Populated?.Invoke();
        }

        public void Populate(ConversationModel conversation)
        {
            using var perfScope = AssistantPerf.Measure(PerfProbe.ConversationPopulate);
            var startTicks = AssistantPerf.Now;

            // The list content is changing, so any pending checkpoint scroll restore is now stale.
            m_RestoreScrollOnExpandedPanelClose = false;

            // Set before UI element creation so ChatElementCheckpoint.ValidateCheckpoint()
            // has the correct source of truth regarding current conversation and message ids.
            SetDisplayedConversation(conversation);

            m_ConversationList.BeginUpdate();
            PopulateNormalMode(conversation);
            m_ConversationList.EndUpdate();
            UpdateVisibility();

            AssistantPerf.MarkElapsed("chat.populate", "messages=" + m_ConversationList.Data.Count, startTicks);
        }

        public void ClearConversation()
        {
            // The list content is changing, so any pending checkpoint scroll restore is now stale.
            m_RestoreScrollOnExpandedPanelClose = false;

            m_ConversationList.ClearData();
            m_FeedbackQueue.Clear();

            UpdateVisibility();
            UpdateOverlayButtons();
        }

        void OnAttach(AttachToPanelEvent evt)
        {
            m_RevertedTimeStampFilterRequestedSubscription =
                AssistantEvents.Subscribe<EventRevertedTimeStampFilterRequested>(OnRevertedTimeStampFilterRequested);
            m_ExpandedViewRequestedSubscription =
                AssistantEvents.Subscribe<EventExpandedViewRequested>(OnExpandedViewRequested);
            m_ExpandedPanelCloseRequestedSubscription =
                AssistantEvents.Subscribe<EventExpandedPanelCloseRequested>(_ => CloseExpandedPanel());
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            AssistantEvents.Unsubscribe(ref m_RevertedTimeStampFilterRequestedSubscription);
            AssistantEvents.Unsubscribe(ref m_ExpandedViewRequestedSubscription);
            AssistantEvents.Unsubscribe(ref m_ExpandedPanelCloseRequestedSubscription);
        }

        void OnExpandedViewRequested(EventExpandedViewRequested eventData)
        {
            m_RestoreScrollOnExpandedPanelClose = false;
            ShowExpandedPanel(eventData);
        }

        void OnRevertedTimeStampFilterRequested(EventRevertedTimeStampFilterRequested eventData)
        {
            var conversation = Context.Blackboard.ActiveConversation;
            if (conversation == null)
            {
                InternalLog.LogError("Requested reverted timestamp filter for a conversation that does not exist");
                return;
            }

            var list = new ChatConversationView();
            list.Initialize(Context);

            list.BeginUpdate();
            foreach (var msg in conversation.Messages)
            {
                if (msg.RevertedTimeStamp == eventData.Timestamp)
                    list.AddData(msg);
            }

            // Landed at the tail once and then left alone: a checkpoint is a snapshot, so nothing arrives
            // to follow, and sticking would fight the reader on every pass.
            list.EndUpdate();
            list.LandAtEndOnce();
            list.SetContentEnabled(false);

            // Remember where the conversation was so we can return to it when closing. (UUM-139446)
            var savedScrollPosition = m_ConversationList.CapturePosition();

            var opened = ShowExpandedPanel(
                new EventExpandedViewRequested(
                    "Checkpoint",
                    list.AsVisualElement,
                    fillsPanel: true));

            // Nothing was shown, so nothing here is the panel's to keep: the view built for the refused
            // request owns its surface subscriptions until it is disposed.
            if (!opened)
            {
                list.Dispose();
                return;
            }

            m_CheckpointList?.Dispose();
            m_CheckpointList = list;
            m_SavedScrollPosition = savedScrollPosition;
            m_RestoreScrollOnExpandedPanelClose = true;
        }

        internal void CloseExpandedPanel()
        {
            if (!m_ExpandedPanel.IsVisible)
                return;

            m_ExpandedPanel.HidePanel();
            AssistantEvents.Send(new EventExpandedPanelClosed());

            m_CheckpointList?.Dispose();
            m_CheckpointList = null;

            // Return to where the conversation was when the panel opened. (UUM-139446)
            if (m_RestoreScrollOnExpandedPanelClose)
            {
                m_RestoreScrollOnExpandedPanelClose = false;
                m_ConversationList.RestorePosition(m_SavedScrollPosition);
            }
        }

        /// <summary>Shows the request in the expanded panel. False when one is already open, which leaves
        /// whatever the caller built for the request in the caller's hands.</summary>
        bool ShowExpandedPanel(EventExpandedViewRequested request)
        {
            if (m_ExpandedPanel.IsVisible)
            {
                InternalLog.LogError(
                    "Trying to open an expanded panel on top of an expanded panel should not happen!");
                return false;
            }

            if (request.FillsPanel)
                m_ExpandedPanel.ShowFillPanel(request.ExpandedElement);
            else
                m_ExpandedPanel.ShowPanel(request.ExpandedElement, request.ScrollMode);

            AssistantEvents.Send(new EventExpandedPanelOpened(request.TitleText, request.HeaderActions));
            return true;
        }

        void UpdateVisibility()
        {
            m_ConversationList.AsVisualElement.SetDisplay(m_ConversationList.HasContent);
            m_OverlayElements.SetDisplay(m_ConversationList.HasContent);
        }

        void OnConversationChanged(AssistantConversationId conversationId)
        {
            using var perfScope = AssistantPerf.Measure(PerfProbe.ConversationPanelDiff);

            var conversation = Context.Blackboard.GetConversation(conversationId);
            if (conversation == null)
            {
                return;
            }

            // Keep DisplayedConversation current so checkpoint validation always sees
            // up-to-date message IDs (e.g. Incomplete → External after streaming completes).
            if (Context.DisplayedConversation?.Id == conversationId)
                SetDisplayedConversation(conversation);

            // If the conversationId does not match the current list, clear everything
            if (m_ConversationList.Data.Count > 0 && m_ConversationList.Data[0].Id.ConversationId != conversationId)
            {
                // The list content is changing, so any pending checkpoint scroll restore is now stale.
                m_RestoreScrollOnExpandedPanelClose = false;
                m_ConversationList.ClearData();
            }

            bool scrollToEndRequired = false;
            int searchStartIndex = 0;
            for (var messageIndex = 0; messageIndex < conversation.Messages.Count; messageIndex++)
            {
                var incoming = conversation.Messages[messageIndex];

                // Skip reverted messages in normal mode
                if (incoming.RevertedTimeStamp != 0)
                {
                    continue;
                }

                // Map incoming messages to existing ones
                int incomingMessageIndex = -1;
                for (int i = searchStartIndex; i < m_ConversationList.Data.Count; i++)
                {
                    var existing = m_ConversationList.Data[i];

                    // Local added data/elements are never a match for incoming messages 
                    if (existing.IsInitialCheckpoint || existing.IsRevertedTimeStampLink)
                        continue;

                    if (existing.Id.FragmentId == incoming.Id.FragmentId)
                    {
                        Debug.Assert(existing.Role == incoming.Role);
                        incomingMessageIndex = i;
                        break;
                    }

                    // Handle special cases for internal and incomplete messages
                    if (IsTemporaryMessage(existing, incoming))
                    {
                        incomingMessageIndex = i;
                        break;
                    }
                }

                if (incomingMessageIndex == -1)
                {
                    AddChatMessage(incoming);

                    scrollToEndRequired = true;
                }
                else
                {
                    var localMessage = m_ConversationList.Data[incomingMessageIndex];

                    var messageHasContentUpdate = !incoming.HasEqualContent(localMessage);
                    m_ConversationList.UpdateData(incomingMessageIndex, incoming);
                    if (messageHasContentUpdate)
                    {
                        InternalLog.LogToFile(
                            conversationId.ToString(),
                            ("event", "Updating message in ui because of content change"),
                            ("index", incomingMessageIndex.ToString()),
                            ("total_messages_in_ui_currently", conversation.Messages.Count.ToString())
                        );
                        m_ConversationList.ScrollToEndIfNotLocked();
                    }

                    // Special case where we may need to update an initial checkpoint message ID
                    if (AssistantProjectPreferences.CheckpointEnabled)
                    {
                        RevalidateCheckpoints(conversation, messageIndex, incoming, localMessage);
                    }

                    searchStartIndex = incomingMessageIndex + 1;
                }
            }

            if (scrollToEndRequired)
            {
                m_ConversationList.ScrollToEndIfNotLocked();
                UpdateVisibility();
                UpdateOverlayButtons();
            }
        }

        void AddChatMessage(MessageModel message)
        {
            InternalLog.Log($"MSG_ADD: {message.Id}");

            m_ConversationList.AddData(message);
        }

        void ScrollToBottom(bool scrollIfNotLocked = false)
        {
            if (scrollIfNotLocked)
                m_ConversationList.ScrollToEndIfNotLocked();
            else
                m_ConversationList.ScrollToEnd();

            UpdateOverlayButtons();
        }

        void UpdateOverlayButtons()
        {
            m_ScrollToBottomButton.SetDisplay(m_ConversationList.CanScrollDown);
        }

        void OnFeedbackLoaded(AssistantMessageId id, FeedbackData? feedback)
        {
            var messageIndex = FindMessageIndex(id);
            var message = m_ConversationList.Data[messageIndex];
            message.Feedback = feedback;
            m_ConversationList.UpdateData(messageIndex, message);
            ScrollToBottom(true);
        }

        int FindMessageIndex(AssistantMessageId incomingMessageId)
        {
            var message = m_ConversationList.Data.FirstOrDefault(m =>
                m.Id == incomingMessageId && IsNormalMessage(m));

            if (!message.Id.ConversationId.IsValid)
            {
                return -1;
            }

            return m_ConversationList.Data.IndexOf(message);

            bool IsNormalMessage(MessageModel message)
            {
                return message is { IsInitialCheckpoint: false, IsRevertedTimeStampLink: false };
            }
        }

        void PopulateNormalMode(ConversationModel conversation)
        {
            using var perfScope = AssistantPerf.Measure(PerfProbe.PopulateAddRows);

            // Add initial checkpoint if there are any non-reverted messages
            AddInitialCheckpointIfNeeded(conversation);

            // Normal mode: Show non-reverted messages; inject grouped links per matching reverted timestamp
            long lastSeenRevertedTimeStamp = 0;

            for (var i = 0; i < conversation.Messages.Count; i++)
            {
                var msg = conversation.Messages[i];

                if (msg.RevertedTimeStamp != 0)
                {
                    if (msg.RevertedTimeStamp != lastSeenRevertedTimeStamp)
                    {
                        lastSeenRevertedTimeStamp = msg.RevertedTimeStamp;

                        // Create a synthetic message for the link
                        var linkMessage = new MessageModel
                        {
                            RevertedTimeStamp = msg.RevertedTimeStamp,
                            IsRevertedTimeStampLink = true
                        };
                        m_ConversationList.AddData(linkMessage);
                    }

                    continue;
                }

                m_ConversationList.AddData(msg);
            }

            // Queue feedback refresh for non-reverted assistant messages
            QueueFeedbackRefresh(conversation);
        }

        void AddInitialCheckpointIfNeeded(ConversationModel conversation)
        {
            if (!AssistantProjectPreferences.CheckpointEnabled)
                return;

            // Add a valid checkpoint for the first non-reverted user message
            for (var i = 0; i < conversation.Messages.Count; i++)
            {
                var msg = conversation.Messages[i];
                if (msg.RevertedTimeStamp == 0 && msg.Role == MessageModelRole.User)
                {
                    // Create a synthetic message for the initial checkpoint
                    var checkpointMessage = new MessageModel
                    {
                        Id = msg.Id,
                        IsInitialCheckpoint = true
                    };
                    m_ConversationList.AddData(checkpointMessage);
                    return;
                }
            }

            // Alternatively, add a temporary initial checkpoint; Id gets updated with the next response
            var temporaryCheckpointMessage = new MessageModel
            {
                Id = new AssistantMessageId(conversation.Id, String.Empty, AssistantMessageIdType.Incomplete),
                IsInitialCheckpoint = true
            };
            m_ConversationList.AddData(temporaryCheckpointMessage);
        }

        /// <summary>
        /// Settles the checkpoints against a message that just changed, once that message has reached the
        /// view. A checkpoint reads its validity off the working tree and announces itself to every
        /// element that draws one, so this is disk work and foreign code running inside the diff, on the
        /// arm a turn's completion takes — and that completion is the last event of its turn, so a
        /// failure here that took the diff with it would leave the finished answer showing the chunk
        /// before last with nothing coming back to correct it.
        /// </summary>
        void RevalidateCheckpoints(ConversationModel conversation,
            int messageIndex, MessageModel incoming, MessageModel localMessage)
        {
            try
            {
                UpdateInitialCheckpointIfNeeded(conversation, messageIndex, incoming, localMessage);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        void UpdateInitialCheckpointIfNeeded(ConversationModel conversation,
            int messageIndex, MessageModel incoming, MessageModel localMessage)
        {
            // Check if message ID is transitioning from temporary to external
            var isLocalMessageTemporary = IsTemporaryMessage(localMessage, incoming);
            var isIncomingMessageExternal = incoming.Id.Type == AssistantMessageIdType.External;

            // If not transitioning to a final ID, skip the ID update below.
            if (!isLocalMessageTemporary || !isIncomingMessageExternal)
            {
                // Issue with checkpoint git tag timing: The tag is written after streaming finishes. ValidateCheckpoint() during element initialization
                // may have run before the tag existed. 1. We refresh all checkpoints here, in case the tags exists now.
                // 2. As fallbacks EventCheckpointsChanged triggers validation, in CheckpointSystem.InitializeAsync (domain reload)
                // and CompletePendingCheckpointAsync (when a pending checkpoint tag is finalized with the real message ID).
                if (incoming is { Role: MessageModelRole.Assistant, IsComplete: true } && !localMessage.IsComplete)
                {
                    RevalidateInitialCheckpoint();
                    
                    // Refresh all other checkpoints in feedback sections
                    AssistantEvents.Send(new EventCheckpointsChanged());
                }
                return;
            }

            AssistantMessageId checkpointId = default;
            bool shouldUpdate = false;

            // Update 1: User message ID is final; update initial checkpoint if it was the first non-reverted user message
            if (incoming.Role == MessageModelRole.User)
            {
                if (IsFirstNonRevertedUserMessage(conversation, messageIndex))
                {
                    checkpointId = incoming.Id;
                    shouldUpdate = true;
                }
            }
            // Update 2: Assistant message ID is final; update initial checkpoint if this follows message from update above
            else if (incoming.Role == MessageModelRole.Assistant)
            {
                if (messageIndex > 0 && IsFirstNonRevertedUserMessage(conversation, messageIndex - 1))
                {
                    // Note: We could improve this: We update data providing the User message ID.
                    // The checkpoint logic needs this detected Assistant message's ID anyway, and stores it.
                    checkpointId = conversation.Messages[messageIndex - 1].Id;
                    shouldUpdate = true;
                }
            }

            if (shouldUpdate)
            {
                for (int i = 0; i < m_ConversationList.Data.Count; i++)
                {
                    if (m_ConversationList.Data[i].IsInitialCheckpoint)
                    {
                        var updated = m_ConversationList.Data[i];
                        updated.Id = checkpointId;
                        m_ConversationList.UpdateData(i, updated);
                        break;
                    }
                }
            }
        }

        void RevalidateInitialCheckpoint()
        {
            for (int i = 0; i < m_ConversationList.Data.Count; i++)
            {
                if (m_ConversationList.Data[i].IsInitialCheckpoint)
                {
                    m_ConversationList.RefreshMessage(i);
                    break;
                }
            }
        }

        void QueueFeedbackRefresh(ConversationModel conversation)
        {
            for (var i = 0; i < conversation.Messages.Count; i++)
            {
                var msg = conversation.Messages[i];
                if (msg.RevertedTimeStamp != 0)
                    continue;

                if (msg.Role == MessageModelRole.Assistant)
                {
                    m_FeedbackQueue.QueueRefresh(msg.Id);
                }
            }
        }

        bool IsTemporaryMessage(MessageModel existing, MessageModel incoming)
        {
            if (existing.Role != incoming.Role)
                return false;

            // Match internal user messages
            if (existing is { Role: MessageModelRole.User, Id: { Type: AssistantMessageIdType.Internal } })
            {
                return true;
            }

            // Match incomplete assistant messages
            if (existing is { Role: MessageModelRole.Assistant, Id: { Type: AssistantMessageIdType.Incomplete } })
            {
                return true;
            }

            return false;
        }

        bool IsFirstNonRevertedUserMessage(ConversationModel conversation, int currentIndex)
        {
            for (int i = 0; i < currentIndex; i++)
            {
                var msg = conversation.Messages[i];
                if (msg.RevertedTimeStamp == 0 && msg.Role == MessageModelRole.User)
                {
                    return false;
                }
            }

            return true;
        }

        void SetDisplayedConversation(ConversationModel conversation)
        {
            Context.DisplayedConversation = conversation;
        }
    }
}