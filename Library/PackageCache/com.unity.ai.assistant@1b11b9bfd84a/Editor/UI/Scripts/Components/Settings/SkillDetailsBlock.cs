using System.Collections.Generic;
using Unity.AI.Assistant.Skills;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class SkillDetailsBlock : ManagedTemplate
    {
        VisualElement m_PathContainer;
        VisualElement m_DescriptionSection;
        Label m_DescriptionValue;
        VisualElement m_PackagesSection;
        VisualElement m_PackagesContainer;
        VisualElement m_EditorVersionSection;
        Label m_EditorVersionValue;
        VisualElement m_ToolsSection;
        VisualElement m_ToolsContainer;
        VisualElement m_ResourcesSection;
        VisualElement m_ResourcesContainer;
        VisualElement m_WarningsContainer;

        public SkillDetailsBlock() : base(AssistantUIConstants.UIModulePath) { }

        protected override void InitializeView(TemplateContainer view)
        {
            m_PathContainer = view.Q<VisualElement>("pathContainer");
            m_DescriptionSection = view.Q<VisualElement>("descriptionSection");
            m_DescriptionValue = view.Q<Label>("descriptionValue");
            m_PackagesSection = view.Q<VisualElement>("packagesSection");
            m_PackagesContainer = view.Q<VisualElement>("packagesContainer");
            m_EditorVersionSection = view.Q<VisualElement>("editorVersionSection");
            m_EditorVersionValue = view.Q<Label>("editorVersionValue");
            m_ToolsSection = view.Q<VisualElement>("toolsSection");
            m_ToolsContainer = view.Q<VisualElement>("toolsContainer");
            m_ResourcesSection = view.Q<VisualElement>("resourcesSection");
            m_ResourcesContainer = view.Q<VisualElement>("resourcesContainer");
            m_WarningsContainer = view.Q<VisualElement>("warningsContainer");

            m_DescriptionSection.SetDisplay(false);
            m_PackagesSection.SetDisplay(false);
            m_EditorVersionSection.SetDisplay(false);
            m_ToolsSection.SetDisplay(false);
            m_ResourcesSection.SetDisplay(false);
        }

        public void SetData(SkillDefinition skill, string displayPath, string unknownFieldsWarning = null)
        {
            if (string.IsNullOrEmpty(skill.Path))
            {
                m_PathContainer.Add(new Label("(no path)"));
            }
            else
            {
                var folderRow = new SkillFolderRow();
                folderRow.Initialize(Context);
                folderRow.SetData("File location", displayPath, skill.Path);
                m_PathContainer.Add(folderRow);
            }

            if (!string.IsNullOrEmpty(skill.MetaData.Description))
            {
                m_DescriptionSection.SetDisplay(true);
                m_DescriptionValue.text = skill.MetaData.Description;
            }

            if (skill.MetaData.RequiredPackages?.Count > 0)
            {
                m_PackagesSection.SetDisplay(true);
                foreach (var package in skill.MetaData.RequiredPackages)
                    m_PackagesContainer.Add(new Label($"{package.Key}: {package.Value}"));
            }

            if (!string.IsNullOrEmpty(skill.MetaData.RequiredEditorVersion))
            {
                m_EditorVersionSection.SetDisplay(true);
                m_EditorVersionValue.text = skill.MetaData.RequiredEditorVersion;
            }

            if (skill.MetaData.Tools?.Count > 0)
            {
                m_ToolsSection.SetDisplay(true);
                foreach (var toolName in skill.MetaData.Tools)
                    m_ToolsContainer.Add(new Label(toolName));
            }

            if (skill.Resources?.Count > 0)
            {
                m_ResourcesSection.SetDisplay(true);
                foreach (var resource in skill.Resources)
                    m_ResourcesContainer.Add(new Label($"{resource.Key} ({resource.Value.Length})"));
            }

            if (unknownFieldsWarning != null)
            {
                var warningBox = new SkillWarningBox();
                warningBox.Initialize(Context);
                warningBox.SetData(unknownFieldsWarning, isInfo: true);
                m_WarningsContainer.Add(warningBox);
            }
        }
    }
}
