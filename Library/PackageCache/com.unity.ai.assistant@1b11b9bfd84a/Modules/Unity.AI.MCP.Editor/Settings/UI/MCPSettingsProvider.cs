using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
using Unity.AI.Assistant.Editor;
using Unity.AI.MCP.Editor.Connection;
using Unity.AI.MCP.Editor.Models;
using Unity.AI.MCP.Editor.Settings.Utilities;
using Unity.AI.MCP.Editor.Settings.UI;
using Unity.AI.MCP.Editor.ToolRegistry;
using Unity.AI.MCP.Editor.Constants;
using Unity.AI.MCP.Editor.Helpers;
using Unity.AI.MCP.Editor.Security;
using Unity.AI.Toolkit;
using Unity.AI.Tracing;
using UnityEngine;

namespace Unity.AI.MCP.Editor.Settings
{
    class MCPSettingsProvider : SettingsProvider
    {
        static string s_UxmlPath = $"{MCPConstants.uiTemplatesPath}/MCPSettingsPanel.uxml";

        VisualElement m_RootElement;

        // Cached UI elements
        Toggle m_DebugLogsToggle;
        Toggle m_AutoApproveBatchToggle;
        DropdownField m_ValidationLevelField;
        Button m_ToggleBridgeButton;
        VisualElement m_ClientList;
        ScrollView m_ConnectedClientsList;
        ScrollView m_PendingConnectionsList;
        ScrollView m_OtherConnectionsList;
        ScrollView m_ToolsList;
        Foldout m_ClientsFoldout;
        Foldout m_OtherConnectionsFoldout;
        Foldout m_ToolsFoldout;
        Button m_ResetToolsButton;
        VisualElement m_PendingConnectionsSection;

        // Status UI elements
        VisualElement m_BridgeStatusIndicator;
        Label m_BridgeStatusLabel;
        Label m_ValidationDescription;
        Label m_ConnectionPolicyLabel;
        Button m_LocateServer;

        // Disclaimer UI elements
        VisualElement m_DisclaimerSection;
        Button m_DisclaimerAcceptButton;
        Label m_DisclaimerSignInLabel;
        VisualElement m_McpServerContent;

        static readonly string[] k_DisclaimerBullets =
        {
            "<b>Direct Third-Party Relationship:</b> You are connecting a tool that is not owned, operated, or modified by Unity. Your use of that tool is governed solely by your existing license and service agreements with the third-party provider, and you assume all risks related to security vulnerabilities, intellectual property infringement, and the accuracy of the content generated.",
            "<b>Data Training and Privacy:</b> The third-party tool provider's ability to train their models on your project data or inputs is determined strictly by your own contract with them. Please review your provider's terms to ensure they meet your privacy requirements. You are responsible for ensuring the security and integrity of the data transmitted between your third-party tool and Unity's MCP Server.",
            "<b>Unity Logging:</b> Unity logs usage metadata (such as session timestamps and connection status) for operational purposes. Unity does not log or store the content of your prompts or the outputs returned by your third-party tool.",
        };

        public MCPSettingsProvider(string path, SettingsScope scope = SettingsScope.Project)
            : base(path, scope) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new MCPSettingsProvider(MCPConstants.projectSettingsPath);
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            m_RootElement = rootElement;
            LoadUI();
            InitializeUI();
            RefreshUI();

            MCPSettingsManager.OnSettingsChanged += RefreshUI;
            ConnectionStore.OnConnectionHistoryChanged += OnConnectionHistoryChanged;
            Bridge.OnClientConnectionChanged += OnClientConnectionChanged;
            ConnectionCensus.PolicyChanged += OnMaxDirectConnectionsPolicyChanged;
            AssistantEditorPreferences.McpServerDisclaimerAcceptedChanged += RefreshDisclaimerState;
        }

