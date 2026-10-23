using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Checkpoint;
using Unity.AI.Assistant.Editor.Checkpoint.Events;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    [UxmlElement]
    partial class ChatElementCheckpoint : ManagedTemplate
    {
        const string k_ToolTipCheckpointsDisabledPreference = "Checkpoints are disabled in Preferences. Enable them under Project Settings to roll back changes.";
        const string k_ToolTipCheckpointsDisabledVerification = "Checkpoints are disabled because initial verification did not include all project files. Restart the editor to retry, or use Initialize Anyway in the Checkpoint dialog.";
        const string k_ToolTipLatestCheckpoint = "You're caught up! No further changes.";
        const string k_ToolTipRestoreCheckpoint = "Restore this checkpoint";
        
        VisualElement m_CheckpointSection;
        VisualElement m_CheckpointTextSection;

        Button m_CheckpointButton;
        AssistantImage m_CheckpointButtonImage;
        
        AssistantMessageId m_AnchorMessageId;
        int m_MessageOffset;
        AssistantMessageId m_TargetMessageId;
        AssistantMessageId m_ResponseMessageId;
        
        BaseEventSubscriptionTicket m_CheckpointsChangedSubscription;
        BaseEventSubscriptionTicket m_CheckpointEnableStateChangedSubscription;

        public ChatElementCheckpoint() : base(AssistantUIConstants.UIModulePath) { }

        public bool CheckpointValid { get; private set;}
        public bool ShowCheckpointLabel { get; set; } = false;
        
        public event Action OnValidated;

        protected override void InitializeView(TemplateContainer view)
        {
            m_CheckpointSection = view.Q("checkpointElementSection");
            m_CheckpointTextSection = view.Q("checkpointTextSection");
            m_CheckpointButton = view.SetupButton("checkpointButton", OnCheckpointClicked);
            m_CheckpointButtonImage = m_CheckpointButton.SetupImage("checkpointButtonImage", "checkpoint");

            m_CheckpointSection.SetDisplay(false);
            CheckpointValid = false;
            m_CheckpointTextSection.SetDisplay(ShowCheckpointLabel);
            RegisterAttachEvents(OnAttach, OnDetach);
        }

        void OnAttach(AttachToPanelEvent evt)
        {
            m_CheckpointsChangedSubscription = AssistantEvents.Subscribe<EventCheckpointsChanged>(OnCheckpointsChanged);
            m_CheckpointEnableStateChangedSubscription = AssistantEvents.Subscribe<EventCheckpointEnableStateChanged>(_ => ValidateCheckpoint());
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            AssistantEvents.Unsubscribe(ref m_CheckpointsChangedSubscription);
            AssistantEvents.Unsubscribe(ref m_CheckpointEnableStateChangedSubscription);
        }

        public void SetCheckpointData(AssistantMessageId messageId, int offset = 0)
        {
            m_AnchorMessageId = messageId;
            m_MessageOffset = offset;

            ValidateCheckpoint();
        }
        
        void OnCheckpointsChanged(EventCheckpointsChanged eventData)
        {
            if (CheckpointValid)
            {
                return;
            }

            ValidateCheckpoint();
        }

        void ValidateCheckpoint()
        {
            CheckpointValid = false;
            m_TargetMessageId = default;
            m_ResponseMessageId = default;
            m_CheckpointSection.SetEnabled(false);
            m_CheckpointSection.SetDisplay(false);
            
            if (!AssistantCheckpoints.IsInitialized || !AssistantProjectPreferences.CheckpointEnabled)
            {
                if (!AssistantProjectPreferences.CheckpointEnabled)
                {
                    m_CheckpointButton.tooltip = k_ToolTipCheckpointsDisabledPreference;
                }
                else
                {
                    m_CheckpointButton.tooltip = k_ToolTipCheckpointsDisabledVerification;
                }
                return;
            }
            
            if (IsLastNonRevertedMessage())
            {
                m_CheckpointSection.SetDisplay(true);
                m_CheckpointSection.SetEnabled(false);
                m_CheckpointButton.tooltip = k_ToolTipLatestCheckpoint;
                return;
            }

            var hasMessageIds = TryUpdateTargetMessage(out _);

            if (m_ResponseMessageId.Type != AssistantMessageIdType.External)
            {
                return;
            }

            if (!AssistantCheckpoints.HasCheckpointForMessage(m_ResponseMessageId.ConversationId, m_ResponseMessageId.FragmentId))
            {
                return;
            }

            m_CheckpointSection.SetDisplay(true);

            CheckpointValid = true;
            OnValidated?.Invoke();
            
            m_CheckpointSection.SetEnabled(hasMessageIds);
            m_CheckpointButton.tooltip = k_ToolTipRestoreCheckpoint;
        }

        bool TryGetConversationAndMessageIndex(out ConversationModel conv, out int messageIndex)
        {
            messageIndex = -1;

            // DisplayedConversation is set in Populate() and kept current by OnConversationChanged(),
            // so it always reflects the latest message IDs (use it as the authoritative source).
            var displayed = Context.DisplayedConversation;
            if (displayed != null && displayed.Id == m_AnchorMessageId.ConversationId)
            {
                conv = displayed;
                messageIndex = FindAnchorIndex(conv);
                return messageIndex != -1;
            }

            // Fall back to the blackboard only when DisplayedConversation hasn't been set yet
            // (e.g. OnCheckpointsChanged fires before the first Populate call after domain reload).
            conv = Context.Blackboard.Conversations.FirstOrDefault(c => c.Id == m_AnchorMessageId.ConversationId);
            if (conv == null)
            {
                return false;
            }

            messageIndex = FindAnchorIndex(conv);
            return messageIndex != -1;
        }

        int FindAnchorIndex(ConversationModel conv)
        {
            // Match by FragmentId only to handle ID type transitions (e.g. Internal->External
            // after domain reload). Fall back to full equality for empty FragmentId (placeholders).
            if (!string.IsNullOrEmpty(m_AnchorMessageId.FragmentId))
            {
                return conv.Messages.FindIndex(msg => msg.Id.FragmentId == m_AnchorMessageId.FragmentId);
            }
            return conv.Messages.FindIndex(msg => msg.Id == m_AnchorMessageId);
        }

        int FindNextNonRevertedUserMessage(ConversationModel conv, int startIndex)
        {
            var currentIndex = startIndex;
            while (currentIndex < conv.Messages.Count)
            {
                var message = conv.Messages[currentIndex];
                if (message.RevertedTimeStamp == 0 && message.Role == MessageModelRole.User)
                {
                    return currentIndex;
                }
                currentIndex++;
            }
            return -1;
        }

        bool TryUpdateTargetMessage(out bool outOfRange)
        {
            outOfRange = false;

            if (!TryGetConversationAndMessageIndex(out var conv, out var messageIndex))
            {
                return false;
            }

            var currentMessageIndex = messageIndex;

            // Go to next user message according to offset
            if (m_MessageOffset > 0)
            {
                currentMessageIndex++;
                var offsetRemaining = m_MessageOffset;

                while (currentMessageIndex < conv.Messages.Count && offsetRemaining > 0)
                {
                    var currentMsg = conv.Messages[currentMessageIndex];
                    if (currentMsg.Role != MessageModelRole.User)
                    {
                        currentMessageIndex++;
                        continue;
                    }
                    offsetRemaining--;
                    if (offsetRemaining == 0)
                    {
                        break;
                    }
                }
                
            }

            currentMessageIndex = FindNextNonRevertedUserMessage(conv, currentMessageIndex);

            outOfRange = (currentMessageIndex == -1);
            if (!outOfRange)
            {
                m_TargetMessageId = conv.Messages[currentMessageIndex].Id;

                if (currentMessageIndex + 1 >= conv.Messages.Count)
                {
                    return false;
                }

                m_ResponseMessageId = conv.Messages[currentMessageIndex + 1].Id;
            }

            return !outOfRange;
        }

        bool IsLastNonRevertedMessage()
        {
            if (!TryGetConversationAndMessageIndex(out var conv, out var messageIndex))
            {
                return false;
            }

            // Check if there are any non-reverted user messages after this one
            var nextUserIndex = FindNextNonRevertedUserMessage(conv, messageIndex);
            return nextUserIndex == -1;
        }
        
#if UNITY_6000_6_OR_NEWER
        // No top-level await here (the awaiting work lives in the onConfirm lambda),
        // so drop async on 6000.6+ to silence CS1998. Behavior is unchanged.
        void OnCheckpointClicked(PointerUpEvent evt)
#else
        async void OnCheckpointClicked(PointerUpEvent evt)
#endif
        {
            if (!CheckpointValid)
            {
                return;
            }

            var targetMessageId = m_TargetMessageId;
            var responseMessageId = m_ResponseMessageId;

            var promptText = string.Empty;
            var conv = Context.DisplayedConversation;
            if (conv != null)
            {
                var targetMsg = conv.Messages.FirstOrDefault(m => m.Id.FragmentId == targetMessageId.FragmentId);
                var promptBlock = targetMsg.Blocks?.OfType<PromptBlockModel>().FirstOrDefault();
                if (promptBlock != null)
                {
                    promptText = promptBlock.Content;
                }
            }

            CheckpointConfirmationDialogWindow.ShowRestoreDialogAsync(
                responseMessageId,
                promptText,
                onConfirm: async () =>
                {
                    AssistantEvents.Send(new EventCheckpointRestoreRequested(responseMessageId));
                    await Context.API.RevertMessage(targetMessageId);
                    Context.API.ConversationLoad(Context.Blackboard.ActiveConversationId);
                });
        }
    }
}
