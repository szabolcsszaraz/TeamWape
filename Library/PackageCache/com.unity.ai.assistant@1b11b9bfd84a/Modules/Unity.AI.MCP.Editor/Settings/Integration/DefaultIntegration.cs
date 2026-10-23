using System;
using System.IO;
using Unity.AI.MCP.Editor.Helpers;
using Unity.AI.MCP.Editor.Models;
using Unity.AI.MCP.Editor.Settings.Utilities;

namespace Unity.AI.MCP.Editor.Settings.Integration
{
    /// <summary>
    /// Default integration for MCP clients that use standard configuration file structure.
    /// Manages Unity MCP server configuration in client-specific config files.
    /// </summary>
    class DefaultIntegration : IClientIntegration
    {
        /// <summary>
        /// Result of a configuration operation.
        /// </summary>
        struct ConfigResult
        {
            /// <summary>
            /// Indicates whether the configuration operation was successful.
            /// </summary>
            public bool Success;

            /// <summary>
            /// Message describing the result of the configuration operation.
            /// </summary>
            public string Message;
        }

        /// <summary>
        /// Gets the MCP client this integration is associated with.
        /// </summary>
        public McpClient Client { get; }

        /// <summary>
        /// Initializes a new instance of the DefaultIntegration class.
        /// </summary>
        /// <param name="client">The MCP client to configure.</param>
        /// <exception cref="ArgumentNullException">Thrown when client is null.</exception>
        public DefaultIntegration(McpClient client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>
        /// Configures the MCP client by adding Unity MCP server to its configuration file.
        /// </summary>
        /// <returns>True if configuration was successful, false otherwise.</returns>
        public bool Configure()
        {
            string configPath = GetConfigPath();
            if (string.IsNullOrEmpty(configPath))
            {
                UpdateStatus(McpStatus.Error, "Config path not available for current platform");
                return false;
            }

            string serverPath = PathUtils.GetServerPath();
            if (string.IsNullOrEmpty(serverPath))
            {
                UpdateStatus(McpStatus.Error, "Server not found");
                return false;
            }

            var result = WriteConfig(serverPath, configPath);
            McpStatus status = result.Success ? McpStatus.Configured : McpStatus.Error;
            UpdateStatus(status, result.Message);

            return result.Success;
        }

        /// <summary>
        /// Disables the integration by removing Unity MCP server from the client's configuration.
        /// </summary>
        /// <returns>True if the configuration was successfully removed, false otherwise.</returns>
        public bool Disable()
        {
            string configPath = GetConfigPath();
            if (string.IsNullOrEmpty(configPath))
            {
                UpdateStatus(McpStatus.Error, "Config path not available for current platform");
                return false;
            }

            try
            {
                if (!File.Exists(configPath))
                {
                    UpdateStatus(McpStatus.NotConfigured, "Configuration file not found");
                    return true; // Not an error if file doesn't exist
                }

                // Read and parse existing config
                string existingContent = File.ReadAllText(configPath);
                var existingConfig = Newtonsoft.Json.Linq.JObject.Parse(existingContent);

                // Check if mcpServers exists and contains our entry
                if (existingConfig[Client.serversJsonKey] != null)
                {
                    var mcpServers = (Newtonsoft.Json.Linq.JObject)existingConfig[Client.serversJsonKey];

                    if (mcpServers[MCPConstants.jsonKeyIntegration] != null)
                    {
                        // Remove our entry
                        mcpServers.Remove(MCPConstants.jsonKeyIntegration);

                        // Write back the modified config
                        string updatedConfig = existingConfig.ToString(Newtonsoft.Json.Formatting.Indented);
                        File.WriteAllText(configPath, updatedConfig);

                        UpdateStatus(McpStatus.NotConfigured, "Configuration removed");
                        return true;
                    }
                }

                UpdateStatus(McpStatus.NotConfigured, "Unity MCP entry not found in configuration");
                return true;
            }
            catch (Exception ex)
            {
                UpdateStatus(McpStatus.Error, $"Failed to remove configuration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks whether the MCP client is properly configured with Unity MCP server.
        /// Updates the client status based on the configuration state.
        /// </summary>
        public void CheckConfiguration()
        {
            string configPath = GetConfigPath();
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                UpdateStatus(McpStatus.NotConfigured, "Configuration file not found");
                return;
            }

            bool isValid = ValidateConfigFile(configPath, Client.serversJsonKey);
            McpStatus status = isValid ? McpStatus.Configured : McpStatus.NotConfigured;
            string message = isValid ? "Configuration is valid" : "Not configured";

            UpdateStatus(status, message);
        }

        /// <summary>
        /// Checks if the MCP client has any missing dependencies.
        /// </summary>
        /// <param name="warningText">Output parameter for warning text if dependencies are missing.</param>
        /// <param name="helpUrl">Output parameter for help URL if dependencies are missing.</param>
        /// <returns>True if dependencies are missing, false otherwise.</returns>
        public bool HasMissingDependencies(out string warningText, out string helpUrl)
        {
            warningText = string.Empty;
            helpUrl = string.Empty;
            return false;
        }

        string GetConfigPath()
        {
            return PlatformUtils.GetConfigPathForClient(Client);
        }

        ConfigResult WriteConfig(string serverPath, string configPath)
        {
            try
            {
                string mainFile = PathUtils.GetServerMainFile(serverPath);
                if (!File.Exists(mainFile))
                {
                    ServerInstaller.InstallOrUpdateRelay();
                    if (!File.Exists(mainFile))
                        return new ConfigResult {Success = false, Message = "Server main file not found"};
                }

                string config = CreateMcpClientConfig(Client, serverPath);

                if (File.Exists(configPath))
                {
                    return UpdateExistingConfig(configPath, config);
                }

                string directory = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(configPath, config);
                return new ConfigResult {Success = true, Message = "Configuration created successfully"};
            }
            catch (Exception e)
            {
                return new ConfigResult {Success = false, Message = e.Message};
            }
        }

        static string CreateMcpClientConfig(McpClient client, string serverPath)
        {
            try
            {
                string mainFile = PathUtils.GetServerMainFile(serverPath);
                if (string.IsNullOrEmpty(mainFile))
                    return string.Empty;

                // Escape backslashes for JSON
                string escapedMainFile = mainFile.Replace("\\", "\\\\");

                // Relay binary with --mcp flag to run in MCP mode
                var command = escapedMainFile;

                return $@"{{
  ""{client.serversJsonKey}"": {{
    ""{MCPConstants.jsonKeyIntegration}"": {{
      ""command"": ""{command}"",
      ""args"": [""--mcp""],
      ""env"": {{}}
    }}
  }}
}}";
            }
            catch
            {
                return string.Empty;
            }
        }

        ConfigResult UpdateExistingConfig(string configPath, string newConfig)
        {
            try
            {
                string serversKey = Client.serversJsonKey;

                // Read and parse existing config
                string existingContent = File.ReadAllText(configPath);
                var existingConfig = Newtonsoft.Json.Linq.JObject.Parse(existingContent);

                // Parse the new config to get our server entry
                var newConfigObj = Newtonsoft.Json.Linq.JObject.Parse(newConfig);

                // Ensure servers container exists in the existing config
                if (existingConfig[serversKey] == null)
                {
                    existingConfig[serversKey] = new Newtonsoft.Json.Linq.JObject();
                }

                // Get the servers object
                var mcpServers = (Newtonsoft.Json.Linq.JObject)existingConfig[serversKey];
                var newMcpServers = (Newtonsoft.Json.Linq.JObject)newConfigObj[serversKey];

                // Merge our unity-mcp entry into the existing servers
                mcpServers[MCPConstants.jsonKeyIntegration] = newMcpServers[MCPConstants.jsonKeyIntegration];

                // Write back with nice formatting
                string mergedConfig = existingConfig.ToString(Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(configPath, mergedConfig);

                return new ConfigResult {Success = true, Message = "Configuration updated successfully"};
            }
            catch (Exception e)
            {
                return new ConfigResult {Success = false, Message = e.Message};
            }
        }

        static bool ValidateConfigFile(string configPath, string serversKey)
        {
            try
            {
                if (!File.Exists(configPath)) return false;

                string content = File.ReadAllText(configPath);
                if (string.IsNullOrWhiteSpace(content)) return false;

                var config = Newtonsoft.Json.Linq.JObject.Parse(content);
                return config?[serversKey]?[MCPConstants.jsonKeyIntegration] != null;
            }
            catch
            {
                return false;
            }
        }

        void UpdateStatus(McpStatus status, string message = "")
        {
            Client.SetStatus(status, message);
            MCPSettingsManager.Settings.UpdateClientState(Client.name, status, message);
            MCPSettingsManager.MarkDirty();
        }
    }
}
