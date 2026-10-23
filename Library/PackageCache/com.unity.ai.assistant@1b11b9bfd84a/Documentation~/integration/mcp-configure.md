---
uid: mcp-configure
---

# Configure MCP servers

[!include[](../snippets/mcp-tool-deprecation-notice.md)]

Configure Model Context Protocol (MCP) servers and use their tools in Assistant conversations.

When you configure MCP, Assistant can find and use tools provided by MCP servers. Servers can run locally as subprocesses (`stdio` transport), or run remotely (`http` transport). This allows the Assistant to access external systems, such as Git, online documentation or custom automation.

## Prerequisites

Before you start, make sure you meet the following prerequisites:

- Understand and know how to use the preferred MCP server.
- Install the required MCP server and any required dependencies on your machine.
- Confirm the server command runs successfully in a terminal.
- (Recommended) [Install and use MCP Inspector](https://modelcontextprotocol.io/docs/tools/inspector) to validate server behavior outside Unity.

To configure the MCP servers, follow these steps:

1. In the Unity Editor, select **Edit** > **Project Settings** > **AI** > **Assistant MCP Extensions**.

2. In the **Server Configuration** section, select **Open** to open the MCP configuration file.

3. In the configuration file, enable MCP and add or edit the server entries.

   The MCP configuration file uses the following structure:

   | JSON field | Type | Description |
   | ---------- | ---- | ----------- |
   | `enabled` | Boolean | Enables or disables MCP integration in Unity. If `false`, Unity doesn’t start or connect to MCP servers. |
   | `path` | String | Extra PATH entries that Unity uses when it starts local MCP servers. On macOS/Linux, separate the entries with `:`. On Windows, separate the entries with `;`. Only applies to `stdio` servers. |
   | `mcpServers` | Object | Collection of MCP servers to which Unity can connect. Each key is the display name of the server. Prefix a server name with `~` to hide it from Unity. |
   | `mcpServers.<serverName>.type` | String | Optional. Transport type. Supported values: `stdio` for local subprocess servers, `http` for remote servers reached over Streamable HTTP. Defaults to `stdio` when omitted. |
   | `mcpServers.<serverName>.command` | String | Required for `stdio` servers. Executable that Unity runs to start the MCP server. This must match the command that works in your terminal or MCP Inspector. Ignored for `http` servers. |
   | `mcpServers.<serverName>.args` | Array of strings | Optional for `stdio` servers. Arguments passed to the command. Include the same flags and arguments you validated in a terminal or MCP Inspector. Ignored for `http` servers. |
   | `mcpServers.<serverName>.env` | Object | Optional for `stdio` servers. Environment variables passed to the MCP server process. Ignored for `http` servers. |
   | `mcpServers.<serverName>.url` | String | Required for `http` servers. Full URL of the remote MCP endpoint. Must use the `http://` or `https://` scheme. Ignored for `stdio` servers. |
   | `mcpServers.<serverName>.headers` | Object | Optional for `http` servers. HTTP headers sent on every request to the remote endpoint. Use this for static-headers authentication such as `Authorization` or `X-API-Key`. Ignored for `stdio` servers. |

   > [!NOTE]
   > To keep a server in your configuration file but prevent Unity from starting it, prefix the server name with a tilde (~). For more information, refer to [Hide a server from Unity with ~](#hide-a-server-from-unity).

4. Save the configuration file.

5. For `stdio` servers, go back to the **Assistant MCP Extensions** page and configure the PATH values under the **Path Configuration** section. Skip this step if you only configured `http` servers.


   1. Review **Path Accessible by Unity** to verify the PATH that the Unity Editor currently uses.

   2. In **User Path**, paste the full shell PATH or add directories for the required executables so that Unity can resolve the same toolchain as your terminal.

      For example, if the server uses `uvx`, ensure Unity can resolve the `uvx` executable path (such as `/opt/homebrew/bin` on macOS).

   > [!NOTE]
   > Some servers require multiple executables to be available. If a server works in your terminal but fails in Unity, the issue is commonly related to PATH. For more information, refer to [Troubleshooting MCP server issues](xref:mcp-troubleshooting).

6. Select **Refresh File and Servers** to apply changes and start the MCP server.

   The **Servers** section lists each server with its status.
7. Select a server and then select **Inspect**.

   - If the server start successfully, **Inspect** shows the server details and the tools it exposes.
   - If the server fails to start or connect, **Inspect** shows the error details and logs.
8.  If you encounter an error, select [**View troubleshooting documentation**](xref:mcp-troubleshooting).

You have successfully configured the MCP servers and can start to use their tools in Assistant.

## Example configuration file

The following example shows a configuration file with both a local `stdio` server and a remote `http` server. Replace values with the command, URL, and arguments required by your MCP servers.

```
{
  "enabled": true,
  "path": "",
  "mcpServers": {
    "git": {
      "type": "stdio",
      "command": "uvx",
      "args": [
        "mcp-server-git"
      ],
      "env": {}
    },
    "example-service": {
      "type": "http",
      "url": "https://mcp.example.com/v1",
      "headers": {
        "Authorization": "Bearer <your-token>"
      }
    }
  }
}
```

> [!NOTE]
> Treat the configuration file as sensitive. Tokens placed in `env` or `headers` are stored in plain text under `UserSettings/mcp.json`. Don’t commit it to source control.

## Hide a server from Unity

If you want to keep an MCP server in your configuration but don't want Unity discover or start it, follow these steps:

1. In the MCP configuration file, prefix the server name with a tilde (**~**), for example, `~Example Git MCP`.

2. Save the configuration and select **Refresh File and Servers**.

## Use MCP tools in Assistant

After your server is running, you can use it through Assistant to ask questions that require external tool calls.

For example, `Use the Git MCP server to show the last 10 commits in this repository: <path>.` or `Summarize recent changes in the repo at <path>.`.

If Assistant uses the wrong folder or returns unexpected output, include explicit paths and confirm expected tool parameters in the MCP Client inspector.

## Additional resources

- [Assistant MCP Extensions page reference](xref:mcp-panel)
- [Troubleshooting MCP server issues](xref:mcp-troubleshooting)
- [MCP Inspector](https://modelcontextprotocol.io/docs/tools/inspector)
