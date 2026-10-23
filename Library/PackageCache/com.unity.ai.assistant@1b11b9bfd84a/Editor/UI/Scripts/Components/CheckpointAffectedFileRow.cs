using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class CheckpointAffectedFileRow : ManagedTemplate
    {
        Image m_Icon;
        Label m_Path;
        string m_IconClass;

        public CheckpointAffectedFileRow() : base(AssistantUIConstants.UIModulePath) { }

        public void SetData(string path, string iconClass)
        {
            m_Path.text = path;

            if (!string.IsNullOrEmpty(m_IconClass))
            {
                m_Icon.RemoveFromClassList(m_IconClass);
            }

            m_IconClass = iconClass;

            if (!string.IsNullOrEmpty(iconClass))
            {
                m_Icon.AddToClassList(iconClass);
            }
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Icon = view.Q<Image>("rowIcon");
            m_Path = view.Q<Label>("rowPath");
        }
    }
}
