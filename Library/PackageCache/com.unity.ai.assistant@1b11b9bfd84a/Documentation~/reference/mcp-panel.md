---
uid: mcp-panel
---

# Assistant MCP Extensions page reference

[!include[](../snippets/mcp-tool-deprecation-notice.md)]

Use the **Assistant MCP Extensions** page to enable the Model Context Protocol (MCP) client, open and edit the configuration file, refresh the settings, and review server status.

For setup, refer to [Configure and use MCP servers](xref:mcp-configure).

The **Assistant MCP Extensions** page contains the following tools:

**General Settings**

| Setting	| Description |
| --------- | ----------- |
| **Enable MCP Tools** | Toggle to enable or disable the MCP client for the current project. |
| **Tool Call Timeout (seconds)** | Specifies how long Assistant waits for an MCP tool call to complete before timing out. Increase this value if your MCP tools run long operations or invoke external programs that take longer to respond. |

**Server Configuration**

| Setting | Description |
| ------- | ----------- |
| **Open** | Opens the MCP configuration file for editing. |
| **Refresh File and Servers** | Reloads the configuration and restarts servers. Use this after you make any edits. |

**Path Configuration**

The settings in this section apply only to `stdio` servers that Unity starts as local subprocesses.

| Setting | Description |
| ------- | ----------- |
| **Path Accessible by Unity** | Displays the PATH environment variable as seen by the Unity Editor process. This is often different from your shell PATH. |
| **User Path** | Provide the PATH that MCP servers use when launched from Unity. Paste the full PATH from your terminal or add specific directories (for example, C:\Program Files\Git\bin (macOS: `/opt/homebrew/bin`)) so Unity can resolve the required executables. When you change this field, select **Refresh File and Servers** to apply it. |

**Servers**

| Setting | Description |
| ------- | ----------- |
| **Server Inspector** | Shows server status, messages, tool manifest, and connection details. If the server fails to start or connect, the inspector includes error details and a link to troubleshooting guidance. |
| **Server Name** |	The display name of the server from your configuration. If the name starts with a tilde (`~`), the server is hidden and won’t start. |
| **Server Status** | The current state of the server. For example, `FailedToStart` or `StartedSuccessfully`. |
| **Server Message** | Displays results with error text from the server. For example, `Failed to connect to server`. |
| **Tools** | Shows the number of tools discovered from the server’s manifest. It also lists the available tool names, descriptions, and parameters required by the server.|

> [!NOTE]
> The servers don't update automatically. Use **Refresh File and Servers** to update servers, re-establish connections or refresh available tools.

## Additional resources

- [Model Context Protocol](xref:mcp-landing)
- [Troubleshooting MCP server issues](xref:mcp-troubleshooting)
