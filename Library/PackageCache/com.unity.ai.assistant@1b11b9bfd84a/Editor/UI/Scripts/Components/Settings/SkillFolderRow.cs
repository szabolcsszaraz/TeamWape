using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class SkillFolderRow : ManagedTemplate
    {
        Label m_Label;
        TextField m_PathField;
        string m_RawPath;

        public SkillFolderRow() : base(AssistantUIConstants.UIModulePath) { }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Label = view.Q<Label>("folderLabel");
            m_PathField = view.Q<TextField>("folderPath");
            m_PathField.SetEnabled(false);
            view.SetupButton("browseButton", _ => EditorUtility.RevealInFinder(m_RawPath));
        }

        public void SetData(string labelText, string displayPath, string rawPath)
        {
            m_RawPath = rawPath;
            m_Label.text = labelText;
            m_PathField.value = displayPath;
        }
    }
}