        public override void OnDeactivate()
        {
            MCPSettingsManager.OnSettingsChanged -= RefreshUI;
            ConnectionStore.OnConnectionHistoryChanged -= OnConnectionHistoryChanged;
            Bridge.OnClientConnectionChanged -= OnClientConnectionChanged;
            ConnectionCensus.PolicyChanged -= OnMaxDirectConnectionsPolicyChanged;
            AssistantEditorPreferences.McpServerDisclaimerAcceptedChanged -= RefreshDisclaimerState;

            if (MCPSettingsManager.HasUnsavedChanges)
            {
                MCPSettingsManager.SaveSettings();
            }
        }

        void RefreshDisclaimerState()
        {
            if (m_DisclaimerSection == null)
                return;

            var accepted = AssistantEditorPreferences.GetMcpServerDisclaimerAccepted();
            m_DisclaimerSection.style.display = accepted ? DisplayStyle.None : DisplayStyle.Flex;

            if (!accepted)
            {
                var hasUserId = !string.IsNullOrEmpty(CloudProjectSettings.userId);
                m_DisclaimerAcceptButton.style.display = hasUserId ? DisplayStyle.Flex : DisplayStyle.None;
                m_DisclaimerSignInLabel.style.display = hasUserId ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (m_McpServerContent != null)
            {
                m_McpServerContent.enabledSelf = accepted;
                m_McpServerContent.tooltip = accepted ? "" : "Accept the third-party agreement above to configure the MCP Server";
            }
        }

        void BuildDisclaimerContent()
        {
            m_DisclaimerSection = m_RootElement.Q<VisualElement>("mcpServerDisclaimerSection");
            m_McpServerContent = m_RootElement.Q<VisualElement>("mcpServerContent");

            if (m_DisclaimerSection == null)
                return;

            var infoIcon = m_DisclaimerSection.Q<Image>("disclaimerInfoIcon");
            if (infoIcon != null)
                infoIcon.image = EditorGUIUtility.IconContent("console.infoicon.sml").image;

            var bulletsContainer = m_DisclaimerSection.Q<VisualElement>("disclaimerBulletsContainer");
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

            m_DisclaimerAcceptButton = m_DisclaimerSection.Q<Button>("disclaimerAcceptButton");
            m_DisclaimerAcceptButton.clicked += OnDisclaimerAcceptClicked;
            m_DisclaimerSignInLabel = m_DisclaimerSection.Q<Label>("disclaimerSignInLabel");
        }

        void OnDisclaimerAcceptClicked()
        {
            AssistantEditorPreferences.SetMcpServerDisclaimerAccepted(true);
        }

        void OnClientConnectionChanged()
        {
            RefreshConnectionsList();
        }

        void OnMaxDirectConnectionsPolicyChanged()
        {
            UpdateConnectionPolicyLabel();
        }

        void OnConnectionHistoryChanged()
        {
            RefreshConnectionsList();
        }

        void LoadUI()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(s_UxmlPath);

            if (visualTree != null)
            {
                visualTree.CloneTree(m_RootElement);
            }
            else
            {
                var fallbackLabel = new Label("Unity MCP Settings - UI template not found");
                fallbackLabel.AddToClassList("umcp-header-title");
                m_RootElement.Add(fallbackLabel);
            }
        }

