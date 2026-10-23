using System;
using System.Collections.Generic;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Utils.Perf;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// The only place in the scroll surface that knows the chat element types: it builds the element a
    /// display item needs and hands it its data. Everything it binds is idempotent, because a pooled
    /// element is rebound far more often than it is built.
    /// </summary>
    class ChatItemBinder
    {
        static readonly Dictionary<Type, Func<ManagedTemplate>> s_Factories = new()
        {
            { typeof(ChatElementUser), () => new ChatElementUser() },
            { typeof(ChatElementBlockCheckpoint), () => new ChatElementBlockCheckpoint() },
            { typeof(ChatElementBlockRevertedMessagesLink), () => new ChatElementBlockRevertedMessagesLink() },
            { typeof(ChatElementResponseSection), () => new ChatElementResponseSection() },
            { typeof(ChatElementReasoningSequence), () => new ChatElementReasoningSequence() },
            { typeof(SubagentHeaderElement), () => new SubagentHeaderElement() },
            { typeof(ChatElementBlockThought), () => new ChatElementBlockThought() },
            { typeof(ChatElementBlockAnswer), () => new ChatElementBlockAnswer() },
            { typeof(ChatElementBlockFunctionCall), () => new ChatElementBlockFunctionCall() },
            { typeof(ChatElementBlockAcpToolCall), () => new ChatElementBlockAcpToolCall() },
            { typeof(ChatElementBlockError), () => new ChatElementBlockError() },
            { typeof(ChatElementBlockInfo), () => new ChatElementBlockInfo() },
            { typeof(ChatElementBlockPlan), () => new ChatElementBlockPlan() },
            { typeof(CompletedActionsSection), () => new CompletedActionsSection() },
            { typeof(ChatElementFeedback), () => new ChatElementFeedback() }
        };

        readonly AssistantUIContext m_Context;

        IReadOnlyList<MessageModel> m_Messages = Array.Empty<MessageModel>();
        IReadOnlyList<DisplayItem> m_Items = Array.Empty<DisplayItem>();
        IReadOnlyList<ChatItemChrome> m_Chrome = Array.Empty<ChatItemChrome>();

        public ChatItemBinder(AssistantUIContext context)
        {
            m_Context = context;
        }

        public void SetSource(IReadOnlyList<MessageModel> messages, IReadOnlyList<DisplayItem> items, IReadOnlyList<ChatItemChrome> chrome)
        {
            m_Messages = messages;
            m_Items = items;
            m_Chrome = chrome;
        }

        public VisualElement Create(DisplayItem item)
        {
            switch (item.Kind)
            {
                case DisplayItemKind.GroupFooter:
                    return new ChatGroupFooter(item.FooterStyle);
                case DisplayItemKind.TopPadding:
                    return Spacer(ItemHeightModel.EstimateFor(item.Kind, item.ElementType, item.FooterStyle));
            }

            // A miss is a container that draws nothing of its own — the message frame and the reasoning
            // group, whose box geometry became a slot inset and a group footer.
            if (item.ElementType == null || !s_Factories.TryGetValue(item.ElementType, out var factory))
                return null;

            var element = factory();
            element.Initialize(m_Context);
            return element;
        }

        /// <summary>
        /// Hands a slot's content the data of an item. False when the item names a block its message no
        /// longer carries: the element still shows what it was last given, so the caller has to keep the
        /// slot hidden rather than let the previous item's content stand in for this one. A container
        /// with no element of its own has nothing to bind and is not a failure.
        /// </summary>
        public bool Bind(VisualElement content, int index)
        {
            if (content == null)
                return true;

            var startTicks = AssistantPerf.Now;
            var item = m_Items[index];
            var bound = true;

            switch (content)
            {
                case ChatElementBlockAnswer answer when TryGetBlock(m_Messages, item, out var answerModel):
                    answer.SetBlockModel(answerModel);
                    answer.SetChromeState(m_Chrome[index].Expanded);
                    break;
                case ChatElementBlock block when TryGetBlock(m_Messages, item, out var model):
                    block.SetBlockModel(model);
                    break;
                case ChatElementBlock:
                    bound = false;
                    break;
                case ChatElementBase leaf:
                    leaf.SetData(MessageOf(m_Messages, item));
                    break;
                case ChatElementFeedback feedback:
                    feedback.SetData(MessageOf(m_Messages, item));
                    break;
                case CompletedActionsSection completed:
                    BindCompletedActions(completed, item);
                    break;
                case ChatElementResponseSection section:
                    BindSection(section, index);
                    break;
                case ChatElementReasoningSequence sequence:
                    sequence.SetChromeState(m_Chrome[index].Expanded, m_Chrome[index].FoldoutTitle);
                    break;
                case SubagentHeaderElement header:
                    BindSubagent(header, index);
                    break;
            }

            if (bound && !m_Context.Blackboard.IsAPIWorking && !MessageOf(m_Messages, item).IsComplete)
                ApplyCancelled(content);

            if (AssistantPerf.Enabled)
                AssistantPerf.MarkElapsed("chat.bind", "kind=" + item.Kind, startTicks);

            return bound;
        }

        /// <summary>
        /// Applies what the API's working flag drives and the display stream does not carry: the section
        /// spinner and the feedback row read the flag off the blackboard, and a turn that stopped while a
        /// message is still incomplete has to reach that message's blocks, which a flattened section no
        /// longer contains.
        /// </summary>
        public void ApplyApiState(IReadOnlyDictionary<int, ChatSlot> realized, bool isWorking)
        {
            foreach (var pair in realized)
            {
                var item = m_Items[pair.Key];
                var content = pair.Value.Content;

                if (item.Kind is DisplayItemKind.Section or DisplayItemKind.FeedbackFooter)
                    Bind(content, pair.Key);

                if (isWorking || MessageOf(m_Messages, item).IsComplete)
                    continue;

                ApplyCancelled(content);
            }
        }

        /// <summary>
        /// The presentation OnConversationCancelled pushed into a retained element: a call the turn never
        /// came back to reads as failed, and so does the subagent that was waiting on it. A classic call's
        /// model carries no result to read that from — only the ACP one is rewritten on cancel — so the
        /// element is the only place it exists, and a slot realized for the first time after the turn
        /// stopped has to be given it at bind rather than by the state change it was not there for.
        /// </summary>
        static void ApplyCancelled(VisualElement content)
        {
            switch (content)
            {
                case ChatElementBlock block:
                    block.OnConversationCancelled();
                    break;
                case SubagentHeaderElement header:
                    header.TryMarkFailed();
                    break;
            }
        }

        /// <summary>
        /// Binds the realized items of one message again although nothing about the message changed, for
        /// validity that lives outside it: a checkpoint element re-reads whether its git tag is on disk
        /// on every bind. An element that guards its own rebind stays untouched, and an item that is not
        /// realized needs nothing — it reads the world when it is built.
        /// </summary>
        public static MessageModel MessageOf(IReadOnlyList<MessageModel> messages, in DisplayItem item)
            => item.MessageIndex >= 0 && item.MessageIndex < messages.Count ? messages[item.MessageIndex] : default;

        public static bool TryGetBlock(IReadOnlyList<MessageModel> messages, in DisplayItem item, out IMessageBlockModel model)
        {
            var blocks = MessageOf(messages, item).Blocks;

            if (item.BlockIndex >= 0 && blocks != null && item.BlockIndex < blocks.Count)
            {
                model = blocks[item.BlockIndex];
                return true;
            }

            model = null;
            return false;
        }

        void BindCompletedActions(CompletedActionsSection section, in DisplayItem item)
        {
            var message = MessageOf(m_Messages, item);
            section.SetData(message.IsComplete ? CompletedActionsExtractor.Extract(message.Blocks) : null);
        }

        void BindSection(ChatElementResponseSection section, int index)
        {
            var item = m_Items[index];

            // The flattener emits a section's reasoning group as its first child, and emits none at all
            // when the section has no reasoning to show; the item past the section's span is its sibling.
            var hasReasoning = index + 1 < m_Items.Count && m_Items[index + 1].Kind == DisplayItemKind.ReasoningGroup;
            var sibling = index + item.Span + 1;
            var isLastSection = sibling >= m_Items.Count || m_Items[sibling].Kind != DisplayItemKind.Section;

            var chrome = m_Chrome[index];
            section.SetChromeState(hasReasoning, chrome.ToggleEnabled, chrome.Expanded);
            section.SetIsWorkingState(
                isLastSection && m_Context.Blackboard.IsAPIWorking && !MessageOf(m_Messages, item).IsComplete,
                HasAnswerContent(index, sibling));
        }

        /// <summary>
        /// Whether the section has already put a block in its answer container, which flattening lays out
        /// as a slot of its own rather than as a child of the section: the reasoning spinner came down
        /// when the first one was added, and a section holding none of them cannot see it arrive.
        /// </summary>
        bool HasAnswerContent(int index, int sibling)
        {
            for (var child = index + 1; child < sibling && child < m_Items.Count; child++)
            {
                if (m_Items[child].Context == ContextSignature.AnswerContent)
                    return true;
            }

            return false;
        }

        void BindSubagent(SubagentHeaderElement header, int index)
        {
            var chrome = m_Chrome[index];

            // Ahead of everything that settles a header: the spawn failure and the progress below, and
            // the cancelled state the bind applies once it returns from here.
            header.ResetState();

            if (!string.IsNullOrEmpty(chrome.FoldoutTitle))
                header.SetAgent(chrome.FoldoutTitle);

            header.SetChromeState(chrome.Expanded);

            // The spawn call carries the subagent's own outcome, so it is applied ahead of the progress
            // that would otherwise settle the group complete over it — and holds the progress off
            // entirely until it comes back.
            if (chrome.SpawnState == SpawnCallState.Failed)
                header.TryMarkFailed();

            header.UpdateProgress(chrome.CompletedCount, chrome.TotalCount,
                canSettleComplete: chrome.SpawnState != SpawnCallState.Pending);
        }

        static VisualElement Spacer(float height)
            => new() { pickingMode = PickingMode.Ignore, style = { height = height } };
    }
}
