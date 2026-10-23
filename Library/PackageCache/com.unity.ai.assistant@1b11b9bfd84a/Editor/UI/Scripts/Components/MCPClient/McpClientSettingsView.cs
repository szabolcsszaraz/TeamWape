using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Mcp.Manager;
using Unity.AI.Assistant.Editor.Service;
using Unity.AI.Toolkit;
using Unity.AI.Toolkit.Accounts.Services;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    /// <summary>
    /// View component for MCP Client settings in Unity Project Settings
    /// </summary>
    class McpClientSettingsView : ManagedTemplate
    {
        VisualElement m_ContentContainer;
        McpServerManagerView m_ServerManagerView;

        VisualElement m_DisclaimerSection;
        Button m_DisclaimerAcceptButton;
        Label m_DisclaimerSignInLabel;

        static readonly string[] k_DisclaimerBullets =
        {
            "<b>Third-Party Tool Responsibility:</b> You are authorizing Unity's MCP Client to connect to and interact with third-party tools and services that are not owned, operated, or modified by Unity. Your use of any third-party tool remains governed solely by your existing license and service agreements with that provider. You assume all risks related to security vulnerabilities, intellectual property infringement, and the accuracy of any content generated or actions taken by those tools.",
            "<b>Automated Actions and Outputs:</b> Unity's MCP Client may send instructions to and receive outputs from third-party tools on your behalf. You are solely responsible for reviewing, validating, and accepting any actions taken or content produced as a result of these interactions. Unity makes no representations or warranties regarding the behavior, reliability, or suitability of any third-party tool accessed through the MCP Client.",
            "<b>Unity Logging:</b> Unity logs usage metadata (such as session timestamps and connection status) for operational purposes. Unity does not log or store the content of your prompts or the outputs returned by third-party tools.",
        };

        public McpClientSettingsView() : base(AssistantUIConstants.UIModulePath) { }

        protected override void InitializeView(TemplateContainer view)
        {
            m_ContentContainer = view.Q<VisualElement>("mcpSettingsContentContainer");
            m_DisclaimerSection = view.Q<VisualElement>("mcp-extensions-disclaimer-section");

            // Load theme styles - this is the root of a settings window
            LoadStyle(view, EditorGUIUtility.isProSkin
                ? AssistantUIConstants.AssistantSharedStyleDark
                : AssistantUIConstants.AssistantSharedStyleLight);
            LoadStyle(view, AssistantUIConstants.AssistantBaseStyle, true);

            // Create and initialize the server manager view
            m_ServerManagerView = new McpServerManagerView();
            m_ServerManagerView.Initialize(Context);

            m_ContentContainer.Add(m_ServerManagerView);

            BuildDisclaimerContent();

            RegisterAttachEvents(OnAttach, OnDetach);
        }

        void OnAttach(AttachToPanelEvent evt)
        {
            AssistantEditorPreferences.McpExtensionsDisclaimerAcceptedChanged += RefreshDisclaimerState;
            Account.session.OnChange += OnSessionChanged;
            RefreshDisclaimerState();
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            AssistantEditorPreferences.McpExtensionsDisclaimerAcceptedChanged -= RefreshDisclaimerState;
            Account.session.OnChange -= OnSessionChanged;
        }

        void OnSessionChanged() => EditorTask.delayCall += RefreshDisclaimerState;

        void RefreshDisclaimerState()
        {
            if (m_DisclaimerSection == null)
                return;

            var accepted = AssistantEditorPreferences.GetMcpExtensionsDisclaimerAccepted();
            m_DisclaimerSection.style.display = accepted ? DisplayStyle.None : DisplayStyle.Flex;

            if (!accepted)
            {
                var hasUserId = !string.IsNullOrEmpty(CloudProjectSettings.userId);
                m_DisclaimerAcceptButton.style.display = hasUserId ? DisplayStyle.Flex : DisplayStyle.None;
                m_DisclaimerSignInLabel.style.display = hasUserId ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (m_ContentContainer != null)
            {
                m_ContentContainer.enabledSelf = accepted;
                m_ContentContainer.tooltip = accepted ? "" : "Accept the third-party agreement above to configure MCP Extensions";
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
            AssistantEditorPreferences.SetMcpExtensionsDisclaimerAccepted(true);
        }

        public void SetServerManager(ServiceHandle<McpServerManagerService> serverManager)
        {
            m_ServerManagerView.SetServerManager(serverManager);
        }
    }
}