        void InitializeUI()
        {
            var settings = MCPSettingsManager.Settings;

            // Cache UI elements
            m_DebugLogsToggle = m_RootElement.Q<Toggle>("debugLogsToggle");
            m_ValidationLevelField = m_RootElement.Q<DropdownField>("validationLevelField");
            m_ToggleBridgeButton = m_RootElement.Q<Button>("toggleBridgeButton");
            m_ClientList = m_RootElement.Q<VisualElement>("clientList");
            m_ConnectedClientsList = m_RootElement.Q<ScrollView>("connectedClientsList");
            m_PendingConnectionsList = m_RootElement.Q<ScrollView>("pendingConnectionsList");
            m_OtherConnectionsList = m_RootElement.Q<ScrollView>("otherConnectionsList");
            m_PendingConnectionsSection = m_RootElement.Q<VisualElement>("pendingConnectionsSection");
            m_ToolsList = m_RootElement.Q<ScrollView>("toolsList");
            m_ClientsFoldout = m_RootElement.Q<Foldout>("clientsFoldout");
            m_OtherConnectionsFoldout = m_RootElement.Q<Foldout>("otherConnectionsFoldout");
            m_ToolsFoldout = m_RootElement.Q<Foldout>("toolsFoldout");
            m_ResetToolsButton = m_RootElement.Q<Button>("resetToolsButton");
            m_ResetToolsButton.clicked += OnResetToolsToDefaults;
            m_LocateServer = m_RootElement.Q<Button>("locateServer");
            m_LocateServer.clicked += PathUtils.OpenServerMainFile;

            SetupGenericConfigSnippet();

            // Cache status UI elements
            m_BridgeStatusIndicator = m_RootElement.Q<VisualElement>("bridgeStatusIndicator");
            m_BridgeStatusLabel = m_RootElement.Q<Label>("bridgeStatusLabel");
            m_ValidationDescription = m_RootElement.Q<Label>("validationDescription");
            m_ConnectionPolicyLabel = m_RootElement.Q<Label>("connectionPolicyLabel");

            // Set initial values and bind events
            m_DebugLogsToggle.value = TraceCategories.IsEnabled("mcp");
            m_DebugLogsToggle.RegisterValueChangedCallback(evt => {
                TraceCategories.SetEnabled("mcp", evt.newValue);
            });

            m_AutoApproveBatchToggle = m_RootElement.Q<Toggle>("autoApproveBatchToggle");
            m_AutoApproveBatchToggle.value = settings.autoApproveInBatchMode;
            m_AutoApproveBatchToggle.RegisterValueChangedCallback(evt => {
                settings.autoApproveInBatchMode = evt.newValue;
                MCPSettingsManager.MarkDirty();
            });

            var validationLevels = ToolDescriptions.ValidationLevels.ToList();
            var currentLevelIndex = validationLevels.IndexOf(settings.validationLevel);

            m_ValidationLevelField.choices = validationLevels;
            m_ValidationLevelField.value = settings.validationLevel;
            m_ValidationLevelField.index = currentLevelIndex > -1 ? currentLevelIndex : 1; // Default to "standard"

            m_ValidationLevelField.RegisterValueChangedCallback(evt => {
                settings.validationLevel = evt.newValue;
                UpdateValidationDescription(evt.newValue);
                MCPSettingsManager.MarkDirty();
            });

            // Bind buttons
            m_ToggleBridgeButton.clicked += ToggleBridge;

            // Setup foldouts - Tools expanded by default, Integrations and Other Connections collapsed
            m_ToolsFoldout.value = true;
            m_ClientsFoldout.value = false;
            m_OtherConnectionsFoldout.value = false;

            // Auto-start bridge if not explicitly stopped
            EnsureBridgeAutoStart();

            // Initialize controls
            SetupClientList();
            SetupConnectionsList();
            SetupToolsList();

            BuildDisclaimerContent();
        }

        void RefreshUI()
        {
            RefreshBridgeStatus();
            RefreshClientList();
            RefreshConnectionsList();
            RefreshToolCounts();
            UpdateValidationDescription(MCPSettingsManager.Settings.validationLevel);
            RefreshDisclaimerState();
        }

        void RefreshBridgeStatus()
        {
            bool isRunning = UnityMCPBridge.IsRunning;
            UpdateBridgeStatus(isRunning);
        }

        void SetupClientList()
        {
            // Clear existing client items
            m_ClientList.Clear();

            var clients = MCPClientManager.GetClients();

            if (clients.Count == 0)
            {
                var noClientsLabel = new Label("No MCP clients available");
                noClientsLabel.AddToClassList("umcp-no-clients-message");
                m_ClientList.Add(noClientsLabel);
                return;
            }

            // Check configuration and add each client as a ClientItemControl
            foreach (var client in clients)
            {
                MCPClientManager.CheckClientConfiguration(client);

                var clientItem = new ClientItemControl(
                    client,
                    CheckClientConfiguration,
                    RefreshClientList
                );

                m_ClientList.Add(clientItem);
            }
        }

