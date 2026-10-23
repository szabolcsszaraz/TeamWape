---
uid: unity-mcp-reference
---

# Unity MCP Server configuration page reference

[!include[](../snippets/mcp-deprecation-notice.md)]

Configure the MCP server bridge and manage connections, tools, and diagnostic settings for AI client integration.

Use the **Unity MCP Server** configuration page to control how external AI clients connect to the Unity Editor through Model Context Protocol (MCP). This page displays the bridge status, active and previous client connections, and the tools that Unity exposes to the connected clients.

To access the **Unity MCP Server** configuration page, go to **Edit** > **Project Settings** > **AI** > **Unity MCP Server**.

The **Unity MCP Server** configuration page contains the following settings.

## General Settings

The **General Settings** section contains the following settings:

| **Setting** | **Description** |
| ----------- | -------------- |
| **Show Debug Logs** | Enables detailed logging for MCP bridge activity. |
| **Validation Level (dropdown)** | Controls the validation behavior for the `Unity_ManageScript` tool. The available options are: basic, standard, comprehensive, and strict. |
| **Auto-approve in Batch Mode** | When enabled, automatically approves all incoming MCP client connections when Unity runs in batch mode. |

## Unity Bridge

The **Unity Bridge** section displays whether the MCP bridge is running. When running, external AI clients can connect to Unity. Use the **Start** or **Stop** controls to manage the bridge state.

## Connected Clients

The **Connected Clients** section displays AI clients that are currently connected to the Unity Editor. These clients can invoke MCP server tools during active sessions.

## Other Connections

The **Other Connections** section displays previously connected clients that aren't currently active. Use this information to track connection history and recognize known clients.

## Tools

The **Tools** section displays a list of tools available for the MCP server.

## Integrations

The **Integrations** section displays a list of supported AI clients that can connect to the Unity Editor through MCP server. For example, Cursor, Claude Code, Windsurf, and others.

The **Integrations** section contains the following settings:

| **Setting** | **Description** |
| ----------- | -------------- |
| Status indicator | Shows whether the client is configured. |
| **Configure** | Opens the configuration flow for the selected client. |
| **Check Status** | Verifies whether the selected client is correctly configured and can connect to the MCP server bridge. |
| **Locate** | Opens the location of the selected client. |
| **Locate Server** | Opens the location of the MCP server relay binary on your system. Use this when you need to manually configure a client with the correct executable path. |

## Additional resources

- [AI client integration with Unity (MCP)](xref:unity-mcp-overview)
- [Get started with MCP server](xref:unity-mcp-get-started)
- [Register custom MCP tools](xref:unity-mcp-tool-registration)
