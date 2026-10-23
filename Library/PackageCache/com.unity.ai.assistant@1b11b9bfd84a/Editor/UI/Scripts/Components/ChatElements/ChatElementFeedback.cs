using System.Collections.Generic;
using System.Threading;
using Unity.AI.Assistant.ApplicationModels;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Utils;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using TextField = UnityEngine.UIElements.TextField;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    [UxmlElement]
    partial class ChatElementFeedback : ManagedTemplate
    {
        const string k_FeedbackButtonActiveClass = "mui-feedback-button-active";
        const string k_FeedbackStatusSuccessClass = "mui-feedback-status-success";
        const string k_FeedbackStatusErrorClass = "mui-feedback-status-error";
        const string k_FeedbackStatusCheckmarkClass = "mui-icon-checkmark-green";
        const string k_FeedbackStatusErrorIconClass = "mui-icon-feedback-error";

        CancellationTokenSource m_ResponseCopyButtonActiveTokenSource;

        VisualElement m_OptionsSection;
        VisualElement m_FeedbackParamSection;
        VisualElement m_FeedbackPanel;

        Button m_CopyButton;
        AssistantImage m_CopyButtonImage;
        Button m_UpVoteButton;
        Button m_DownVoteButton;
        Label m_MessageCostLabel;

        TextField m_FeedbackText;
        VisualElement m_FeedbackPlaceholderContent;
        Label m_FeedbackPlaceholder;
        bool m_FeedbackTextFocused;

        Foldout m_ContactFoldout;
        TextField m_FeedbackEmailText;
        Toggle m_FeedbackConsentCheckbox;

        Button m_FeedbackSendButton;
        Label m_FeedbackSendButtonLabel;

        Label m_ReportUnacceptableContentLink;
        VisualElement m_FeedbackStatusRow;
        Image m_FeedbackStatusIcon;
        Label m_FeedbackStatusText;
        Button m_FeedbackStatusAction;

        FeedbackEditMode m_FeedbackMode = FeedbackEditMode.None;

        ChatElementCheckpoint m_Checkpoint;

        AssistantMessageId m_MessageId;

        static AssistantConversationId s_CurrentStoredConversationFeedbacks;
        static readonly Dictionary<AssistantMessageId, FeedbackData> k_StoredFeedbackUIState = new();
        static readonly Dictionary<AssistantMessageId, FeedbackDraft> k_ParkedFeedbackDrafts = new();

        MessageModel m_Message;

        bool m_MessageCostRequested;
        bool m_IsCommentSendPending;
        AssistantMessageId m_PendingCommentMessageId;

        enum FeedbackEditMode
        {
            None,
            UpVote,
            DownVote
        }

        /// <summary>What the user had typed into the feedback panel of one message and not sent yet.
        /// The element is pooled, so the draft outlives the binding it was typed on rather than the
        /// element, and it is only ever restored onto the message it was parked for.</summary>
        readonly struct FeedbackDraft
        {
            public FeedbackDraft(string comment, string email, bool consent, FeedbackEditMode mode)
            {
                Comment = comment;
                Email = email;
                Consent = consent;
                Mode = mode;
            }

            public string Comment { get; }

            public string Email { get; }

            public bool Consent { get; }

            public FeedbackEditMode Mode { get; }
        }

        public ChatElementFeedback() : base(AssistantUIConstants.UIModulePath) { }

        protected override void InitializeView(TemplateContainer view)
        {
            m_OptionsSection = view.Q("optionsSection");
            m_OptionsSection.SetDisplay(false);
            m_CopyButton = view.SetupButton("copyButton", OnCopyClicked);
            m_CopyButtonImage = m_CopyButton.SetupImage("copyButtonImage", "copy");
            m_UpVoteButton = view.SetupButton("upVoteButton", OnUpvoteClicked);
            m_DownVoteButton = view.SetupButton("downVoteButton", OnDownvoteClicked);
            m_MessageCostLabel = view.Q<Label>("messageCostLabel");
            m_MessageCostLabel.SetDisplay(false);

            m_FeedbackParamSection = view.Q("feedbackParamSection");

            m_FeedbackPanel = view.Q("feedbackPanel");
            m_FeedbackPanel.SetDisplay(false);

            view.Q<Label>("feedbackPanelTitle").text = AssistantUIConstants.FeedbackPanelTitle;

            m_FeedbackPlaceholderContent = view.Q("placeholderContent");
            m_FeedbackPlaceholder = view.Q<Label>("placeholderText");
            m_FeedbackText = view.Q<TextField>("feedbackValueText");
            m_FeedbackText.multiline = true;
            m_FeedbackText.maxLength = AssistantMessageSizeConstraints.PromptLimit;
            m_FeedbackText.RegisterValueChangedCallback(_ => CheckFeedbackState());
            m_FeedbackText.RegisterCallback<FocusInEvent>(_ => SetFeedbackTextFocused(true));
            m_FeedbackText.RegisterCallback<FocusOutEvent>(_ => SetFeedbackTextFocused(false));
            m_FeedbackPlaceholderContent.RegisterCallback<ClickEvent>(_ => m_FeedbackText.Focus());

            m_ReportUnacceptableContentLink = view.Q<Label>("reportUnacceptableContentLink");
            m_ReportUnacceptableContentLink.text = AssistantUIConstants.ReportContentRichText;
            m_ReportUnacceptableContentLink.RegisterCallback<PointerDownLinkTagEvent>(evt =>
            {
                switch (evt.linkID)
                {
                    case AssistantUIConstants.ReportContentLinkIdReport:
                        Application.OpenURL(AssistantUIConstants.ReportUnacceptableContentUrl);
                        break;
                    case AssistantUIConstants.ReportContentLinkIdLearn:
                        Application.OpenURL(AssistantUIConstants.ContentTransparencyPolicyUrl);
                        break;
                }
            });
            m_ReportUnacceptableContentLink.RegisterCallback<PointerOverLinkTagEvent>(_ => m_ReportUnacceptableContentLink.AddToClassList(AssistantUIConstants.RichTextLinkHoverClass));
            m_ReportUnacceptableContentLink.RegisterCallback<PointerOutLinkTagEvent>(_ => m_ReportUnacceptableContentLink.RemoveFromClassList(AssistantUIConstants.RichTextLinkHoverClass));

            m_ContactFoldout = view.Q<Foldout>("contactFoldout");
            m_ContactFoldout.value = false;
            m_ContactFoldout.text = AssistantUIConstants.FeedbackContactFoldoutTitle;
            m_ContactFoldout.RegisterValueChangedCallback(_ =>
            {
                Context.SendScrollToEndRequest();
                this.ReportHeightChange();
            });

            m_FeedbackEmailText = view.Q<TextField>("feedbackEmailText");
            m_FeedbackEmailText.RegisterValueChangedCallback(_ => CheckFeedbackState());

            m_FeedbackConsentCheckbox = view.Q<Toggle>("feedbackConsentCheckbox");
            m_FeedbackConsentCheckbox.RegisterValueChangedCallback(_ => CheckFeedbackState());
            var consentLabel = view.Q<Label>("feedbackConsentLabel");
            consentLabel.enableRichText = true;
            consentLabel.text = AssistantUIConstants.FeedbackConsentText;
            consentLabel.RegisterCallback<PointerDownLinkTagEvent>(OnConsentLinkClicked);
            consentLabel.RegisterCallback<PointerOverLinkTagEvent>(_ => consentLabel.AddToClassList(AssistantUIConstants.RichTextLinkHoverClass));
            consentLabel.RegisterCallback<PointerOutLinkTagEvent>(_ => consentLabel.RemoveFromClassList(AssistantUIConstants.RichTextLinkHoverClass));

            m_FeedbackSendButton = view.SetupButton("feedbackSendButton", OnSendFeedback);
            m_FeedbackSendButtonLabel = m_FeedbackSendButton.Q<Label>();

            m_FeedbackStatusRow = view.Q("feedbackStatusRow");
            m_FeedbackStatusRow.SetDisplay(false);
            m_FeedbackStatusIcon = view.Q<Image>("feedbackStatusIcon");
            m_FeedbackStatusText = view.Q<Label>("feedbackStatusText");
            m_FeedbackStatusAction = view.SetupButton("feedbackStatusAction", OnSendAnotherComment);
            m_FeedbackStatusAction.text = AssistantUIConstants.FeedbackAddAnotherCommentTitle;
            m_FeedbackStatusAction.SetDisplay(false);

            m_Checkpoint = view.Q<ChatElementCheckpoint>();
            m_Checkpoint.Initialize(Context);

            RegisterAttachEvents(OnAttachToPanel, OnDetachFromPanel);

            CheckFeedbackState();
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            Context.API.FeedbackSent -= OnFeedbackSent;
            Context.API.FeedbackSent += OnFeedbackSent;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            DisposeFeedbackResources();
        }

        public void SetData(MessageModel message)
        {
            var previousMessageId = m_MessageId;
            var messageChanged = previousMessageId != message.Id;

            // Before the conversation store is switched, so a draft typed in the outgoing conversation
            // is dropped with the rest of that conversation's state instead of outliving it.
            if (messageChanged)
                ParkDraft(previousMessageId);

            m_MessageId = message.Id;
            m_Message = message;

            SetCurrentConversation(message.Id.ConversationId);

            m_FeedbackMode = FeedbackEditMode.None;

            // Preserve pending send state when this same message is rebound,
            // otherwise the completion event can be ignored and the send button stays disabled.
            if (messageChanged)
            {
                m_IsCommentSendPending = false;
                m_PendingCommentMessageId = AssistantMessageId.Invalid;

                // An unsent comment belongs to the message it was typed on, and this element is pooled.
                m_FeedbackText.value = string.Empty;
                m_FeedbackEmailText.value = string.Empty;
                m_FeedbackConsentCheckbox.value = false;

                Context.API.MessageCostReceived -= OnMessageCostReceived;
                m_MessageCostRequested = false;
                m_MessageCostLabel.text = string.Empty;
                m_MessageCostLabel.SetDisplay(false);
            }

            RefreshFeedbackParameters();
            ClearFeedbackStatus();

            if (message.Feedback != null)
            {
                // Feedback returned from backend
                SetFeedback(message.Id, message.Feedback);
                StoreFeedbackUIState(message.Id, message.Feedback.Value);
            }
            else if (k_StoredFeedbackUIState.TryGetValue(m_Message.Id, out var feedbackData))
            {
                // Feedback cached for current conversation
                SetFeedback(message.Id, feedbackData);
            }

            // After the stored feedback, which clears the comment box to show what was already sent.
            if (messageChanged && k_ParkedFeedbackDrafts.TryGetValue(message.Id, out var draft))
                RestoreDraft(draft);

            // Check if last block is a complete response to show feedback options and it's not an error
            var endsOnAnAnswer = TurnIsOver(message) && message.Blocks is { Count: > 0 } && message.Blocks[^1] is AnswerBlockModel;

            // Written on every bind, not only on the bind that earns it: the element is pooled, so the
            // row a previous message was offered would otherwise stand over one that has no answer yet.
            m_OptionsSection.SetDisplay(endsOnAnAnswer);

            if (endsOnAnAnswer)
            {
                // Hide vote buttons for non-Unity providers (they don't support feedback)
                bool isUnitySession = Context.IsUnityProvider;
                m_UpVoteButton.SetDisplay(isUnitySession);
                m_DownVoteButton.SetDisplay(isUnitySession);

                if (!m_MessageCostRequested)
                {
                    // Fetch message cost
                    if (m_MessageId != AssistantMessageId.Invalid)
                    {
                        Context.API.MessageCostReceived += OnMessageCostReceived;
                        Context.API.FetchMessageCost(m_MessageId);
                        m_MessageCostRequested = true;
                    }
                }
            }
            m_Checkpoint.SetCheckpointData(message.Id, 1);
        }

        /// <summary>
        /// Whether nothing is still coming for the message. Its completion is stamped by the terminal
        /// fragment of its turn, which a turn does not always end on, so the API's working flag going
        /// down — the one signal every ending raises — is what the row falls back on.
        /// </summary>
        bool TurnIsOver(MessageModel message) => message.IsComplete || !Context.Blackboard.IsAPIWorking;

        void OnMessageCostReceived(AssistantMessageId id, int? cost)
        {
            if (id != m_MessageId) return;

            if (cost.HasValue && m_MessageCostLabel != null)
            {
                m_MessageCostLabel.text = $"• {cost.Value} cr";
                m_MessageCostLabel.SetDisplay(true);
            }
            Context.API.MessageCostReceived -= OnMessageCostReceived;
        }

        void SetFeedbackTextFocused(bool state)
        {
            m_FeedbackTextFocused = state;

            CheckFeedbackState();
        }

        void CheckFeedbackState()
        {
            bool canSend = !m_IsCommentSendPending
                && !string.IsNullOrEmpty(m_FeedbackText.value)
                && (!m_FeedbackConsentCheckbox.value || !string.IsNullOrWhiteSpace(m_FeedbackEmailText.value));
            m_FeedbackSendButton.SetEnabled(canSend);
            m_FeedbackPlaceholderContent.SetDisplay(!m_FeedbackTextFocused && string.IsNullOrEmpty(m_FeedbackText.value));
        }

        void OnSendFeedback(PointerUpEvent evt)
        {
            if (m_IsCommentSendPending)
                return;

            if (string.IsNullOrEmpty(m_FeedbackText.value))
            {
                ErrorHandlingUtils.ShowGeneralError($"Failed to send Feedback: 'your feedback' section is empty");
                return;
            }

            if (m_FeedbackMode != FeedbackEditMode.DownVote && m_FeedbackMode != FeedbackEditMode.UpVote)
            {
                ErrorHandlingUtils.ShowGeneralError($"Failed to send Feedback: Sentiment must be set");
                return;
            }

            string message = m_FeedbackText.value.Trim();

            if (m_FeedbackConsentCheckbox.value && !string.IsNullOrEmpty(m_FeedbackEmailText.value))
            {
                message += $" (Contact email: {m_FeedbackEmailText.value.Trim()})";
            }

            m_FeedbackSendButton.SetEnabled(false);
            m_FeedbackSendButtonLabel.text = AssistantUIConstants.FeedbackSendingTitle;
            ClearFeedbackStatus();
            m_IsCommentSendPending = true;
            m_PendingCommentMessageId = m_MessageId;
            Context.API.SendFeedback(m_MessageId, false, message, m_FeedbackMode == FeedbackEditMode.UpVote);
        }

        void ClearFeedbackParameters()
        {
            m_FeedbackText.value = string.Empty;
            m_FeedbackConsentCheckbox.value = false;
            RefreshFeedbackParameters();
        }

        void OnDownvoteClicked(PointerUpEvent evt)
        {
            if (m_FeedbackMode == FeedbackEditMode.DownVote)
            {
                return;
            }

            ClearFeedbackStatus();
            m_FeedbackPlaceholder.text = AssistantUIConstants.FeedbackDownVotePlaceholder;

            Context.API.SendFeedback(m_MessageId, false, string.Empty, false);

            var newFeedbackData = new FeedbackData(Sentiment.Negative, m_FeedbackText.value);
            StoreFeedbackUIState(m_Message.Id, newFeedbackData);

            m_FeedbackMode = FeedbackEditMode.DownVote;
            RefreshFeedbackParameters();
        }

        void OnUpvoteClicked(PointerUpEvent evt)
        {
            if (m_FeedbackMode == FeedbackEditMode.UpVote)
            {
                return;
            }

            ClearFeedbackStatus();
            m_FeedbackPlaceholder.text = AssistantUIConstants.FeedbackUpVotePlaceholder;

            Context.API.SendFeedback(m_MessageId, false, string.Empty, true);

            var newFeedbackData = new FeedbackData(Sentiment.Positive, m_FeedbackText.value);
            StoreFeedbackUIState(m_Message.Id, newFeedbackData);

            m_FeedbackMode = FeedbackEditMode.UpVote;
            RefreshFeedbackParameters();
        }

        void OnCopyClicked(PointerUpEvent evt)
        {
            // Format message with footnotes (indices to sources)
            IList<SourceBlock> sourceBlocks = new List<SourceBlock>();

            var outMessage = string.Empty;
            foreach (var block in m_Message.Blocks)
            {
                if (block is not AnswerBlockModel responseBlockModel)
                    continue;

                MessageUtils.ProcessContent(responseBlockModel.Content, responseBlockModel.IsComplete, ref sourceBlocks, out var outBlockMessage, MessageUtils.FootnoteFormat.SimpleIndexForClipboard);
                outMessage += outBlockMessage;
            }

            // Add sources in same order of footnote indices
            MessageUtils.AppendSourceBlocks(sourceBlocks, ref outMessage);

            GUIUtility.systemCopyBuffer = string.Concat(AssistantConstants.GetDisclaimerHeader(), outMessage);

            m_CopyButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, true);
            m_CopyButtonImage.SetOverrideIconClass("checkmark");
            TimerUtils.DelayedAction(ref m_ResponseCopyButtonActiveTokenSource, () =>
            {
                m_CopyButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, false);
                m_CopyButtonImage.SetOverrideIconClass(null);
            });

            AIAssistantAnalytics.ReportUITriggerLocalCopyResponseEvent(m_MessageId, outMessage);
        }

        void RefreshFeedbackParameters(bool initialLoadedState = false)
        {
            if (m_Message.Role == MessageModelRole.Error || m_Message.Role == MessageModelRole.Info || !TurnIsOver(m_Message))
            {
                m_CopyButton.SetEnabled(false);
                m_UpVoteButton.SetEnabled(false);
                m_DownVoteButton.SetEnabled(false);
                m_FeedbackParamSection.SetDisplay(false);
                ClearFeedbackStatus();
                return;
            }

            m_CopyButton.SetEnabled(true);
            m_UpVoteButton.SetEnabled(true);
            m_DownVoteButton.SetEnabled(true);

            switch (m_FeedbackMode)
            {
                case FeedbackEditMode.None:
                {
                    m_FeedbackParamSection.SetDisplay(false);
                    m_UpVoteButton.RemoveFromClassList(k_FeedbackButtonActiveClass);
                    m_DownVoteButton.RemoveFromClassList(k_FeedbackButtonActiveClass);
                    ClearFeedbackStatus();
                    return;
                }

                case FeedbackEditMode.DownVote:
                {
                    m_FeedbackParamSection.SetDisplay(true);
                    m_FeedbackPanel.SetDisplay(true);
                    m_ReportUnacceptableContentLink.SetDisplay(true);
                    m_UpVoteButton.RemoveFromClassList(k_FeedbackButtonActiveClass);
                    m_DownVoteButton.AddToClassList(k_FeedbackButtonActiveClass);

                    if (!initialLoadedState)
                    {
                        Context.SendScrollToEndRequest();
                        this.ReportHeightChange();
                    }

                    break;
                }

                case FeedbackEditMode.UpVote:
                {
                    m_FeedbackParamSection.SetDisplay(true);
                    m_FeedbackPanel.SetDisplay(true);
                    m_ReportUnacceptableContentLink.SetDisplay(false);
                    m_UpVoteButton.AddToClassList(k_FeedbackButtonActiveClass);
                    m_DownVoteButton.RemoveFromClassList(k_FeedbackButtonActiveClass);

                    if (!initialLoadedState)
                    {
                        Context.SendScrollToEndRequest();
                        this.ReportHeightChange();
                    }

                    break;
                }
            }
        }

        void SetFeedback(AssistantMessageId assistantMessageId, FeedbackData? feedbackData)
        {
            if (assistantMessageId != m_MessageId)
                return;

            if (feedbackData == null)
                return;

            if (feedbackData.Value.Sentiment == Sentiment.Positive)
            {
                m_FeedbackMode = FeedbackEditMode.UpVote;
                m_FeedbackPlaceholder.text = AssistantUIConstants.FeedbackUpVotePlaceholder;
            }
            else
            {
                m_FeedbackMode = FeedbackEditMode.DownVote;
                m_FeedbackPlaceholder.text = AssistantUIConstants.FeedbackDownVotePlaceholder;
            }

            m_FeedbackText.value = string.Empty;
            ClearFeedbackStatus();

            RefreshFeedbackParameters(true);
        }

        void OnConsentLinkClicked(PointerDownLinkTagEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.linkID))
                Application.OpenURL(evt.linkID);
        }

        void OnSendAnotherComment(PointerUpEvent evt)
        {
            ClearFeedbackStatus();
            m_FeedbackPanel.SetDisplay(true);
            m_FeedbackText.value = string.Empty;
            m_FeedbackConsentCheckbox.value = false;
            m_FeedbackText.Focus();
            CheckFeedbackState();
            Context.SendScrollToEndRequest();
            this.ReportHeightChange();
        }

        void OnFeedbackSent(AssistantMessageId messageId, bool success)
        {
            if (!m_IsCommentSendPending || messageId != m_PendingCommentMessageId)
                return;

            if (panel == null)
                return;

            m_IsCommentSendPending = false;
            m_PendingCommentMessageId = AssistantMessageId.Invalid;
            m_FeedbackSendButtonLabel.text = AssistantUIConstants.FeedbackButtonDefaultTitle;

            if (!success)
            {
                UpdateFeedbackStatus(false);
                CheckFeedbackState();
                return;
            }

            if (k_StoredFeedbackUIState.TryGetValue(m_Message.Id, out var feedbackData))
            {
                // Null is intentional since we clear the sent text at this point
                var newFeedbackData = new FeedbackData(feedbackData.Sentiment, null);
                StoreFeedbackUIState(m_Message.Id, newFeedbackData);
            }

            ClearFeedbackParameters();

            UpdateFeedbackStatus(true);
        }

        void UpdateFeedbackStatus(bool isSuccess)
        {
            m_FeedbackStatusRow.EnableInClassList(k_FeedbackStatusSuccessClass, isSuccess);
            m_FeedbackStatusRow.EnableInClassList(k_FeedbackStatusErrorClass, !isSuccess);
            m_FeedbackStatusText.text = isSuccess
                ? AssistantUIConstants.FeedbackCommentSentMessage
                : AssistantUIConstants.FeedbackSendFailedMessage;

            m_FeedbackStatusIcon.EnableInClassList(k_FeedbackStatusCheckmarkClass, isSuccess);
            m_FeedbackStatusIcon.EnableInClassList(k_FeedbackStatusErrorIconClass, !isSuccess);

            m_FeedbackStatusAction.SetDisplay(isSuccess);
            m_FeedbackStatusRow.SetDisplay(true);
            m_FeedbackPanel.SetDisplay(!isSuccess);

            this.ReportHeightChange();
        }

        void ClearFeedbackStatus()
        {
            m_FeedbackStatusRow.EnableInClassList(k_FeedbackStatusSuccessClass, false);
            m_FeedbackStatusRow.EnableInClassList(k_FeedbackStatusErrorClass, false);
            m_FeedbackStatusRow.SetDisplay(false);
            m_FeedbackStatusAction.SetDisplay(false);
            m_FeedbackStatusIcon.RemoveFromClassList(k_FeedbackStatusCheckmarkClass);
            m_FeedbackStatusIcon.RemoveFromClassList(k_FeedbackStatusErrorIconClass);
        }

        void DisposeFeedbackResources()
        {
            Context.API.FeedbackSent -= OnFeedbackSent;
            Context.API.MessageCostReceived -= OnMessageCostReceived;
            m_MessageCostRequested = false;

            m_IsCommentSendPending = false;
            m_PendingCommentMessageId = AssistantMessageId.Invalid;
            m_ResponseCopyButtonActiveTokenSource?.Cancel();
            m_ResponseCopyButtonActiveTokenSource?.Dispose();
            m_ResponseCopyButtonActiveTokenSource = null;
        }

        static void SetCurrentConversation(AssistantConversationId conversationId)
        {
            if (s_CurrentStoredConversationFeedbacks == conversationId)
                return;

            k_StoredFeedbackUIState.Clear();

            s_CurrentStoredConversationFeedbacks = conversationId;
        }

        void StoreFeedbackUIState(AssistantMessageId assistantMessageId, FeedbackData feedbackData)
        {
            k_StoredFeedbackUIState[assistantMessageId] = feedbackData;
        }

        /// <summary>Keeps what the user typed for the message this element is about to stop showing, so a
        /// pooled element handed back to that message can offer it again.</summary>
        void ParkDraft(AssistantMessageId assistantMessageId)
        {
            if (assistantMessageId == AssistantMessageId.Invalid)
                return;

            var hasDraft = !string.IsNullOrEmpty(m_FeedbackText.value)
                || !string.IsNullOrEmpty(m_FeedbackEmailText.value)
                || m_FeedbackConsentCheckbox.value;

            if (!hasDraft)
            {
                k_ParkedFeedbackDrafts.Remove(assistantMessageId);
                return;
            }

            k_ParkedFeedbackDrafts[assistantMessageId] = new FeedbackDraft(
                m_FeedbackText.value, m_FeedbackEmailText.value, m_FeedbackConsentCheckbox.value, m_FeedbackMode);
        }

        void RestoreDraft(FeedbackDraft draft)
        {
            m_FeedbackText.value = draft.Comment;
            m_FeedbackEmailText.value = draft.Email;
            m_FeedbackConsentCheckbox.value = draft.Consent;

            // The sentiment the draft was typed under only has to be put back when the stored feedback
            // did not already restore it, which is what opens the panel the comment box lives in.
            if (m_FeedbackMode == FeedbackEditMode.None && draft.Mode != FeedbackEditMode.None)
            {
                m_FeedbackMode = draft.Mode;
                m_FeedbackPlaceholder.text = draft.Mode == FeedbackEditMode.UpVote
                    ? AssistantUIConstants.FeedbackUpVotePlaceholder
                    : AssistantUIConstants.FeedbackDownVotePlaceholder;
                RefreshFeedbackParameters(true);
            }

            // Without notifying: the foldout's callback scrolls the view and reports a height change,
            // both of which belong to the click that opened it and not to a rebind.
            if (draft.Consent || !string.IsNullOrEmpty(draft.Email))
                m_ContactFoldout.SetValueWithoutNotify(true);

            CheckFeedbackState();
        }
    }
}