        void SetupGenericConfigSnippet()
        {
            var snippetField = m_RootElement.Q<TextField>("genericConfigSnippetField");
            if (snippetField == null)
                return;

            string mainFile = PathUtils.GetServerMainFile().Replace("\\", "\\\\");
            string snippet = $"{{\n" +
                             $"  \"mcpServers\": {{\n" +
                             $"    \"{MCPConstants.jsonKeyIntegration}\": {{\n" +
                             $"      \"command\": \"{mainFile}\",\n" +
                             $"      \"args\": [\"--mcp\"]\n" +
                             $"    }}\n" +
                             $"  }}\n" +
                             $"}}";

            snippetField.value = snippet;
            snippetField.isReadOnly = true;
            snippetField.multiline = true;

            var copyButton = m_RootElement.Q<Button>("copyGenericSnippetButton");
            if (copyButton != null)
            {
                copyButton.text = "";
                var copyIcon = EditorGUIUtility.IconContent("Clipboard");
                if (copyIcon?.image != null)
                {
                    var iconImage = new Image { image = copyIcon.image };
                    iconImage.AddToClassList("umcp-copy-icon");
                    copyButton.Add(iconImage);
                }

                copyButton.RegisterCallback<ClickEvent>(_ =>
                {
                    EditorGUIUtility.systemCopyBuffer = snippet;
                });
            }
        }

