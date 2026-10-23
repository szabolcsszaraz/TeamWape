using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Settings;
using Unity.AI.Toolkit;
using Unity.AI.Toolkit.Accounts.Services;
using Unity.Relay.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class GatewayPreferencesPage : ManagedTemplate
    {
        Label m_ErrorMessage;
        VisualElement m_SettingsContent;

        VisualElement m_DisclaimerSection;
        Button m_DisclaimerAcceptButton;
        Label m_DisclaimerSignInLabel;

        DropdownField m_ProviderDropdown;
        Label m_AgentVersionLabel;
        VisualElement m_ProviderContentSection;

        static readonly string[] k_DisclaimerBullets =
        {
            "<b>Direct Third-Party Relationship:</b> You are using an AI agent that is not owned, operated, or modified by Unity. Your use of the agent is governed solely by your existing license and service agreements with the third-party provider, and you assume all risks related to security vulnerabilities, intellectual property infringement, and the accuracy of the content generated.",
            "<b>Data Training and Privacy:</b> The third-party provider's ability to train their models on your project data or inputs is determined strictly by your own contract with them. Please review your provider's terms to ensure they meet your privacy requirements. You are responsible for ensuring the security and integrity of the data sent to and received from the third-party agent.",
            "<b>Unity Logging:</b> Unity logs usage metadata (such as session timestamps and connection status) for operational purposes. Unity does not log or store the content of your prompts or the third-party provider's outputs.",
        };

        public GatewayPreferencesPage() : base(AssistantUIConstants.UIModulePath) { }

        protected override void InitializeView(TemplateContainer view)
        {
            LoadStyle(view,
                EditorGUIUtility.isProSkin
                    ? AssistantUIConstants.AssistantSharedStyleDark
                    : AssistantUIConstants.AssistantSharedStyleLight);
            LoadStyle(view, AssistantUIConstants.AssistantBaseStyle, true);

            m_ErrorMessage = view.Q<Label>("error-message");
            m_SettingsContent = view.Q<VisualElement>("gateway-settings-content");
            m_DisclaimerSection = view.Q<VisualElement>("gateway-disclaimer-section");

            m_ProviderDropdown = view.Q<DropdownField>("agent-type-dropdown");
            m_AgentVersionLabel = view.Q<Label>("agent-version-label");
            m_ProviderContentSection = view.Q<VisualElement>("provider-content-section");

            m_ProviderDropdown.RegisterValueChangedCallback(_ => RefreshSelectedProvider());

            BuildDisclaimerContent();

            RegisterAttachEvents(OnAttach, OnDetach);
        }

        void OnAttach(AttachToPanelEvent evt)
        {
            GatewayPreferenceService.Instance.Preferences.Refresh();    // Force a clean update every time the page is shown.

            GatewayPreferenceService.Instance.Preferences.OnChange += Refresh;
            RelayService.Instance.StateChanged += Refresh;
            AssistantEditorPreferences.AiGatewayDisclaimerAcceptedChanged += RefreshDisclaimerState;
            AssistantEditorPreferences.ProviderEnabledChanged += Refresh;
            Account.session.OnChange += OnSessionChanged;
            Refresh();
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            GatewayPreferenceService.Instance.Preferences.OnChange -= Refresh;
            RelayService.Instance.StateChanged -= Refresh;
            AssistantEditorPreferences.AiGatewayDisclaimerAcceptedChanged -= RefreshDisclaimerState;
            AssistantEditorPreferences.ProviderEnabledChanged -= Refresh;
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

            UpdateSettingsContentEnabled();
        }

        void UpdateSettingsContentEnabled()
        {
            var accepted = AssistantEditorPreferences.GetAiGatewayDisclaimerAccepted();
            var relayConnected = RelayService.Instance.IsConnected;

            m_SettingsContent.enabledSelf = accepted && relayConnected;

            if (!relayConnected)
                m_SettingsContent.tooltip = "Relay Not connected";
            else if (!accepted)
                m_SettingsContent.tooltip = "Accept the third-party agreement above to configure providers";
            else
                m_SettingsContent.tooltip = "";
        }

        void Refresh()
        {
            RefreshDisclaimerState();

            var prefs = GatewayPreferenceService.Instance.Preferences?.Value;

            m_ErrorMessage.text = prefs?.Error;
            m_ErrorMessage.style.display = string.IsNullOrEmpty(prefs?.Error) ? DisplayStyle.None : DisplayStyle.Flex;

            RefreshProviderList(prefs);
        }

        void RefreshProviderList(PreferencesData prefs)
        {
            if (m_ProviderDropdown == null)
                return;

            m_ProviderDropdown.choices = prefs?.ProviderInfoList?
                .Select(a => a.ProviderDisplayName)
                .ToList() ?? new List<string>();

            RefreshSelectedProvider();
        }

        void RefreshSelectedProvider()
        {
            m_ProviderContentSection.Clear();

            var prefs = GatewayPreferenceService.Instance.Preferences.Value;
            if (m_ProviderDropdown.value == null)
            {
                m_ProviderDropdown.value = prefs?.ProviderInfoList?.FirstOrDefault()?.ProviderDisplayName;
                return;  // Setting value triggers the callback, which calls us again
            }

            var providerInfo = prefs?.ProviderInfoList?.FirstOrDefault(info => m_ProviderDropdown.value == info.ProviderDisplayName);
            if (providerInfo == null)
                return;

            // Version label
            if (!string.IsNullOrEmpty(providerInfo.Version))
            {
                var versionText = $"v{providerInfo.Version}";
                if (providerInfo.IsCustom)
                    versionText += "  [Custom]";
                m_AgentVersionLabel.text = versionText;
                m_AgentVersionLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                m_AgentVersionLabel.text = "";
                m_AgentVersionLabel.style.display = DisplayStyle.None;
            }

            // Enable Agent row
            var enableRow = new VisualElement();
            enableRow.AddToClassList("gateway-enable-agent-row");

            var enableToggle = new Toggle("Enable Agent");
            enableToggle.AddToClassList("gateway-enable-agent-toggle");

            // Check if all required env vars are present
            var hasRequiredKeys = true;
            if (providerInfo.RequiredEnvVarNames is { Count: > 0 })
            {
                var satisfiedVarNames = providerInfo.Variables?
                    .Where(v => (v.InKeychain && v.IsSet) || (!v.InKeychain && !string.IsNullOrEmpty(v.Value)))
                    .Select(v => v.Name)
                    .ToHashSet() ?? new HashSet<string>();
                hasRequiredKeys = providerInfo.RequiredEnvVarNames.All(satisfiedVarNames.Contains);
            }

            enableToggle.SetEnabled(hasRequiredKeys);
            enableToggle.value = hasRequiredKeys && AssistantEditorPreferences.GetProviderEnabled(providerInfo.ProviderType);

            // If key was removed while enabled, disable the provider.
            // Deferred to avoid re-entrancy: SetProviderEnabled fires ProviderEnabledChanged → Refresh → RefreshSelectedProvider while we are still building UI.
            if (!hasRequiredKeys && AssistantEditorPreferences.GetProviderEnabled(providerInfo.ProviderType))
            {
                var providerType = providerInfo.ProviderType;
                EditorTask.delayCall += () => AssistantEditorPreferences.SetProviderEnabled(providerType, false);
            }

            var providerId = providerInfo.ProviderType;
            enableToggle.RegisterValueChangedCallback(evt =>
                AssistantEditorPreferences.SetProviderEnabled(providerId, evt.newValue));
            enableRow.Add(enableToggle);

            if (providerInfo.RequiredEnvVarNames is { Count: > 0 })
            {
                var hint = new Label($"Requires {string.Join(", ", providerInfo.RequiredEnvVarNames)}");
                hint.AddToClassList("gateway-enable-agent-hint");
                enableRow.Add(hint);
            }

            m_ProviderContentSection.Add(enableRow);

            // Environment variables
            var envVarsUI = new ProviderEnvironmentariablesUI();
            envVarsUI.Initialize(null);
            envVarsUI.Refresh(providerInfo);
            m_ProviderContentSection.Add(envVarsUI);
        }

        void BuildDisclaimerContent()
        {
            // Set the info icon (must be done in code — runtime icon lookup)
            var infoIcon = m_DisclaimerSection.Q<Image>("disclaimer-info-icon");
            infoIcon.image = EditorGUIUtility.IconContent("console.infoicon.sml").image;

            // Add bullet points dynamically — hanging-indent layout: glyph in gutter, text wraps under itself.
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

            // Query elements needed for state management
            m_DisclaimerAcceptButton = m_DisclaimerSection.Q<Button>("disclaimer-accept-button");
            m_DisclaimerAcceptButton.clicked += OnDisclaimerAcceptClicked;
            m_DisclaimerSignInLabel = m_DisclaimerSection.Q<Label>("disclaimer-signin-label");
        }

        void OnDisclaimerAcceptClicked()
        {
            AssistantEditorPreferences.SetAiGatewayDisclaimerAccepted(true);
        }
    }
}
