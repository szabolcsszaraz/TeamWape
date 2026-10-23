using System;
using System.IO;
using Unity.AI.MCP.Editor.Models;
using Unity.AI.MCP.Editor.Settings.Utilities;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace Unity.AI.MCP.Editor.Settings.UI
{
    class ClientItemControl : VisualElement
    {
        static readonly string UxmlPath = MCPConstants.uiTemplatesPath + "/ClientItemControl.uxml";

        readonly McpClient m_Client;

        VisualElement m_StatusIndicator;

        Label m_NameLabel;
        Label m_StatusLabel;
        Label m_WarningLabel;

        VisualElement m_WarningRoot;

        Button m_ConfigureButton;
        Button m_CheckButton;
        Button m_LocateButton;
        Button m_WarningHelpButton;

        string m_ActiveWarningHelpUrl;

        public ClientItemControl(McpClient client, Action<McpClient> onCheck, Action onRefresh)
        {
            m_Client = client ?? throw new ArgumentNullException(nameof(client));

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            visualTree.CloneTree(this);

            m_StatusIndicator = this.Q<VisualElement>("statusIndicator");
            m_NameLabel = this.Q<Label>("clientName");
            m_NameLabel.enableRichText = false; // Disable rich text for security - prevents markup injection from client-provided names
            m_StatusLabel = this.Q<Label>("clientStatus");
            m_WarningRoot = this.Q<VisualElement>("warningSection");
            m_WarningLabel = this.Q<Label>("warningLabel");

            m_WarningHelpButton = this.Q<Button>("warningHelpButton");
            m_WarningHelpButton.RegisterCallback<ClickEvent>(_ =>
            {
                if (!string.IsNullOrEmpty(m_ActiveWarningHelpUrl))
                {
                    Application.OpenURL(m_ActiveWarningHelpUrl);
                }
            });

            m_ConfigureButton = this.Q<Button>("configureButton");
            m_ConfigureButton.RegisterCallback<ClickEvent>(_ =>
            {
                var integration = MCPClientManager.CreateClientIntegration(m_Client);
                bool isConfigured = m_Client.status == McpStatus.Configured;

                if (isConfigured)
                {
                    integration.Disable();
                }
                else
                {
                    integration.Configure();
                }

                onRefresh();
            });

            m_CheckButton = this.Q<Button>("checkButton");
            m_CheckButton.RegisterCallback<ClickEvent>(_ =>
            {
                onCheck(m_Client);
                onRefresh();
            });

            m_LocateButton = this.Q<Button>("locateButton");
            m_LocateButton.RegisterCallback<ClickEvent>(_ =>
            {
                string configPath = PlatformUtils.GetConfigPathForClient(m_Client);
                if (!string.IsNullOrEmpty(configPath))
                {
                    if (File.Exists(configPath))
                        EditorUtility.RevealInFinder(configPath);
                    else
                    {
                        string dir = Path.GetDirectoryName(configPath);
                        if (Directory.Exists(dir))
                            EditorUtility.RevealInFinder(dir);
                    }
                }
            });

            RefreshUI();
        }

        void RefreshUI()
        {
            // Update basic info
            m_NameLabel.text = m_Client.name;
            m_StatusLabel.text = m_Client.GetStatusDisplayString();

            // Update status indicator
            UpdateStatusIndicator();

            // Update buttons visibility
            UpdateButtons();

            // Update warnings
            UpdateWarnings();
        }

        void UpdateStatusIndicator()
        {
            m_StatusIndicator.ClearClassList();
            m_StatusIndicator.AddToClassList("umcp-status-indicator");
            m_StatusIndicator.AddToClassList(StatusToCssClass(m_Client.status));
        }

        static string StatusToCssClass(McpStatus status) => status switch
        {
            McpStatus.NotConfigured => "not-configured",
            McpStatus.Configured => "configured",
            McpStatus.Running => "running",
            McpStatus.Connected => "connected",
            McpStatus.IncorrectPath => "incorrect-path",
            McpStatus.CommunicationError => "communication-error",
            McpStatus.NoResponse => "no-response",
            McpStatus.MissingConfig => "missing-config",
            McpStatus.UnsupportedOS => "unsupported-os",
            McpStatus.Error => "error",
            _ => "not-configured"
        };

        void UpdateButtons()
        {
            // Always show configure button for all clients
            bool isConfigured = m_Client.status == McpStatus.Configured;
            m_ConfigureButton.text = isConfigured ? "Disable" : "Configure";
        }

        void UpdateWarnings()
        {
            m_WarningRoot.SetDisplay(false);
            m_ActiveWarningHelpUrl = string.Empty;

            var integration = MCPClientManager.CreateClientIntegration(m_Client);
            if (integration.HasMissingDependencies(out string warningText, out string helpUrl))
            {
                m_WarningRoot.SetDisplay(true);
                m_WarningLabel.text = warningText;
                m_ActiveWarningHelpUrl = helpUrl;
            }
        }
    }
}
