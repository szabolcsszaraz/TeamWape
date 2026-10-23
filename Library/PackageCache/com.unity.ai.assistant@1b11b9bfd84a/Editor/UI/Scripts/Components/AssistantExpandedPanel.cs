using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class AssistantExpandedPanel : ManagedTemplate
    {
        ScrollView m_Content;
        VisualElement m_FillHost;
        VisualElement m_FillElement;

        public AssistantExpandedPanel() : base(AssistantUIConstants.UIModulePath) { }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Content = view.Q<ScrollView>("expandedPanelContent");
            m_FillHost = m_Content.parent;
            m_Content.mode = ScrollViewMode.VerticalAndHorizontal;
            m_Content.verticalScrollerVisibility = ScrollerVisibility.Auto;
            m_Content.horizontalScrollerVisibility = ScrollerVisibility.Auto;
        }

        internal bool IsVisible => resolvedStyle.display != DisplayStyle.None;

        internal void ShowPanel(VisualElement element, ScrollViewMode scrollMode = ScrollViewMode.VerticalAndHorizontal)
        {
            m_Content.mode = scrollMode;
            m_Content.Clear();
            m_Content.Add(element);
            this.SetDisplay(true);
        }

        /// <summary>
        /// Shows content that scrolls itself. It replaces the panel's scroll view rather than sitting
        /// inside it: a viewport of its own needs a height to fill, and inside a scroll view it would only
        /// ever be given the height of what it is trying to clip.
        /// </summary>
        internal void ShowFillPanel(VisualElement element)
        {
            m_Content.Clear();
            m_Content.SetDisplay(false);

            element.style.flexGrow = 1f;
            m_FillHost.Add(element);
            m_FillElement = element;
            this.SetDisplay(true);
        }

        internal void HidePanel()
        {
            this.SetDisplay(false);
            m_Content.Clear();
            m_Content.SetDisplay(true);

            if (m_FillElement == null)
                return;

            m_FillHost.Remove(m_FillElement);
            m_FillElement = null;
        }
    }
}
