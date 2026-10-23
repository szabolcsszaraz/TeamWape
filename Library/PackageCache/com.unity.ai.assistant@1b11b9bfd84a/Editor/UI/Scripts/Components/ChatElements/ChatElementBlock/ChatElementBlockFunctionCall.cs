using System;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementBlockFunctionCall : ChatElementBlockBase<FunctionCallBlockModel>
    {
        VisualElement m_RootContainer;
        FunctionCallElement m_FunctionCallElement;
        string m_RendererFunctionId;
        Guid m_RendererCallId;
        BaseEventSubscriptionTicket m_InlineInteractionSubscription;
        InteractionContentView m_InlineContent;
        InteractionContentView m_CompletedSubscribedContent;
        Guid m_AttachedCallId;

        public Guid CallId => BlockModel.Call.CallId;
        public bool IsDone => BlockModel.Call.Result.IsDone;
        public string Agent => BlockModel.Call.Agent;

        public override void OnConversationCancelled() => m_FunctionCallElement?.OnConversationCancelled();

        protected override void InitializeView(TemplateContainer view)
        {
            base.InitializeView(view);

            m_RootContainer = view.Q<VisualElement>("functionCallRoot");
            RegisterAttachEvents(OnAttach, OnDetach);
        }

        void OnAttach(AttachToPanelEvent evt)
        {
            m_InlineInteractionSubscription = AssistantEvents.Subscribe<EventInlineInteractionPushed>(OnInlineInteractionPushed);
            TryAttachPendingInlineInteraction();
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            AssistantEvents.Unsubscribe(ref m_InlineInteractionSubscription);
        }

        void OnInlineInteractionPushed(EventInlineInteractionPushed evt)
        {
            if (evt.CallId != CallId)
                return;

            TryAttachPendingInlineInteraction();
        }

        void TryAttachPendingInlineInteraction()
        {
            // Re-attach cached content if the element was recycled by the virtualized list.
            if (m_InlineContent != null)
            {
                if (!m_RootContainer.Contains(m_InlineContent))
                    m_RootContainer.Add(m_InlineContent);
                return;
            }

            if (Context?.PendingInlineInteractions == null)
                return;

            if (BlockModel?.Call == null)
                return;

            if (!Context.PendingInlineInteractions.TryGetValue(CallId, out var content))
                return;

            // Keep the entry in the dictionary so it can be re-attached after scroll recycling.
            // Remove it only once the interaction completes. Guard against duplicate subscriptions
            // when OnBlockModelChanged recycles the element back to the same CallId.
            if (m_CompletedSubscribedContent != content)
            {
                m_CompletedSubscribedContent = content;
                var capturedCallId = CallId;
                content.Completed += () =>
                {
                    Context?.PendingInlineInteractions?.Remove(capturedCallId);
                    m_CompletedSubscribedContent = null;
                    AssistantEvents.Send(new EventInlineInteractionCompleted(capturedCallId));
                };
            }
            m_InlineContent = content;

            if (!content.IsInitialized)
                content.Initialize(Context);

            m_RootContainer.Add(content);
            Context.SendScrollToEndRequest();
        }

        protected override void OnBlockModelChanged()
        {
            var newCallId = BlockModel?.Call.CallId ?? Guid.Empty;
            if (m_InlineContent != null && newCallId != m_AttachedCallId)
            {
                // The content outlives the element that showed it, and another block bound to the same
                // call re-parents it, so by now it may be someone else's child — and Remove throws on one.
                if (m_InlineContent.parent == m_RootContainer)
                    m_RootContainer.Remove(m_InlineContent);

                m_InlineContent = null;
            }
            m_AttachedCallId = newCallId;
            RefreshContent();
            TryAttachPendingInlineInteraction();
        }

        void RefreshContent()
        {
            var functionCall = BlockModel.Call;

            // The renderer is chosen by function id and cannot be swapped on a built element, so a block
            // rebound from one call to another has to build a new one: the renderer reads the result as
            // its own tool's type, and every function call shares this element type when it is pooled.
            // Two calls of the same tool need one too — a built element expands its foldout and offers
            // its expanded view off the call it finished, and neither is taken back for the next one.
            if (m_FunctionCallElement != null
                && (m_RendererFunctionId != functionCall.FunctionId || m_RendererCallId != functionCall.CallId))
            {
                m_FunctionCallElement.RemoveFromHierarchy();
                m_FunctionCallElement = null;
            }

            if (m_FunctionCallElement == null)
            {
                var renderer = FunctionCallRendererFactory.CreateFunctionCallRenderer(functionCall.FunctionId);
                m_FunctionCallElement = new FunctionCallElement(renderer);
                m_FunctionCallElement.Initialize(Context);
                m_RootContainer.Add(m_FunctionCallElement);
                m_RendererFunctionId = functionCall.FunctionId;
                m_RendererCallId = functionCall.CallId;
            }

            m_FunctionCallElement.UpdateData(functionCall);
        }
    }
}
