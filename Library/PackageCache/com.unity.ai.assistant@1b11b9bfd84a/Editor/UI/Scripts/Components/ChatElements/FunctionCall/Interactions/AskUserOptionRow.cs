using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class AskUserOptionRow : ManagedTemplate
    {
        VisualElement m_Row;
        VisualElement m_OptionTextColumn;
        Label m_OptionLabel;
        Label m_OptionDescription;

        public AskUserOptionRow()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Row = view.Q<VisualElement>("optionRow");
            m_OptionTextColumn = view.Q<VisualElement>(className: "ask-user-option-text-column");
            m_OptionLabel = view.Q<Label>("optionLabel");
            m_OptionDescription = view.Q<Label>("optionDescription");
            m_OptionDescription.SetDisplay(false);
        }

        public void SetData(VisualElement indicator, string label, string description = null)
        {
            m_Row.Insert(0, indicator);
            m_OptionLabel.text = label;
            m_OptionDescription.text = description ?? "";
            m_OptionDescription.SetDisplay(!string.IsNullOrEmpty(description));
        }

        public void SetData(VisualElement indicator, VisualElement content)
        {
            m_OptionTextColumn.RemoveFromHierarchy();
            m_Row.Insert(0, indicator);
            m_Row.Add(content);
        }
    }
}