        void CheckClientConfiguration(McpClient client)
        {
            MCPClientManager.CheckClientConfiguration(client);

            string message = $"[Unity MCP] {client.name}: {client.GetStatusDisplayString()}";
            switch (client.status)
            {
                case McpStatus.Error:
                case McpStatus.IncorrectPath:
                case McpStatus.MissingConfig:
                case McpStatus.CommunicationError:
                case McpStatus.NoResponse:
                case McpStatus.UnsupportedOS:
                    Debug.LogWarning(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }

        void RefreshClientList()
        {
            SetupClientList();
        }

        void RefreshToolCounts()
        {
            var allTools = McpToolRegistry.GetAllToolsForSettings();
            int enabledCount = allTools.Count(t => t.IsEnabled);
            m_ToolsFoldout.text = $"Tools ({enabledCount} of {allTools.Length} enabled)";
        }

        void SetupConnectionsList()
        {
            // Clear all three lists
            m_ConnectedClientsList.Clear();
            m_PendingConnectionsList.Clear();
            m_OtherConnectionsList.Clear();

            UpdateConnectionPolicyLabel();

            // Get per-transport active connections (one entry per physical connection)
            var activeTransports = UnityMCPBridge.IsRunning
                ? TransportStore.GetActiveTransportStates()
                : new List<TransportState>();

            // Build set of active identity keys for filtering historical connections
            var activeIdentityKeys = new HashSet<string>(activeTransports.Select(t => t.IdentityKey));

            // Classify active transports into connected (approved) vs pending (awaiting approval)
            var connectedClients = new List<ConnectionRecord>();
            var pendingActiveTransports = new List<ConnectionRecord>();
            foreach (var ts in activeTransports)
            {
                var info = ts.ValidationDecision?.Connection;
                if (info == null)
                    continue;

                if (ts.ClientInfo != null)
                    info.ClientInfo = ts.ClientInfo;

                var approvalState = ts.ApprovalState;
                bool isApproved = approvalState == ConnectionApprovalState.Approved ||
                                  approvalState == ConnectionApprovalState.GatewayApproved;

                var record = new ConnectionRecord
                {
                    Info = info,
                    Status = isApproved ? ValidationStatus.Accepted : ValidationStatus.Pending,
                    ValidationReason = ts.ValidationDecision?.Reason,
                    Identity = ConnectionIdentity.FromConnectionInfo(info)
                };

                if (isApproved)
                    connectedClients.Add(record);
                else if (approvalState == ConnectionApprovalState.AwaitingApproval)
                    pendingActiveTransports.Add(record);
                // Denied/Validating/Unknown active transports are excluded from
                // activeIdentityKeys so they fall through to historical connections below.
                else
                    activeIdentityKeys.Remove(ts.IdentityKey);
            }

            // Historical connections not currently active (active ones are shown from TransportStore above)
            var inactiveConnections = ConnectionStore.GetRecentConnections(100)
                .Where(c => c.Info != null && c.Info.Timestamp != DateTime.MinValue)
                .Where(c => c.Identity == null || !activeIdentityKeys.Contains(c.Identity.CombinedIdentityKey))
                .OrderByDescending(c => c.Info?.Timestamp ?? DateTime.MinValue)
                .ToList();

            // Combine active pending transports with historical pending connections
            var pendingConnections = pendingActiveTransports
                .Concat(inactiveConnections.Where(c => c.Status == ValidationStatus.Pending))
                .ToList();

            var otherConnections = inactiveConnections
                .Where(c => c.Status != ValidationStatus.Pending)
                .ToList();

            // Get gateway connections (AI Gateway auto-approved connections)
            var gatewayConnections = ConnectionStore.GetGatewayConnections();

            // Setup Connected Clients section (always visible)
            var hasAnyConnectedClients = connectedClients.Count > 0 || gatewayConnections.Count > 0;

            if (!hasAnyConnectedClients)
            {
                var noClientsLabel = new Label("No clients connected");
                noClientsLabel.AddToClassList("umcp-no-clients-message");
                m_ConnectedClientsList.Add(noClientsLabel);
            }
            else
            {
                // Add gateway connections first (with purple indicator)
                foreach (var gateway in gatewayConnections)
                {
                    var gatewayItem = new GatewayConnectionItemControl(gateway);
                    m_ConnectedClientsList.Add(gatewayItem);
                }

                // Add regular connected clients (one per physical connection)
                // Detect duplicate display names and disambiguate with server PID
                var titleCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in connectedClients)
                {
                    var (t, _) = c.GetIdentityDisplayParts();
                    titleCounts[t] = titleCounts.TryGetValue(t, out var count) ? count + 1 : 1;
                }

                foreach (var connection in connectedClients)
                {
                    var (t, _) = connection.GetIdentityDisplayParts();
                    string suffix = titleCounts[t] > 1 && connection.Info?.Server != null
                        ? $"(PID {connection.Info.Server.ProcessId})"
                        : null;
                    var connectionItem = new ConnectionItemControl(connection, RefreshConnectionsList, suffix);
                    m_ConnectedClientsList.Add(connectionItem);
                }
            }

            // Setup Pending Connections section (conditionally visible)
            if (pendingConnections.Count > 0)
            {
                m_PendingConnectionsSection.style.display = DisplayStyle.Flex;
                foreach (var connection in pendingConnections)
                {
                    var connectionItem = new ConnectionItemControl(connection, RefreshConnectionsList);
                    m_PendingConnectionsList.Add(connectionItem);
                }
            }
            else
            {
                m_PendingConnectionsSection.style.display = DisplayStyle.None;
            }

            // Setup Other Connections section (foldout, collapsed by default)
            if (otherConnections.Count == 0)
            {
                var noConnectionsLabel = new Label("No other connections");
                noConnectionsLabel.AddToClassList("umcp-no-connections-message");
                m_OtherConnectionsList.Add(noConnectionsLabel);
            }
            else
            {
                foreach (var connection in otherConnections)
                {
                    var connectionItem = new ConnectionItemControl(connection, RefreshConnectionsList);
                    m_OtherConnectionsList.Add(connectionItem);
                }
            }
        }

        void RefreshConnectionsList()
        {
            SetupConnectionsList();
        }

        void UpdateConnectionPolicyLabel()
        {
            int maxDirect = ConnectionCensus.Policy.MaxDirect;

            m_ConnectionPolicyLabel.text = maxDirect < 0
                ? "Unlimited direct connections allowed."
                : maxDirect == 1
                    ? "1 direct connection allowed at a time."
                    : $"Up to {maxDirect} direct connections allowed at a time.";
        }

        void SetupToolsList()
        {
            m_ToolsList.Clear();

            var allTools = McpToolRegistry.GetAllToolsForSettings();

            if (allTools.Length == 0)
            {
                var noToolsLabel = new Label("No MCP tools available");
                noToolsLabel.AddToClassList("umcp-no-tools-message");
                m_ToolsList.Add(noToolsLabel);
                return;
            }

            // Group tools by their first category (or "Uncategorized")
            var grouped = new SortedDictionary<string, List<ToolSettingsEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in allTools)
            {
                string category = entry.Groups != null && entry.Groups.Length > 0
                    ? entry.Groups[0]
                    : "uncategorized";

                if (!grouped.TryGetValue(category, out var list))
                {
                    list = new List<ToolSettingsEntry>();
                    grouped[category] = list;
                }
                list.Add(entry);
            }

            foreach (var (category, tools) in grouped)
            {
                tools.Sort((a, b) =>
                {
                    if (a.IsDefault != b.IsDefault)
                        return a.IsDefault ? -1 : 1;
                    return string.Compare(a.Info.name, b.Info.name, StringComparison.Ordinal);
                });

                int catEnabled = tools.Count(t => t.IsEnabled);
                var categoryFoldout = new Foldout
                {
                    text = $"{FormatCategoryName(category)} ({catEnabled} of {tools.Count})",
                    value = true
                };
                categoryFoldout.AddToClassList("umcp-category-foldout");

                foreach (var entry in tools)
                {
                    var toolItem = new ToolItemControl(entry);
                    categoryFoldout.Add(toolItem);
                }

                m_ToolsList.Add(categoryFoldout);
            }

            RefreshToolCounts();
        }

