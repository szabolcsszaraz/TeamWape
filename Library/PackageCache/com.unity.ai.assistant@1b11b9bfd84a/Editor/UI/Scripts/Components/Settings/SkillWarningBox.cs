using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class SkillWarningBox : ManagedTemplate
    {
        Image m_Icon;
        Label m_Message;
        VisualElement m_Container;
        string m_CurrentIconClass;

        public SkillWarningBox() : base(AssistantUIConstants.UIModulePath) { }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Container = view.Q<VisualElement>("warningContainer");
            m_Icon = view.Q<Image>("warningIcon");
            m_Message = view.Q<Label>("warningMessage");
        }

        public void SetData(string message, bool isInfo, string tooltip = null)
        {
            if (m_CurrentIconClass != null)
                m_Icon.RemoveFromClassList(m_CurrentIconClass);
            m_CurrentIconClass = isInfo ? "mui-icon-info" : "mui-icon-warn-large";
            m_Icon.AddToClassList(m_CurrentIconClass);
            m_Message.text = message;
            if (tooltip != null)
                m_Container.tooltip = tooltip;
        }
    }
}
