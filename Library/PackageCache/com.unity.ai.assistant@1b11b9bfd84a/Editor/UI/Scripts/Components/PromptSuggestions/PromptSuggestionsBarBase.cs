using System;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.PromptSuggestions
{
    abstract class PromptSuggestionsBarBase : ManagedTemplate
    {
        readonly TabData[] m_VisibleTabs;
        protected Label[] m_TabButtons;
        protected int m_ActiveTabIndex = -1;
        public event Action<string> PromptSelected;
        
        protected PromptSuggestionsBarBase(TabData[] visibleTabs) : base(AssistantUIConstants.UIModulePath)
        {
            m_VisibleTabs = visibleTabs;
            foreach (var tab in m_VisibleTabs)
            {
                tab.Collapse = Collapse;
                tab.OnPromptSelected = text => PromptSelected?.Invoke(text);
            }
        }
        
        public override void Initialize(AssistantUIContext context, bool autoShowControl = true)
        {
            base.Initialize(context, autoShowControl);
            foreach (var tab in m_VisibleTabs)
                tab.Context = Context;
        }

        internal abstract void Collapse();

        protected void RefreshButtonStyles(string activeClass)
        {
            for (var i = 0; i < m_TabButtons.Length; i++)
                m_TabButtons[i].EnableInClassList(activeClass, i == m_ActiveTabIndex);
        }

        protected void BuildButtons(VisualElement container, string cssClass, Action<int> onSelected)
        {
            m_TabButtons = new Label[m_VisibleTabs.Length];

            for (var i = 0; i < m_VisibleTabs.Length; i++)
            {
                var index = i;
                var button = new Label(m_VisibleTabs[i].Label);
                button.AddToClassList(cssClass);
                button.AddManipulator(new Clickable(() => onSelected(index)));
                container.Add(button);
                m_TabButtons[i] = button;
            }
        }

        protected void RefreshPromptList(VisualElement container)
        {
            container.Clear();
            if (m_ActiveTabIndex < 0 || m_ActiveTabIndex >= m_VisibleTabs.Length) return;
            
            var tab = m_VisibleTabs[m_ActiveTabIndex];
            tab.BuildContent(container);
        }
    }
}
