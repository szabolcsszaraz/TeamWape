using System;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// The box geometry a container used to draw below its children, carried as its own display item so
    /// flattening does not drop it. Everything it draws comes from its class in ChatScrollSurface.uss.
    /// </summary>
    class ChatGroupFooter : VisualElement
    {
        const string k_MessageClass = "mui-chat-surface-footer-message";
        const string k_ReasoningGroupClass = "mui-chat-surface-footer-reasoning-group";
        const string k_ReasoningSequenceClass = "mui-chat-surface-footer-reasoning-sequence";
        const string k_ReasoningSeparatorClass = "mui-chat-surface-footer-reasoning-separator";

        public ChatGroupFooter(GroupFooterStyle footerStyle)
        {
            FooterStyle = footerStyle;
            pickingMode = PickingMode.Ignore;
            AddToClassList(ClassFor(footerStyle));
        }

        public GroupFooterStyle FooterStyle { get; }

        static string ClassFor(GroupFooterStyle footerStyle) => footerStyle switch
        {
            GroupFooterStyle.Message => k_MessageClass,
            GroupFooterStyle.ReasoningGroup => k_ReasoningGroupClass,
            GroupFooterStyle.ReasoningSequence => k_ReasoningSequenceClass,
            GroupFooterStyle.ReasoningSeparator => k_ReasoningSeparatorClass,
            _ => throw new ArgumentOutOfRangeException(nameof(footerStyle), footerStyle, "No footer decoration exists for this style.")
        };
    }
}