        void OnResetToolsToDefaults()
        {
            MCPSettingsManager.Settings.ResetToolsToDefaults();
            MCPSettingsManager.MarkDirty();
            SetupToolsList();
        }

        static string FormatCategoryName(string category)
        {
            if (string.IsNullOrEmpty(category))
                return "Uncategorized";

            // Look up display name from ToolCategories metadata
            var cat = ToolCategoryExtensions.FromStringId(category);
            if (cat != ToolCategory.None)
            {
                var info = ToolCategories.GetCategoryInfo(cat);
                return info.DisplayName;
            }

            // Capitalize first letter for unknown categories
            return char.ToUpperInvariant(category[0]) + category.Substring(1);
        }

        void ToggleBridge()
        {
            if (UnityMCPBridge.IsRunning)
            {
                UnityMCPBridge.Stop();
                EditorPrefs.SetBool("MCPBridge.ExplicitlyStopped", true);
            }
            else
            {
                UnityMCPBridge.Start();
                EditorPrefs.SetBool("MCPBridge.ExplicitlyStopped", false);
            }
            RefreshUI();
        }


        void UpdateBridgeStatus(bool isRunning)
        {
            m_BridgeStatusIndicator.ClearClassList();
            m_BridgeStatusIndicator.AddToClassList("umcp-status-indicator");
            m_BridgeStatusIndicator.AddToClassList(isRunning ? "green" : "red");

            m_BridgeStatusLabel.text = isRunning ? "Running" : "Stopped";
            m_ToggleBridgeButton.text = isRunning ? "Stop" : "Start";
        }

        void UpdateValidationDescription(string level)
        {
            string description = level switch
            {
                "basic" => "Only basic syntax checks (braces, quotes, comments)",
                "standard" => "Syntax checks + Unity best practices and warnings",
                "comprehensive" => "All checks + semantic analysis and performance warnings",
                "strict" => "Full semantic validation with namespace/type resolution (requires Roslyn)",
                _ => "Standard validation"
            };

            m_ValidationDescription.text = description;
        }

        void EnsureBridgeAutoStart()
        {
            // Check if bridge was explicitly stopped by user
            bool wasExplicitlyStopped = EditorPrefs.GetBool("MCPBridge.ExplicitlyStopped", false);

            if (!wasExplicitlyStopped && !UnityMCPBridge.IsRunning)
            {
                UnityMCPBridge.Start();
            }
        }

    }
}