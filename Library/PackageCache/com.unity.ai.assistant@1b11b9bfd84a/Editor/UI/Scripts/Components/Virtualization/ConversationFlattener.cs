using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>Per-item foldout state, keyed by <see cref="DisplayItem.Identity"/>.</summary>
    interface IChatViewState
    {
        bool IsExpanded(ContentKey identity, bool defaultExpanded);
    }

    /// <summary>
    /// Flattens a conversation into the display-item stream in pre-order, applying the placement rules
    /// <see cref="ChatElementResponseSection"/> and the message frame that held it applied imperatively
    /// while building their trees.
    /// </summary>
    class ConversationFlattener
    {
        const string k_SpawnSubagentToolId = "Agent.SpawnSubagent";
        const string k_RoleParam = "role";

        readonly IChatViewState m_ViewState;

        MessageModel m_Message;
        int m_MessageIndex;
        int m_SectionIndex;
        int m_SequenceIndex;
        Node m_Reasoning;
        Node m_Sequence;
        int m_SequenceOrdinal;
        bool m_HasAnswerBlock;

        ConversationFlattener(IChatViewState viewState)
        {
            m_ViewState = viewState;
        }

        /// <summary>
        /// Flattens the conversation, and describes each emitted item's chrome bind state in
        /// <paramref name="chrome"/>, which is exactly as long as the returned stream.
        /// </summary>
        public static List<DisplayItem> Flatten(IReadOnlyList<MessageModel> messages, IChatViewState viewState, out List<ChatItemChrome> chrome)
            => new ConversationFlattener(viewState).Run(messages, out chrome);

        List<DisplayItem> Run(IReadOnlyList<MessageModel> messages, out List<ChatItemChrome> chrome)
        {
            var items = new List<DisplayItem>();
            chrome = new List<ChatItemChrome>();
            var padKey = ContentKeyBuilder.Start().Add((int)DisplayItemKind.TopPadding).Key;
            m_MessageIndex = DisplayItem.NoIndex;
            Linearize(Leaf(DisplayItemKind.TopPadding, null, DisplayItem.NoIndex, padKey, padKey), DisplayItem.NoIndex, items, chrome);

            for (var i = 0; messages != null && i < messages.Count; i++)
            {
                m_Message = messages[i];
                m_MessageIndex = i;
                m_SectionIndex = DisplayItem.NoIndex;

                var message = BuildMessage();
                Linearize(message, DisplayItem.NoIndex, items, chrome);

                // The per-message bottom margin sits beside the message it follows, not inside it.
                Linearize(Footer(message, GroupFooterStyle.Message), DisplayItem.NoIndex, items, chrome);
            }

            return items;
        }

        // The order of these tests is the one the message wrapper used to pick an element type.
        Node BuildMessage() => m_Message switch
        {
            { IsRevertedTimeStampLink: true } => MessageLeaf(DisplayItemKind.RevertedLink, typeof(ChatElementBlockRevertedMessagesLink)),
            { IsInitialCheckpoint: true } => MessageLeaf(DisplayItemKind.Checkpoint, typeof(ChatElementBlockCheckpoint)),
            { Role: MessageModelRole.User } => MessageLeaf(DisplayItemKind.UserPrompt, typeof(ChatElementUser), includeBlocks: true),
            // An unsupported role still gets a frame: refusing to flatten one message costs the whole stream.
            _ => BuildAssistantFrame()
        };

        Node BuildAssistantFrame()
        {
            var identity = Identity(DisplayItemKind.MessageFrame);
            // No element of its own: a frame drew a box and held its children, and flattening gives the
            // box to the slot inset and the children items of their own.
            var frame = Container(DisplayItemKind.MessageFrame, null, identity,
                ContentKeyBuilder.Start().Add(identity.Value).Key);

            var blocks = m_Message.Blocks;
            var sectionStart = 0;
            var sectionIndex = 0;

            for (var blockIndex = 0; blocks != null && blockIndex < blocks.Count; blockIndex++)
            {
                // PartitionBlocksIntoSections closes a section on every answer block, then adds whatever
                // is left over as a final one.
                if (blocks[blockIndex] is not AnswerBlockModel)
                    continue;

                frame.Children.Add(BuildSection(sectionIndex++, sectionStart, blockIndex + 1));
                sectionStart = blockIndex + 1;
            }

            if (blocks != null && sectionStart < blocks.Count)
                frame.Children.Add(BuildSection(sectionIndex, sectionStart, blocks.Count));

            // Both render below responsesContainer, so they are trailing leaves rather than frame chrome.
            m_SectionIndex = DisplayItem.NoIndex;
            frame.Children.Add(MessageLeaf(DisplayItemKind.CompletedActions, typeof(CompletedActionsSection)));
            frame.Children.Add(MessageLeaf(DisplayItemKind.FeedbackFooter, typeof(ChatElementFeedback)));

            return frame;
        }

        Node BuildSection(int sectionIndex, int start, int end)
        {
            m_SectionIndex = sectionIndex;

            var section = Container(DisplayItemKind.Section, typeof(ChatElementResponseSection),
                Identity(DisplayItemKind.Section), default);
            var reasoningIdentity = Identity(DisplayItemKind.ReasoningGroup);
            m_Reasoning = Container(DisplayItemKind.ReasoningGroup, null, reasoningIdentity,
                ContentKeyBuilder.Start().Add(reasoningIdentity.Value).Key);
            m_Sequence = null;
            m_SequenceIndex = 0;
            m_HasAnswerBlock = false;
            var answer = new List<Node>();
            var blocks = m_Message.Blocks;

            for (var blockIndex = start; blockIndex < end; blockIndex++)
            {
                var blockModel = blocks[blockIndex];
                switch (blockModel)
                {
                    case ThoughtBlockModel thought:
                    {
                        var sequence = EnsureSequence();
                        sequence.Children.Add(BlockLeaf(DisplayItemKind.Thought, typeof(ChatElementBlockThought), blockIndex,
                            ContextSignature.SequenceContent));
                        sequence.Title = ChatElementBlockThought.ExtractTitle(thought.Content, out _);
                        continue;
                    }
                    case FunctionCallBlockModel call:
                    {
                        var emphasizedSuccess = FunctionCallRendererFactory.IsEmphasized(call.Call.FunctionId)
                            && call.Call.Result is { IsDone: true, HasFunctionCallSucceeded: true };

                        if (emphasizedSuccess && SubagentHeaderElement.IsSubagent(call.Call.Agent))
                            emphasizedSuccess = false;

                        if (!emphasizedSuccess)
                        {
                            AddReasoningCall(call, blockIndex);
                            continue;
                        }

                        // A promoted call must not orphan the calls its sequence is still waiting on.
                        if (m_Sequence is not { HasPendingCalls: true })
                            BreakSequence();

                        break;
                    }
                    case AcpToolCallBlockModel { IsReasoning: true } toolCall:
                    {
                        var sequence = EnsureSequence();
                        sequence.Add(
                            BlockLeaf(DisplayItemKind.Block, typeof(ChatElementBlockAcpToolCall), blockIndex, ContextSignature.SequenceContent),
                            !toolCall.IsDone);
                        sequence.Title = AcpToolCallElement.GetDisplayTitle(toolCall.CallInfo?.Title, toolCall.CallInfo?.ToolName);
                        continue;
                    }
                    case AcpToolCallBlockModel:
                    case AnswerBlockModel:
                    case ErrorBlockModel:
                    case InfoBlockModel:
                    case AcpPlanBlockModel:
                        BreakSequence();
                        break;
                }

                // PlaceBlock's isAnswerContainer test: a promoted emphasized call is the only regular
                // block that goes back into the section's reasoning, above the sequences already there.
                var isAnswerTarget = blockModel switch
                {
                    AnswerBlockModel or ErrorBlockModel or InfoBlockModel or AcpPlanBlockModel => true,
                    AcpToolCallBlockModel { IsReasoning: false } => true,
                    _ => false
                };

                var block = BlockLeaf(DisplayItemKind.Block, RegularBlockElementType(blockModel), blockIndex,
                    isAnswerTarget ? ContextSignature.AnswerContent : ContextSignature.ReasoningContent);

                if (blockModel is AnswerBlockModel)
                    ReadSourcesFoldout(block);

                if (isAnswerTarget)
                    answer.Add(block);
                else
                    m_Reasoning.Children.Add(block);

                m_HasAnswerBlock |= blockModel is AnswerBlockModel;
            }

            if (m_Sequence != null)
                CloseSequence(m_ViewState.IsExpanded(m_Sequence.Identity, true));

            // OnAnswerBlockCreated enables the toggle, and OnConversationCancelled enables it for reasoning
            // a turn stopped before answering: only a message still streaming has none to offer yet.
            var hasReasoning = m_Reasoning.Children.Count > 0;
            var toggleEnabled = hasReasoning && (m_HasAnswerBlock || m_Message.IsComplete);
            if (hasReasoning)
            {
                // Keyed by the section: the foldout that toggles the group is drawn by the section element.
                m_Reasoning.SetExpanded(!toggleEnabled
                    || m_ViewState.IsExpanded(section.Identity, !AssistantEditorPreferences.CollapseReasoningWhenComplete));

                // #reasoningContent carries the separator and collapsing hides it, so it is a child;
                // #reasoningContainer carries the bottom margin and collapsing keeps it, so it is a sibling.
                if (m_HasAnswerBlock)
                    m_Reasoning.Children.Add(Footer(m_Reasoning, GroupFooterStyle.ReasoningSeparator));

                section.Children.Add(m_Reasoning);
                section.Children.Add(Footer(m_Reasoning, GroupFooterStyle.ReasoningGroup));
            }

            section.Children.AddRange(answer);
            section.ToggleEnabled = toggleEnabled;
            section.Expanded = m_Reasoning.Children.Count == 0 || m_Reasoning.Expanded;
            section.Content = ContentKeyBuilder.Start().Add(section.Identity.Value).Add(toggleEnabled).Key;
            return section;
        }

        /// <summary>
        /// The answer's foldout over its sources, which the element draws inside the block rather than
        /// around it: no item of its own, so it is keyed by the block's identity and its state is folded
        /// into the block's content key, where a collapsed answer's height is cached apart from an
        /// expanded one's.
        /// </summary>
        void ReadSourcesFoldout(Node answer)
        {
            answer.Expanded = m_ViewState.IsExpanded(answer.Identity, true);
            answer.Content = ContentKeyBuilder.Start().Add(answer.Content.Value).Add(answer.Expanded).Key;
        }

        void AddReasoningCall(FunctionCallBlockModel call, int blockIndex)
        {
            var sequence = EnsureSequence();
            var isDone = call.Call.Result.IsDone;
            sequence.Title = call.Call.GetDefaultTitle();

            if (call.Call.FunctionId == k_SpawnSubagentToolId)
                RecordSpawnCall(sequence, call.Call);

            if (!SubagentHeaderElement.IsSubagent(call.Call.Agent))
            {
                sequence.Add(
                    BlockLeaf(DisplayItemKind.Block, typeof(ChatElementBlockFunctionCall), blockIndex, ContextSignature.SequenceContent),
                    !isDone);
                return;
            }

            var agent = call.Call.Agent;
            sequence.AgentHeaders ??= new Dictionary<string, Node>();

            if (!sequence.AgentHeaders.TryGetValue(agent, out var header))
            {
                // The header lands where the agent's first call would have gone; every later call for
                // that agent joins it, wherever in block order it arrives.
                header = Container(DisplayItemKind.SubagentGroup, typeof(SubagentHeaderElement),
                    Identity(DisplayItemKind.SubagentGroup, m_SequenceOrdinal, agent), default, ContextSignature.SequenceContent);
                header.SetExpanded(m_ViewState.IsExpanded(header.Identity, true));

                // A collapsed group has no calls left to read the agent name off, so it travels here.
                header.Title = agent;
                sequence.AgentHeaders.Add(agent, header);
                sequence.Children.Add(header);
            }

            header.Children.Add(BlockLeaf(DisplayItemKind.Block, typeof(ChatElementBlockFunctionCall), blockIndex,
                ContextSignature.SubagentContent));
            header.TotalCount++;
            if (isDone)
                header.CompletedCount++;

            sequence.HasPendingCalls |= !isDone;
        }

        /// <summary>
        /// Settles the sequence's subagent groups, once every call made in it has arrived: each spawn
        /// call is paired with the group it minted, and each group is keyed over what its header renders.
        /// </summary>
        static void SettleSubagentGroups(Node sequence)
        {
            MatchSpawnCalls(sequence);

            foreach (var child in sequence.Children)
            {
                if (child.Kind != DisplayItemKind.SubagentGroup)
                    continue;

                // The total the header reads beside the completed count is deliberately not folded in:
                // a group holds one child item per call it counts, and Linearize keys a container over
                // its child count already.
                child.Content = ContentKeyBuilder.Start()
                    .Add(child.Identity.Value)
                    .Add(child.CompletedCount)
                    .Add((int)child.SpawnState)
                    .Key;
            }
        }

        /// <summary>
        /// Keeps a spawn call until the sequence closes. The agent name a role becomes is only known once
        /// the spawned agent's own calls arrive, and they arrive after the call that asked for them.
        /// </summary>
        static void RecordSpawnCall(Node sequence, AssistantFunctionCall call)
        {
            var role = call.Parameters?[k_RoleParam]?.ToString();
            if (string.IsNullOrEmpty(role))
                return;

            var state = call.Result switch
            {
                { IsDone: false } => SpawnCallState.Pending,
                { HasFunctionCallSucceeded: true } => SpawnCallState.Succeeded,
                _ => SpawnCallState.Failed
            };

            sequence.SpawnCalls ??= new List<(string, SpawnCallState)>();
            sequence.SpawnCalls.Add((role, state));
        }

        /// <summary>
        /// Pairs the sequence's spawn calls with the groups they minted, both in the order they arrived:
        /// the call names a role and one role can be spawned more than once, so the first group of that
        /// role still waiting for a spawn call is the one this call spawned.
        /// </summary>
        static void MatchSpawnCalls(Node sequence)
        {
            if (sequence.SpawnCalls == null)
                return;

            foreach (var (role, state) in sequence.SpawnCalls)
            {
                var spawnedName = SubagentHeaderElement.SubagentPrefix + role;

                foreach (var child in sequence.Children)
                {
                    if (child.Kind != DisplayItemKind.SubagentGroup || child.SpawnState != SpawnCallState.None
                        || !child.Title.StartsWith(spawnedName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    child.SpawnState = state;
                    break;
                }
            }
        }

        Node EnsureSequence()
        {
            if (m_Sequence != null)
                return m_Sequence;

            m_SequenceOrdinal = m_SequenceIndex++;
            var identity = Identity(DisplayItemKind.ReasoningSequence, m_SequenceOrdinal);
            m_Sequence = Container(DisplayItemKind.ReasoningSequence, typeof(ChatElementReasoningSequence), identity,
                ContentKeyBuilder.Start().Add(identity.Value).Key, ContextSignature.ReasoningContent);
            m_Reasoning.Children.Add(m_Sequence);
            return m_Sequence;
        }

        void BreakSequence()
        {
            if (m_Sequence == null)
                return;

            // BreakReasoningSequence collapses the sequence it closes.
            CloseSequence(m_ViewState.IsExpanded(m_Sequence.Identity, false));
            m_Sequence = null;
        }

        void CloseSequence(bool expanded)
        {
            m_Sequence.SetExpanded(expanded);
            SettleSubagentGroups(m_Sequence);

            if (m_Sequence.Children.Count > 0)
                m_Sequence.Children.Add(Footer(m_Sequence, GroupFooterStyle.ReasoningSequence));
        }

        static Type RegularBlockElementType(IMessageBlockModel blockModel) => blockModel switch
        {
            AnswerBlockModel => typeof(ChatElementBlockAnswer),
            FunctionCallBlockModel => typeof(ChatElementBlockFunctionCall),
            ErrorBlockModel => typeof(ChatElementBlockError),
            InfoBlockModel => typeof(ChatElementBlockInfo),
            AcpToolCallBlockModel => typeof(ChatElementBlockAcpToolCall),
            AcpPlanBlockModel => typeof(ChatElementBlockPlan),
            // A block kind no element renders keeps its place in the stream and draws nothing there.
            _ => null
        };

        // Where the item sits in the conversation, with nothing a streamed chunk can change. The
        // ordinal is the sequence index or the block index, depending on the kind.
        // The message's place rather than its id: both the type and the fragment id of a streamed
        // message are replaced the moment it finalizes, while its place in the stream never moves.
        ContentKey Identity(DisplayItemKind kind, int ordinal = DisplayItem.NoIndex, string agent = null)
            => ContentKeyBuilder.Start()
                .Add(m_MessageIndex)
                .Add((int)kind)
                .Add(m_SectionIndex)
                .Add(ordinal)
                .AddName(agent)
                .Key;

        Node MessageLeaf(DisplayItemKind kind, Type elementType, bool includeBlocks = false)
        {
            var identity = Identity(kind);
            var content = ContentKeyBuilder.Start().Add(identity.Value).AddMessageState(m_Message);

            if (includeBlocks && m_Message.Blocks != null)
            {
                foreach (var block in m_Message.Blocks)
                    content = content.Add(block);
            }

            return Leaf(kind, elementType, DisplayItem.NoIndex, identity, content.Key);
        }

        Node BlockLeaf(DisplayItemKind kind, Type elementType, int blockIndex, ContextSignature context)
        {
            var identity = Identity(kind, blockIndex);
            var content = ContentKeyBuilder.Start().Add(identity.Value).Add(m_Message.Blocks[blockIndex]).Key;
            return Leaf(kind, elementType, blockIndex, identity, content, context);
        }

        Node Footer(Node container, GroupFooterStyle style)
        {
            // The style is part of the identity: one container can trail two footers.
            var identity = ContentKeyBuilder.Start()
                .Add(container.Identity.Value)
                .Add((int)DisplayItemKind.GroupFooter)
                .Add((int)style)
                .Key;
            var footer = Leaf(DisplayItemKind.GroupFooter, null, DisplayItem.NoIndex, identity, identity);
            footer.FooterStyle = style;
            return footer;
        }

        Node Leaf(DisplayItemKind kind, Type elementType, int blockIndex, ContentKey identity, ContentKey content,
            ContextSignature context = ContextSignature.None)
            => new(kind, elementType, m_MessageIndex, blockIndex, context, identity, content, container: false);

        Node Container(DisplayItemKind kind, Type elementType, ContentKey identity, ContentKey content,
            ContextSignature context = ContextSignature.None)
            => new(kind, elementType, m_MessageIndex, DisplayItem.NoIndex, context, identity, content, container: true);

        static void Linearize(Node node, int parent, List<DisplayItem> items, List<ChatItemChrome> chrome)
        {
            var index = items.Count;
            var content = node.Children == null
                ? node.Content
                : ContentKeyBuilder.Start().Add(node.Content.Value).Add(node.Children.Count).Add(!node.Collapsed).Key;

            items.Add(new DisplayItem(node.Kind, parent, node.MessageIndex, node.BlockIndex, node.ElementType, node.Context,
                node.FooterStyle, node.Identity, content));
            chrome.Add(ChromeFor(node));

            if (node.Children == null || node.Collapsed)
                return;

            foreach (var child in node.Children)
                Linearize(child, index, items, chrome);

            items[index] = items[index].WithSpan(items.Count - index - 1);
        }

        static ChatItemChrome ChromeFor(Node node) => node.Kind switch
        {
            DisplayItemKind.Section => new ChatItemChrome(null, 0, 0, node.ToggleEnabled, node.Expanded),
            DisplayItemKind.ReasoningSequence => new ChatItemChrome(node.Title, 0, 0, false, node.Expanded),
            DisplayItemKind.SubagentGroup => new ChatItemChrome(node.Title, node.CompletedCount, node.TotalCount, false,
                node.Expanded, node.SpawnState),
            // A block draws no chrome of its own, apart from the answer's foldout over its sources.
            DisplayItemKind.Block => new ChatItemChrome(null, 0, 0, false, node.Expanded),
            _ => default
        };

        class Node
        {
            public readonly DisplayItemKind Kind;
            public readonly Type ElementType;
            public readonly int MessageIndex;
            public readonly int BlockIndex;
            public readonly ContextSignature Context;
            public readonly ContentKey Identity;
            public readonly List<Node> Children;
            public ContentKey Content;
            public GroupFooterStyle FooterStyle;
            public bool Collapsed;
            public bool Expanded;

            // Reasoning sequences read HasPendingCalls back (the break rule), own one header per
            // subagent and hold the spawn calls made in them until they close; a subagent header counts
            // the calls under it that are done and carries the outcome of the call that spawned it.
            public bool HasPendingCalls;
            public int CompletedCount;
            public int TotalCount;
            public bool ToggleEnabled;
            public string Title;
            public Dictionary<string, Node> AgentHeaders;
            public List<(string Role, SpawnCallState State)> SpawnCalls;
            public SpawnCallState SpawnState;

            public Node(DisplayItemKind kind, Type elementType, int messageIndex, int blockIndex, ContextSignature context,
                ContentKey identity, ContentKey content, bool container)
            {
                Kind = kind;
                ElementType = elementType;
                MessageIndex = messageIndex;
                BlockIndex = blockIndex;
                Context = context;
                Identity = identity;
                Content = content;
                Children = container ? new List<Node>() : null;
            }

            public void Add(Node block, bool isPending)
            {
                Children.Add(block);
                HasPendingCalls |= isPending;
            }

            /// <summary>Collapsing prunes the stream; the same flag is what a foldout binds to.</summary>
            public void SetExpanded(bool expanded)
            {
                Expanded = expanded;
                Collapsed = !expanded;
            }
        }
    }
}
