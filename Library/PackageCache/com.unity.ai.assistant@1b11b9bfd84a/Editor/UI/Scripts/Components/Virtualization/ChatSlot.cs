using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// Pool bucket identity. The context signature and the footer style are part of the key because a
    /// rented slot must already carry the wrapper chain and the decoration its content needs: binding
    /// never changes the hierarchy inside a slot.
    /// </summary>
    readonly struct SlotKey : IEquatable<SlotKey>
    {
        public SlotKey(DisplayItemKind kind, Type elementType, ContextSignature context, GroupFooterStyle footerStyle)
        {
            Kind = kind;
            ElementType = elementType;
            Context = context;
            FooterStyle = footerStyle;
        }

        public static SlotKey For(in DisplayItem item) => new(item.Kind, item.ElementType, item.Context, item.FooterStyle);

        public DisplayItemKind Kind { get; }

        public Type ElementType { get; }

        public ContextSignature Context { get; }

        public GroupFooterStyle FooterStyle { get; }

        public bool Equals(SlotKey other)
            => Kind == other.Kind
                && ElementType == other.ElementType
                && Context == other.Context
                && FooterStyle == other.FooterStyle;

        public override bool Equals(object obj) => obj is SlotKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine((int)Kind, ElementType, (int)Context, (int)FooterStyle);

        public override string ToString() => Kind + "/" + (ElementType?.Name ?? "none") + "/" + Context + "/" + FooterStyle;

        public static bool operator ==(SlotKey left, SlotKey right) => left.Equals(right);

        public static bool operator !=(SlotKey left, SlotKey right) => !left.Equals(right);
    }

    /// <summary>
    /// One pooled, absolutely positioned cell of the content layer. A slot joins the content layer once
    /// and stays in it: a realize pass writes its top, toggles its display and its visibility, and never
    /// re-parents it, so nothing inside a slot sees an attach or a detach because the window moved.
    /// Slots start hidden — an item is only revealed once it has been positioned from a measured height.
    /// </summary>
    class ChatSlot : VisualElement, IChatHeightChangeHost
    {
        /// <summary>Top for a slot that is kept alive off-window; the viewport clips it.</summary>
        public const float ParkedTop = -1000000f;

        const string k_SlotClass = "mui-chat-surface-slot";
        const string k_SlotInsetClass = "mui-chat-surface-slot-inset";
        const string k_AnswerContentName = "answerContent";
        const string k_ReasoningContentName = "reasoningContent";
        const string k_SubagentContentName = "subagentContent";
        const string k_TextAreaClass = "mui-chat-element-text-area";
        const string k_ChatRootClass = "mui-chat-root";
        const string k_ChatResponseClass = "mui-chat-response";
        const string k_SequenceContentClass = "mui-reasoning-sequence-content";
        const string k_SubagentContentClass = "subagent-content";

        readonly VisualElement m_Host;

        float m_Top = float.NaN;
        bool m_Visible;

        public ChatSlot(SlotKey key)
        {
            Key = key;
            BoundIndex = DisplayItem.NoIndex;
            pickingMode = PickingMode.Ignore;
            usageHints = UsageHints.GroupTransform;

            AddToClassList(k_SlotClass);
            if (IsInset(key))
                AddToClassList(k_SlotInsetClass);

            // Left and right stay in USS: an inline write would override .mui-chat-surface-slot-inset.
            style.position = Position.Absolute;
            style.visibility = Visibility.Hidden;

            m_Host = BuildContextChain(key.Context, this);
        }

        public SlotKey Key { get; }

        /// <summary>Item this slot currently renders, or <see cref="DisplayItem.NoIndex"/> when free.</summary>
        public int BoundIndex { get; set; }

        /// <summary>Content key of the bound item, so a rebind can tell a height change from a no-op.</summary>
        public ContentKey BoundContent { get; set; }

        /// <summary>Content key this slot's element did not survive being given, kept so a pass does not
        /// hand it the same thing again. Cleared by a bind that comes back and by a return to the pool.</summary>
        public ContentKey FailedContent { get; set; }

        /// <summary>Whether the element already failed on <paramref name="content"/>, in which case it
        /// still renders what it held before it and must neither be rebound nor revealed.</summary>
        public bool FailedToBind(ContentKey content) => content != default && FailedContent == content;

        public VisualElement Content { get; private set; }

        public bool IsSealed { get; private set; }

        public float Top => m_Top;

        public bool IsVisible => m_Visible;

        /// <summary>Whether the slot renders an item at a position inside the content layer. A free slot
        /// and a parked one both keep the text they last held, and neither puts it on screen.</summary>
        public bool IsShowingContent => BoundIndex != DisplayItem.NoIndex && m_Top > ParkedTop;

        /// <summary>Raised when a foldout anywhere inside the slot changed how much room its content
        /// needs, with the new expansion state when the toggle also prunes the display stream.</summary>
        public event Action<ChatSlot, bool?> HeightChanged;

        public void OnContentHeightChanged(bool? expanded) => HeightChanged?.Invoke(this, expanded);

        /// <summary>
        /// Puts the element this slot renders inside the context chain. Called once per slot, ever: the
        /// hierarchy inside a slot is fixed for its whole pooled life.
        /// </summary>
        public void Attach(VisualElement content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            if (Content != null)
                throw new InvalidOperationException("A slot's content is attached once; " + Key + " is already attached.");

            Content = content;
            m_Host.Add(content);
        }

        public void SetTop(float top)
        {
            if (Mathf.Approximately(m_Top, top))
                return;

            m_Top = top;
            style.top = top;
        }

        public void SetVisible(bool visible)
        {
            if (m_Visible == visible)
                return;

            m_Visible = visible;
            style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
        }

        public void Park() => SetTop(ParkedTop);

        /// <summary>
        /// Pins the slot to the height it just laid out at. A definite height, hidden overflow and the
        /// group-transform hint together are the only regime measured flat (F8); the height can only come
        /// from a completed layout pass, so a slot is sealed the pass after it is filled.
        /// </summary>
        public void Seal()
        {
            if (IsSealed)
                return;

            var height = layout.height;
            if (float.IsNaN(height) || height <= 0f)
                return;

            // Ceil, not the raw float: hidden overflow on a height that rounds down clips a descender.
            style.height = Mathf.Ceil(height);
            style.overflow = Overflow.Hidden;
            IsSealed = true;
        }

        public void Unseal()
        {
            if (!IsSealed)
                return;

            style.height = StyleKeyword.Null;
            style.overflow = Overflow.Visible;
            IsSealed = false;
        }

        /// <summary>
        /// The content of the slots under <paramref name="container"/> that are showing an item, in the
        /// order a reader meets them. Slots join the content layer in the order the pool first built them
        /// and never leave it, so neither the tree order nor the whole tree describes what is on screen.
        /// </summary>
        public static List<VisualElement> ShownContentInReadingOrder(VisualElement container)
        {
            var shown = new List<ChatSlot>();

            foreach (var child in container.Children())
            {
                if (child is ChatSlot { IsShowingContent: true, Content: not null } slot)
                    shown.Add(slot);
            }

            shown.Sort(CompareBoundItem);

            var roots = new List<VisualElement>(shown.Count);

            foreach (var slot in shown)
                roots.Add(slot.Content);

            return roots;
        }

        static int CompareBoundItem(ChatSlot left, ChatSlot right) => left.BoundIndex.CompareTo(right.BoundIndex);

        /// <summary>
        /// Reproduces, inside the slot, the ancestor names and classes the authored USS rules are scoped
        /// to, and returns the element the content belongs in. Element names are not unique in UI Toolkit,
        /// so a rule like `#answerContent .mui-chat-response .unity-text-element` applies verbatim.
        /// </summary>
        static VisualElement BuildContextChain(ContextSignature context, VisualElement root)
        {
            switch (context)
            {
                case ContextSignature.None:
                    return root;
                case ContextSignature.AnswerContent:
                    return AddResponseArea(root, k_AnswerContentName);
                case ContextSignature.ReasoningContent:
                    return AddResponseArea(root, k_ReasoningContentName);
                case ContextSignature.SequenceContent:
                    return AddWrapper(root, k_ReasoningContentName, k_SequenceContentClass);
                case ContextSignature.SubagentContent:
                    return AddWrapper(root, k_SubagentContentName, k_SubagentContentClass);
                default:
                    throw new ArgumentOutOfRangeException(nameof(context), context, "Unknown context signature.");
            }
        }

        static VisualElement AddResponseArea(VisualElement root, string name)
        {
            var area = AddWrapper(root, name, k_TextAreaClass);
            var response = AddWrapper(area, string.Empty, k_ChatRootClass);
            response.AddToClassList(k_ChatResponseClass);
            return response;
        }

        static VisualElement AddWrapper(VisualElement parent, string name, string className)
        {
            var wrapper = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            wrapper.AddToClassList(className);
            parent.Add(wrapper);
            return wrapper;
        }

        // A message's items are siblings in the content layer rather than children of its frame, so the
        // horizontal inset an assistant message is drawn with has to be carried by each of them.
        static bool IsInset(SlotKey key) => key.Kind switch
        {
            DisplayItemKind.MessageFrame => true,
            DisplayItemKind.Section => true,
            DisplayItemKind.ReasoningGroup => true,
            DisplayItemKind.ReasoningSequence => true,
            DisplayItemKind.SubagentGroup => true,
            DisplayItemKind.Thought => true,
            DisplayItemKind.Block => true,
            DisplayItemKind.CompletedActions => true,
            DisplayItemKind.FeedbackFooter => true,
            DisplayItemKind.GroupFooter => key.FooterStyle != GroupFooterStyle.Message,
            _ => false
        };
    }
}
