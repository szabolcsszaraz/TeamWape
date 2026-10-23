using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    /// <summary>
    /// Builds the shared "Other" row used in both single-select and multi-select AskUser questions.
    /// The row contains an indicator (Toggle or RadioButton), a text field with placeholder behaviour,
    /// and a Save button.
    /// </summary>
    class AskUserOtherRow
    {
        const string k_PlaceholderClass = "ask-user-field-placeholder";

        readonly string m_Placeholder;

        public AskUserOptionRow Root { get; }
        public TextField Field { get; }
        public Button SaveButton { get; }

        public bool IsPlaceholder => Field.ClassListContains(k_PlaceholderClass);

        public AskUserOtherRow(AskUserOptionRow root, VisualElement indicator, string placeholder)
        {
            m_Placeholder = placeholder;

            Field = new TextField { multiline = true, verticalScrollerVisibility = ScrollerVisibility.Auto };
            Field.AddToClassList("ask-user-other-inline-field");

            SaveButton = new Button { text = "Save" };
            SaveButton.AddToClassList("ask-user-other-save-btn");
            SaveButton.SetDisplay(false);

            SetPlaceholder();
            Field.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (string.IsNullOrWhiteSpace(Field.value)) SetPlaceholder();
            });

            var fieldRow = new VisualElement();
            fieldRow.AddToClassList("ask-user-other-field-row");
            fieldRow.Add(Field);
            fieldRow.Add(SaveButton);

            Root = root;
            Root.SetData(indicator, fieldRow);
        }

        public void SetPlaceholder()
        {
            Field.SetValueWithoutNotify(m_Placeholder);
            Field.AddToClassList(k_PlaceholderClass);
        }

        public void ClearPlaceholder()
        {
            Field.value = "";
            Field.RemoveFromClassList(k_PlaceholderClass);
        }

        public void ResetSaveButton()
        {
            SaveButton.text = "Save";
            SaveButton.RemoveFromClassList("ask-user-save-btn--saved");
        }

        public void MarkSaved()
        {
            SaveButton.text = "Saved";
            SaveButton.AddToClassList("ask-user-save-btn--saved");
        }
    }
}
