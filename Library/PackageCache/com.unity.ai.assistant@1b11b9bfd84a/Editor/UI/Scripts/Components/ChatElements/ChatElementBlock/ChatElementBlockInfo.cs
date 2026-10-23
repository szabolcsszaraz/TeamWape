using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    /// <summary>
    /// Renders an <see cref="InfoBlockModel"/> — a non-error notice that should appear in the
    /// conversation flow but must not be styled as a failure (e.g. server graceful disconnect).
    /// </summary>
    class ChatElementBlockInfo : ChatElementBlockBase<InfoBlockModel>
    {
        Label m_InfoText;

        protected override void InitializeView(TemplateContainer view)
        {
            base.InitializeView(view);

            m_InfoText = view.Q<Label>("infoText");
            m_InfoText.selection.isSelectable = true;
        }

        protected override void OnBlockModelChanged()
        {
            m_InfoText.text = BlockModel.Message;
        }
    }
}
