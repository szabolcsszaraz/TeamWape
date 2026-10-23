using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Skills;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UpmPackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    [UxmlElement]
    partial class AssistantSkillsSettingsView : ManagedTemplate
    {
        struct PackagePathEntry
        {
            public string NormalizedResolved;
            public string Name;
        }

        const string k_MajorWarning = "Major issue: The skill was not registered. See the message for details.";
        const string k_MinorWarning = "Minor issue: The skill is registered and can be used. See the message for details.";

        Label m_SkillsTimestampLabel;
        IVisualElementScheduledItem m_TimestampRefreshSchedule;
        IVisualElementScheduledItem m_PendingScanRefreshSchedule;

        VisualElement m_SkillsIssuesSection;
        VisualElement m_SkillsIssuesContainer;
        ToolbarSearchField m_SkillsSearchField;

        VisualElement m_ProjectSkillsContainer;
        VisualElement m_UserSkillsContainer;
        VisualElement m_PackageSkillsSection;
        VisualElement m_PackageSkillsContainer;
        VisualElement m_InternalSkillsSection;
        VisualElement m_InternalSkillsContainer;
        
        List<SkillFileIssue> m_MinorIssues = new();
        List<PackagePathEntry> m_CachedPackagePaths;

        public AssistantSkillsSettingsView() : base(AssistantUIConstants.UIModulePath)
        {
            RegisterAttachEvents(OnAttach, OnDetach);
        }

        protected override void InitializeView(TemplateContainer view)
        {
            LoadStyle(view, "AssistantSkillsSettingsView");
            LoadStyle(view, EditorGUIUtility.isProSkin
                ? AssistantUIConstants.AssistantSharedStyleDark
                : AssistantUIConstants.AssistantSharedStyleLight);
            LoadStyle(view, AssistantUIConstants.AssistantBaseStyle, true);
            
            m_SkillsTimestampLabel = view.Q<Label>("skillsTimestampLabel");

            var infoRow = view.Q<VisualElement>(className: "assistant-skills-info-row");
            var rescanButton = new Button(SkillsScanner.ForceRescan) { text = "Rescan" };
            rescanButton.AddToClassList("assistant-skills-rescan-button");
            infoRow.Insert(0, rescanButton);

            var searchContainer = view.Q<VisualElement>("skillsSearchContainer");

            m_SkillsSearchField = new ToolbarSearchField();
            m_SkillsSearchField.AddToClassList("assistant-skills-search");
            m_SkillsSearchField.RegisterValueChangedCallback(_ => RefreshSkillsList());

            var innerTextField = m_SkillsSearchField.Q<TextField>();
            if (innerTextField != null)
            {
                innerTextField.textEdition.placeholder = "Filter skills by name";
            }

            searchContainer.Add(m_SkillsSearchField);
            
            var notifyToggle = view.Q<Toggle>("notifyToggle");
            notifyToggle.value = AssistantEditorPreferences.NotifyOnNewSkills;
            notifyToggle.RegisterValueChangedCallback(evt =>
                AssistantEditorPreferences.NotifyOnNewSkills = evt.newValue);

            m_SkillsIssuesSection = view.Q<VisualElement>("skillsListOuter");
            m_SkillsIssuesContainer = view.Q<VisualElement>("skillsIssuesContainer");
            
            m_ProjectSkillsContainer = view.Q<VisualElement>("skillsContainerProject");
            m_UserSkillsContainer = view.Q<VisualElement>("skillsContainerUser");
            m_PackageSkillsSection = view.Q<VisualElement>("skillsPackageSection");
            m_PackageSkillsContainer = view.Q<VisualElement>("skillsContainerPackage");
            m_InternalSkillsSection = view.Q<VisualElement>("skillsInternalSection");
            m_InternalSkillsContainer = view.Q<VisualElement>("skillsContainerInternal");

            Debug.Assert(m_ProjectSkillsContainer != null, "Proj was null");
            Debug.Assert(m_UserSkillsContainer != null, "User was null");
        }

        void OnAttach(AttachToPanelEvent evt)
        {
            SkillsScanner.OnSkillsRescanned += OnSkillsRescanned;
            AssistantEditorPreferences.SkillAllowedStateChanged += RefreshSkillsList;
            OnSkillsRescanned();
            m_TimestampRefreshSchedule = schedule.Execute(UpdateTimestampLabel).Every(60000);
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            SkillsScanner.OnSkillsRescanned -= OnSkillsRescanned;
            AssistantEditorPreferences.SkillAllowedStateChanged -= RefreshSkillsList;
            m_TimestampRefreshSchedule?.Pause();
            m_TimestampRefreshSchedule = null;
            m_PendingScanRefreshSchedule?.Pause();
            m_PendingScanRefreshSchedule = null;
        }

        void OnSkillsRescanned()
        {
            m_CachedPackagePaths = null;
            RefreshSkillsList();
        }

        static List<PackagePathEntry> BuildPackagePathCache()
        {
            var result = new List<PackagePathEntry>();
            foreach (var pkg in UpmPackageInfo.GetAllRegisteredPackages())
            {
                if (string.IsNullOrEmpty(pkg.resolvedPath)) continue;
                result.Add(new PackagePathEntry
                {
                    NormalizedResolved = pkg.resolvedPath.Replace('\\', '/').TrimEnd('/'),
                    Name = pkg.name
                });
            }
            return result;
        }

        void RefreshSkillsList()
        {
            m_PendingScanRefreshSchedule?.Pause();
            m_PendingScanRefreshSchedule = null;

            string filter = m_SkillsSearchField?.value?.ToLowerInvariant() ?? "";

            m_ProjectSkillsContainer.Clear();
            m_UserSkillsContainer.Clear();
            m_PackageSkillsContainer.Clear();
            m_InternalSkillsContainer.Clear();
            m_MinorIssues.Clear();

            var errors = SkillsScanner.LoadResults.SkillParsingIssues;

            var allSkills = SkillsRegistry.GetAllSkillsNoWait();
            var skills = new List<SkillDefinition>();
            foreach (var skill in allSkills)
            {
                if (GetSourceTag(skill.Tags) != string.Empty)
                    skills.Add(skill);
            }

            var timeoutIssues = SkillsScanner.TimeoutIssues;

            var duplicateIssues = SkillsRegistry.GetDuplicateIssues();

            UpdateTimestampLabel();
            BuildSkillsFoldouts(skills, filter);
            BuildIssuesFoldouts(errors, filter, timeoutIssues, duplicateIssues);

            // Poll until the scan completes in case OnSkillsRescanned fired before we subscribed.
            if (!SkillsRegistry.IsLoadComplete)
                m_PendingScanRefreshSchedule = schedule.Execute(RefreshSkillsList).StartingIn(500);
        }

        void UpdateTimestampLabel()
        {
            if (m_SkillsTimestampLabel == null || SkillsScanner.LastRescanTime == default)
                return;
            var minutesAgo = (int)(DateTime.Now - SkillsScanner.LastRescanTime).TotalMinutes;
            string agoText = minutesAgo == 0 ? "just now" : $"{minutesAgo} min ago";
            m_SkillsTimestampLabel.text = $"Last scanned {agoText}";
        }

        void BuildSkillsFoldouts(List<SkillDefinition> skills, string filter)
        {
            var filteredSkills = new List<SkillDefinition>();
            foreach (var skill in skills)
            {
                if (string.IsNullOrEmpty(filter) ||
                    skill.MetaData.Name.ToLowerInvariant().Contains(filter))
                {
                    filteredSkills.Add(skill);
                }
            }

            filteredSkills.Sort((a, b) => string.Compare(a.MetaData.Name, b.MetaData.Name, StringComparison.OrdinalIgnoreCase));

            var projectSkills = new List<SkillDefinition>();
            var userSkills = new List<SkillDefinition>();
            var packageSkills = new List<SkillDefinition>();
            var internalSkills = new List<SkillDefinition>();
            foreach (var skill in filteredSkills)
            {
                var tag = GetSourceTag(skill.Tags);
                if (tag == SkillRegistryTags.User)
                    userSkills.Add(skill);
                else if (tag == SkillRegistryTags.Package)
                    packageSkills.Add(skill);
                else if (SkillRegistryTags.IsInternalOrBuiltIn(tag))
                    internalSkills.Add(skill);
                else
                    projectSkills.Add(skill);
            }

            AddFolderRow(m_ProjectSkillsContainer, "Skills location", "Assets", addSeparator: true);
            PopulateSkillGroup(m_ProjectSkillsContainer, projectSkills);
            
            if (Directory.Exists(SkillsScanner.UserAppDataFolder))
            {
                AddFolderRow(m_UserSkillsContainer, "Skills location", SkillsScanner.UserAppDataFolder, showFullUserPath: true, addSeparator: true);
                PopulateSkillGroup(m_UserSkillsContainer, userSkills);
            }
            else
            {
                var createFolderButton = new Button(SkillsScanner.CreateUserFolder) { text = "Create user skills folder" };
                createFolderButton.AddToClassList("assistant-skills-enable-button");
                m_UserSkillsContainer.Add(createFolderButton);
            }

            m_PackageSkillsSection.SetDisplay(packageSkills.Count > 0);
            if (packageSkills.Count > 0)
                PopulateSkillGroup(m_PackageSkillsContainer, packageSkills);

            var showInternalSkills = SkillsScanner.InternalSkillsEnabled;
            m_InternalSkillsSection.SetDisplay(showInternalSkills);
            if (showInternalSkills)
                PopulateSkillGroup(m_InternalSkillsContainer, internalSkills);
        }

        void BuildIssuesFoldouts(IReadOnlyList<SkillFileIssue> issues, string filter, List<SkillFileIssue> timeoutIssues = null, IReadOnlyList<SkillFileIssue> duplicateIssues = null)
        {
            var anySkills = issues.Count > 0 || m_MinorIssues.Count > 0 || timeoutIssues?.Count > 0 || duplicateIssues?.Count > 0;

            m_SkillsIssuesContainer.Clear();
            m_SkillsIssuesSection.SetDisplay(anySkills);

            if (!anySkills)
                return;

            // Timeout issues are system-level - always shown regardless of the name filter.
            var filteredIssues = new List<SkillFileIssue>();
            if (timeoutIssues != null)
                filteredIssues.AddRange(timeoutIssues);

            foreach (var entry in issues)
            {
                if (string.IsNullOrEmpty(filter) || entry.Name.ToLowerInvariant().Contains(filter))
                    filteredIssues.Add(entry);
            }
            foreach (var entry in m_MinorIssues)
            {
                if (string.IsNullOrEmpty(filter) || entry.Name.ToLowerInvariant().Contains(filter))
                    filteredIssues.Add(entry);
            }
            if (duplicateIssues != null)
            {
                foreach (var entry in duplicateIssues)
                {
                    if (string.IsNullOrEmpty(filter) || entry.Name.ToLowerInvariant().Contains(filter))
                        filteredIssues.Add(entry);
                }
            }

            filteredIssues.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            foreach (var issue in filteredIssues)
            {
                var container = new Foldout { value = false, text = issue.Name };
                container.AddToClassList("assistant-skills-foldout");

                var toggleLabel = container.Q<Label>();
                if (toggleLabel != null)
                {
                    var foldoutIcon = new Image();
                    foldoutIcon.AddToClassList("assistant-skills-header-icon");
                    if (issue.Type == SkillFileIssue.ErrorLevel.Info)
                        foldoutIcon.AddToClassList("mui-icon-info");
                    else
                        foldoutIcon.AddToClassList("mui-icon-warn");
                    toggleLabel.parent.Insert(toggleLabel.parent.IndexOf(toggleLabel), foldoutIcon);
                }

                var warningBox = new SkillWarningBox();
                warningBox.Initialize(Context);
                var tooltip = issue.Type == SkillFileIssue.ErrorLevel.Critical ? k_MajorWarning : k_MinorWarning;
                warningBox.SetData(issue.Error, issue.Type == SkillFileIssue.ErrorLevel.Info, tooltip);
                container.Add(warningBox);

                if (string.IsNullOrEmpty(issue.Path))
                    container.Add(new Label("(no path)"));
                else
                    AddFolderRow(container, "File location", issue.Path, showFullUserPath: true);

                m_SkillsIssuesContainer.Add(container);
            }
        }

        void PopulateSkillGroup(VisualElement skillsContainer, List<SkillDefinition> group)
        {
            if (group.Count == 0)
            {
                var noSkillsLabel = new Label("(no skills found)");
                noSkillsLabel.AddToClassList("assistant-skills-no-skills-label");
                skillsContainer.Add(noSkillsLabel);
                return;
            }

            foreach (var skill in group)
            {
                var detailsFoldout = new Foldout { value = false, text = skill.MetaData.Name };
                detailsFoldout.AddToClassList("assistant-skills-foldout");
                detailsFoldout.SetEnabled(skill.MetaData.Enabled);

                if (!skill.MetaData.Enabled)
                {
                    var toggleRow = detailsFoldout.Q<VisualElement>(className: "unity-foldout__toggle");
                    if (toggleRow != null)
                    {
                        var disabledLabel = new Label("Disabled");
                        disabledLabel.AddToClassList("assistant-skills-disabled-label");
                        toggleRow.Add(disabledLabel);
                    }
                }

                var isInternal = SkillRegistryTags.IsInternalOrBuiltIn(skill.Tags);
                if (!isInternal)
                {
                    var toggleRow = detailsFoldout.Q<VisualElement>(className: "unity-foldout__toggle");
                    if (toggleRow != null)
                    {
                        var capturedSkill = skill;
                        var choices = new List<string> { "Allow", "Deny" };
                        var dropdown = new DropdownField(choices,
                            AssistantEditorPreferences.GetSkillAllowed(capturedSkill) ? 0 : 1);
                        dropdown.AddToClassList("assistant-skills-opt-in-dropdown");
                        dropdown.RegisterValueChangedCallback(evt =>
                            AssistantEditorPreferences.SetSkillAllowed(capturedSkill, evt.newValue == choices[0]));
                        toggleRow.Add(dropdown);
                    }
                }

                var unknownFields = !string.IsNullOrEmpty(skill.Path)
                    ? SkillUtils.GetUncommonFrontmatterFields(skill.Content, skill.Path)
                    : null;
                string unknownFieldsWarning = unknownFields?.Count > 0
                    ? $"Unknown frontmatter fields: {string.Join(", ", unknownFields)}\nExpected: {SkillUtils.CommonFrontmatterFieldNames}"
                    : null;

                if (unknownFieldsWarning != null)
                    m_MinorIssues.Add(new SkillFileIssue(skill.MetaData.Name, skill.Path, unknownFieldsWarning, SkillFileIssue.ErrorLevel.Warning));

                string displayPath = string.IsNullOrEmpty(skill.Path) ? null : ComputeDisplayPath(skill.Path);
                
                var detailsBlock = new SkillDetailsBlock();
                detailsBlock.Initialize(Context);
                detailsBlock.SetData(skill, displayPath, unknownFieldsWarning);
                detailsFoldout.Add(detailsBlock);

                skillsContainer.Add(detailsFoldout);
            }
        }

        static string GetSourceTag(List<string> tags)
        {
            foreach (var t in tags)
                if (SkillRegistryTags.All.Contains(t))
                    return t;
            return string.Empty;
        }

        void AddFolderRow(VisualElement container, string labelText, string path, bool showFullUserPath = false, bool addSeparator = false)
        {
            var folderRow = new SkillFolderRow();
            folderRow.Initialize(Context);
            folderRow.SetData(labelText, ComputeDisplayPath(path, showFullUserPath), path);

            if (addSeparator)
            {
                var wrapper = new VisualElement();
                wrapper.AddToClassList("assistant-skills-folder-separator");
                wrapper.Add(folderRow);
                container.Add(wrapper);
            }
            else
            {
                container.Add(folderRow);
            }
        }

        string ComputeDisplayPath(string path, bool showFullUserPath = false)
        {
            string displayPath = path;

            string normalizedPath = path.Replace('\\', '/');
            string normalizedDataPath = Application.dataPath.Replace('\\', '/');
            string normalizedUserPath = SkillsScanner.UserAppDataFolder.Replace('\\', '/');

            bool isAssetPath = false;
            if (normalizedPath.StartsWith(normalizedDataPath, StringComparison.OrdinalIgnoreCase))
            {
                displayPath = "Assets" + normalizedPath.Substring(normalizedDataPath.Length);
                isAssetPath = true;
            }
            else if (normalizedPath.StartsWith(normalizedUserPath, StringComparison.OrdinalIgnoreCase))
            {
                if (!showFullUserPath)
                    displayPath = normalizedPath.Substring(normalizedUserPath.Length).TrimStart('/');
            }
            else
            {
                m_CachedPackagePaths ??= BuildPackagePathCache();
                foreach (var entry in m_CachedPackagePaths)
                {
                    if (normalizedPath.StartsWith(entry.NormalizedResolved, StringComparison.OrdinalIgnoreCase) &&
                        (normalizedPath.Length == entry.NormalizedResolved.Length || normalizedPath[entry.NormalizedResolved.Length] == '/'))
                    {
                        displayPath = "Packages/" + entry.Name + normalizedPath.Substring(entry.NormalizedResolved.Length);
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(displayPath))
            {
                displayPath = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
            
            // Final absolute user path shown as native path, the way user is used to for current OS
            if (!isAssetPath)
            {
                displayPath = displayPath.Replace('/', Path.DirectorySeparatorChar);
            }

            return displayPath;
        }
    }
}
