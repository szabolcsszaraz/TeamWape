using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Settings;
using Unity.AI.Toolkit;
using Unity.AI.Toolkit.Accounts.Services;
using Unity.Relay.Editor;
using Unity.Relay.Editor.Acp;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    /// <summary>
    /// Project Settings page for configuring AI Gateway working directories per provider.
    /// Appears under Project Settings > AI > Gateway.
    /// </summary>
    class GatewayProjectSettingsPage : ManagedTemplate
    {
        DropdownField m_ProviderDropdown;
        TextField m_WorkdirPathField;
        Button m_WorkdirBrowseButton;
        Toggle m_IncludeDefaultAgentsToggle;

        VisualElement m_DisclaimerSection;
        Button m_DisclaimerAcceptButton;
        Label m_DisclaimerSignInLabel;
        VisualElement m_SettingsContent;

        static readonly string[] k_DisclaimerBullets =
        {
            "<b>Direct Third-Party Relationship:</b> You are using an AI agent that is not owned, operated, or modified by Unity. Your use of the agent is governed solely by your existing license and service agreements with the third-party provider, and you assume all risks related to security vulnerabilities, intellectual property infringement, and the accuracy of the content generated.",
            "<b>Data Training and Privacy:</b> The third-party provider's ability to train their models on your project data or inputs is determined strictly by your own contract with them. Please review your provider's terms to ensure they meet your privacy requirements. You are responsible for ensuring the security and integrity of the data sent to and received from the third-party agent.",
            "<b>Unity Logging:</b> Unity logs usage metadata (such as session timestamps and connection status) for operational purposes. Unity does not log or store the content of your prompts or the third-party provider's outputs.",
        };

        public GatewayProjectSettingsPage() : base(AssistantUIConstants.UIModulePath) { }

        protected override void InitializeView(TemplateContainer view)
        {
            // Query UI elements
            m_ProviderDropdown = view.Q<DropdownField>("provider-dropdown");
            m_WorkdirPathField = view.Q<TextField>("workdir-path-field");
            m_WorkdirBrowseButton = view.Q<Button>("workdir-browse-button");
            m_IncludeDefaultAgentsToggle = view.Q<Toggle>("include-default-agents-toggle");

            m_DisclaimerSection = view.Q<VisualElement>("gateway-disclaimer-section");
            m_SettingsContent = view.Q<VisualElement>(className: "gateway-workdir-content");

            // Set up working directory path field
            m_WorkdirPathField?.RegisterValueChangedCallback(OnWorkdirPathChanged);
            m_WorkdirBrowseButton?.RegisterCallback<ClickEvent>(_ => BrowseWorkdir());

            // Set up include default agents.md toggle
            m_IncludeDefaultAgentsToggle?.RegisterValueChangedCallback(OnIncludeDefaultAgentsChanged);

            BuildDisclaimerContent();

            RegisterAttachEvents(OnAttach, OnDetach);
        }

        void OnAttach(AttachToPanelEvent evt)
        {
            GatewayProjectPreferences.WorkingDirChanged += OnWorkingDirChangedExternally;
            GatewayProjectPreferences.IncludeDefaultAgentsMdChanged += LoadIncludeDefaultAgents;
            GatewayPreferenceService.Instance.Preferences.Refresh();    // Force a clean update every time the page is shown.

            GatewayPreferenceService.Instance.Preferences.OnChange += Refresh;
            RelayService.Instance.StateChanged += Refresh;
            AssistantEditorPreferences.AiGatewayDisclaimerAcceptedChanged += RefreshDisclaimerState;
            Account.session.OnChange += OnSessionChanged;
            Refresh();
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            GatewayProjectPreferences.WorkingDirChanged -= OnWorkingDirChangedExternally;
            GatewayProjectPreferences.IncludeDefaultAgentsMdChanged -= LoadIncludeDefaultAgents;
            GatewayPreferenceService.Instance.Preferences.OnChange -= Refresh;
            RelayService.Instance.StateChanged -= Refresh;
            AssistantEditorPreferences.AiGatewayDisclaimerAcceptedChanged -= RefreshDisclaimerState;
            Account.session.OnChange -= OnSessionChanged;
        }

        void OnSessionChanged() => EditorTask.delayCall += RefreshDisclaimerState;

        void RefreshDisclaimerState()
        {
            var accepted = AssistantEditorPreferences.GetAiGatewayDisclaimerAccepted();

            m_DisclaimerSection.style.display = accepted ? DisplayStyle.None : DisplayStyle.Flex;

            if (!accepted)
            {
                var hasUserId = !string.IsNullOrEmpty(CloudProjectSettings.userId);
                m_DisclaimerAcceptButton.style.display = hasUserId ? DisplayStyle.Flex : DisplayStyle.None;
                m_DisclaimerSignInLabel.style.display = hasUserId ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (m_SettingsContent != null)
            {
                m_SettingsContent.enabledSelf = accepted;
                m_SettingsContent.tooltip = accepted ? "" : "Accept the third-party agreement above to configure providers";
            }
        }

        void BuildDisclaimerContent()
        {
            if (m_DisclaimerSection == null)
                return;

            var infoIcon = m_DisclaimerSection.Q<Image>("disclaimer-info-icon");
            if (infoIcon != null)
                infoIcon.image = EditorGUIUtility.IconContent("console.infoicon.sml").image;

            var bulletsContainer = m_DisclaimerSection.Q<VisualElement>("disclaimer-bullets-container");
            foreach (var bullet in k_DisclaimerBullets)
            {
                var row = new VisualElement();
                row.AddToClassList("disclaimer-bullet-row");

                var glyph = new Label("\u2022");
                glyph.AddToClassList("disclaimer-bullet-glyph");

                var text = new Label(bullet) { enableRichText = true };
                text.AddToClassList("disclaimer-bullet-text");

                row.Add(glyph);
                row.Add(text);
                bulletsContainer.Add(row);
            }

            m_DisclaimerAcceptButton = m_DisclaimerSection.Q<Button>("disclaimer-accept-button");
            m_DisclaimerAcceptButton.clicked += OnDisclaimerAcceptClicked;
            m_DisclaimerSignInLabel = m_DisclaimerSection.Q<Label>("disclaimer-signin-label");
        }

        void OnDisclaimerAcceptClicked()
        {
            AssistantEditorPreferences.SetAiGatewayDisclaimerAccepted(true);
        }

        void Refresh()
        {
            RefreshDisclaimerState();

            var prefs = GatewayPreferenceService.Instance.Preferences?.Value;
            m_ProviderDropdown.choices = prefs?.ProviderInfoList?
                .Select(a => a.ProviderDisplayName)
                .ToList() ?? new List<string>();

            // Default to first provider
            m_ProviderDropdown.index = 0;

            m_ProviderDropdown.RegisterValueChangedCallback(_ => LoadWorkdirPath());
            LoadIncludeDefaultAgents();
        }

        void OnWorkingDirChangedExternally(string agentType)
        {
            // Only refresh if the changed agent type matches the currently selected one
            if (agentType == SelectedProviderType)
                LoadWorkdirPath();
        }

        ProviderInfo SelectedProvider =>
            GatewayPreferenceService.Instance.Preferences.Value?.ProviderInfoList?
                .FirstOrDefault(info => m_ProviderDropdown.value == info.ProviderDisplayName);

        string SelectedProviderType => SelectedProvider?.ProviderType;

        void LoadWorkdirPath()
        {
            if (m_WorkdirPathField == null) return;

            var configuredPath = GatewayProjectPreferences.GetConfiguredWorkingDir(SelectedProviderType);
            m_WorkdirPathField.SetValueWithoutNotify(configuredPath);
        }

        void OnWorkdirPathChanged(ChangeEvent<string> evt) =>
            GatewayProjectPreferences.SetWorkingDir(SelectedProviderType, evt.newValue);

        void BrowseWorkdir()
        {
            if (SelectedProvider == null)
                return;

            var title = $"Select Working Directory for {SelectedProvider.ProviderType}";

            // Start from current configured path or project root
            var currentPath = GatewayProjectPreferences.GetWorkingDir(SelectedProvider.ProviderType);
            var startFolder = !string.IsNullOrEmpty(currentPath) && System.IO.Directory.Exists(currentPath)
                ? currentPath
                : GatewayProjectPreferences.ProjectRoot;

            var selectedPath = EditorUtility.OpenFolderPanel(title, startFolder, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                m_WorkdirPathField.value = selectedPath;
            }
        }

        void LoadIncludeDefaultAgents()
        {
            if (m_IncludeDefaultAgentsToggle == null) return;

            var value = GatewayProjectPreferences.IncludeDefaultAgentsMd;
            m_IncludeDefaultAgentsToggle.SetValueWithoutNotify(value);
        }

        void OnIncludeDefaultAgentsChanged(ChangeEvent<bool> evt)
        {
            GatewayProjectPreferences.IncludeDefaultAgentsMd = evt.newValue;
        }
    }
}
