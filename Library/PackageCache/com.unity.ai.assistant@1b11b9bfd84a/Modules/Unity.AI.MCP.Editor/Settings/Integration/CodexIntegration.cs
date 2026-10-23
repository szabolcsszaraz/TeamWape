using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Unity.AI.MCP.Editor.Helpers;
using Unity.AI.MCP.Editor.Models;
using Unity.AI.MCP.Editor.Settings.Utilities;

namespace Unity.AI.MCP.Editor.Settings.Integration
{
    /// <summary>
    /// Integration for Codex (OpenAI) client, managing configuration in TOML format.
    /// </summary>
    class CodexIntegration : IClientIntegration
    {
        const string k_SectionHeader = "[mcp_servers.unity_mcp]";

        public McpClient Client { get; }

        public CodexIntegration(McpClient client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public bool Configure()
        {
            string serverPath = PathUtils.GetServerPath();
            if (string.IsNullOrEmpty(serverPath))
            {
                UpdateStatus(McpStatus.Error, "Server not found");
                return false;
            }

            string mainFile = PathUtils.GetServerMainFile(serverPath);
            if (!File.Exists(mainFile))
            {
                ServerInstaller.InstallOrUpdateRelay();
                if (!File.Exists(mainFile))
                {
                    UpdateStatus(McpStatus.Error, "Server main file not found");
                    return false;
                }
            }

            string configPath = PlatformUtils.GetConfigPathForClient(Client);
            if (string.IsNullOrEmpty(configPath))
            {
                UpdateStatus(McpStatus.Error, "Config path not available for current platform");
                return false;
            }

            bool success = WriteTomlConfig(configPath, mainFile);
            McpStatus status = success ? McpStatus.Configured : McpStatus.Error;
            string message = success ? "Successfully configured" : "Failed to update configuration";

            UpdateStatus(status, message);
            return success;
        }

        public bool Disable()
        {
            string configPath = PlatformUtils.GetConfigPathForClient(Client);
            if (string.IsNullOrEmpty(configPath))
            {
                UpdateStatus(McpStatus.Error, "Config path not available for current platform");
                return false;
            }

            bool success = RemoveTomlSection(configPath);
            McpStatus status = success ? McpStatus.NotConfigured : McpStatus.Error;
            string message = success ? "Successfully unconfigured" : "Failed to remove configuration";

            UpdateStatus(status, message);
            return success;
        }

        public void CheckConfiguration()
        {
            string configPath = PlatformUtils.GetConfigPathForClient(Client);
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                UpdateStatus(McpStatus.NotConfigured, "Configuration file not found");
                return;
            }

            string content = File.ReadAllText(configPath);
            bool isConfigured = content.Contains(k_SectionHeader);
            McpStatus status = isConfigured ? McpStatus.Configured : McpStatus.NotConfigured;
            string message = isConfigured ? "Configured" : "Not configured";
            UpdateStatus(status, message);
        }

        public bool HasMissingDependencies(out string warningText, out string helpUrl)
        {
            warningText = string.Empty;
            helpUrl = string.Empty;
            return false;
        }

        bool WriteTomlConfig(string configPath, string mainFile)
        {
            try
            {
                // Escape backslashes for TOML string
                string escapedPath = mainFile.Replace("\\", "\\\\");

                string section = $@"

{k_SectionHeader}
command = ""{escapedPath}""
args = [""--mcp""]
enabled = true
";

                if (File.Exists(configPath))
                {
                    string content = File.ReadAllText(configPath);

                    // Replace existing section or append
                    if (content.Contains(k_SectionHeader))
                    {
                        content = RemoveSectionFromContent(content);
                    }

                    content = content.TrimEnd() + section;
                    File.WriteAllText(configPath, content);
                }
                else
                {
                    string directory = Path.GetDirectoryName(configPath);
                    if (!Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    File.WriteAllText(configPath, section.TrimStart());
                }

                return true;
            }
            catch (Exception ex)
            {
                McpLog.Warning($"Failed to write Codex config: {ex.Message}");
                return false;
            }
        }

        bool RemoveTomlSection(string configPath)
        {
            try
            {
                if (!File.Exists(configPath))
                    return true;

                string content = File.ReadAllText(configPath);
                if (!content.Contains(k_SectionHeader))
                    return true;

                content = RemoveSectionFromContent(content);
                File.WriteAllText(configPath, content);
                return true;
            }
            catch (Exception ex)
            {
                McpLog.Warning($"Failed to remove Codex config section: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes the [mcp_servers.unity_mcp] section from TOML content.
        /// A section ends at the next [section] header or end of file.
        /// </summary>
        static string RemoveSectionFromContent(string content)
        {
            // Match from our section header to the next section header or end of file
            var pattern = @"\n?" + Regex.Escape(k_SectionHeader) + @"(?:(?!\r?\n\s*\[)[\s\S])*";
            return Regex.Replace(content, pattern, "");
        }

        void UpdateStatus(McpStatus status, string message = "")
        {
            Client.SetStatus(status, message);
            MCPSettingsManager.Settings.UpdateClientState(Client.name, status, message);
            MCPSettingsManager.MarkDirty();
        }
    }
}
