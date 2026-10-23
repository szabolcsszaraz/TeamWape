using System;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    enum DisplayItemKind
    {
        TopPadding,
        UserPrompt,
        Checkpoint,
        RevertedLink,
        MessageFrame,
        Section,
        ReasoningGroup,
        ReasoningSequence,
        SubagentGroup,
        Thought,
        Block,
        CompletedActions,
        FeedbackFooter,
        GroupFooter
    }

    /// <summary>
    /// The ancestor chain a slot reproduces inside itself so the authored USS rules scoped to
    /// #answerContent, #reasoningContent and #subagentContent keep applying to a flattened item.
    /// </summary>
    enum ContextSignature
    {
        None,
        AnswerContent,
        ReasoningContent,
        SequenceContent,
        SubagentContent
    }

    /// <summary>
    /// The box geometry a container draws below its children, which flattening would otherwise drop.
    /// Each value selects one class from ChatScrollSurface.uss.
    /// </summary>
    enum GroupFooterStyle
    {
        None,
        Message,
        ReasoningGroup,
        ReasoningSequence,
        ReasoningSeparator
    }

    /// <summary>
    /// One entry of the flattened conversation. Containers precede their descendants and cover them
    /// with <see cref="Span"/>, so a window of items can be extended to the chrome it needs by walking
    /// <see cref="Parent"/> upwards.
    /// </summary>
    readonly struct DisplayItem
    {
        public const int NoIndex = -1;

        public DisplayItem(
            DisplayItemKind kind,
            int parent,
            int messageIndex,
            int blockIndex,
            Type elementType,
            ContextSignature context,
            GroupFooterStyle footerStyle,
            ContentKey identity,
            ContentKey content,
            int span = 0)
        {
            Kind = kind;
            Parent = parent;
            MessageIndex = messageIndex;
            BlockIndex = blockIndex;
            ElementType = elementType;
            Context = context;
            FooterStyle = footerStyle;
            Identity = identity;
            Content = content;
            Span = span;
        }

        public DisplayItemKind Kind { get; }

        /// <summary>Index of the owning container, or <see cref="NoIndex"/> for a root item.</summary>
        public int Parent { get; }

        /// <summary>Number of items that follow this one and belong to it. Zero for a leaf, and zero
        /// for a collapsed container: collapsing prunes the stream rather than hiding elements.</summary>
        public int Span { get; }

        public int MessageIndex { get; }

        /// <summary>Index into the message's block list, or <see cref="NoIndex"/> for chrome.</summary>
        public int BlockIndex { get; }

        /// <summary>Element to build for this item; null for a container with no element of its own.</summary>
        public Type ElementType { get; }

        /// <summary>Wrapper chain the slot holding this item must provide. Part of the pool key.</summary>
        public ContextSignature Context { get; }

        /// <summary>Which decoration a <see cref="DisplayItemKind.GroupFooter"/> draws; unset otherwise.</summary>
        public GroupFooterStyle FooterStyle { get; }

        /// <summary>Structural key. Stable while content changes, so it rebases the scroll anchor
        /// after a re-flatten and keys per-item view state.</summary>
        public ContentKey Identity { get; }

        /// <summary>Identity plus everything that changes rendered height. Keys the height cache.</summary>
        public ContentKey Content { get; }

        public bool IsContainer => IsContainerKind(Kind);

        public static bool IsContainerKind(DisplayItemKind kind) => kind switch
        {
            DisplayItemKind.MessageFrame => true,
            DisplayItemKind.Section => true,
            DisplayItemKind.ReasoningGroup => true,
            DisplayItemKind.ReasoningSequence => true,
            DisplayItemKind.SubagentGroup => true,
            _ => false
        };

        public DisplayItem WithSpan(int span)
            => new(Kind, Parent, MessageIndex, BlockIndex, ElementType, Context, FooterStyle, Identity, Content, span);
    }
}
