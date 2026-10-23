---
uid: unity-mcp-overview
---

# AI client integration with Unity (MCP)

[!include[](../snippets/mcp-deprecation-notice.md)]

Connect external AI clients to the Unity Editor so they can call Unity tools through Model Context Protocol (MCP).

MCP server connects large language model (LLM)-based agents, such as Claude Code and Cursor, to the Unity Editor through standardized MCP tools. This allows AI agents to interact directly with your project, including managing scenes, assets, scripts, and console output, and to automate in-editor workflows.

With MCP server, Unity acts as an MCP server that exposes tools, while external AI clients act as MCP clients that discover and invoke those tools. External clients connect through a relay process and communicate with the Unity Editor through the MCP bridge, which exposes built-in and custom tools. This provides a structured and secure way for AI agents to query Unity data and perform actions.

MCP server is built on the open [Model Context Protocol](https://modelcontextprotocol.io/).

## How MCP server works

When Unity starts, the MCP bridge launches automatically and opens a local interprocess communication (IPC) channel (called `pipes` on Windows and `Unix sockets` on macOS/Linux). The relay binary, installed to `~/.unity/relay/`, runs as an MCP server process that AI clients start. The relay connects to the MCP server bridge and exposes Unity's capabilities as MCP tools.

During a session, AI clients discover available tools, send requests with arguments, and receive responses from Unity through this connection.

### Architecture overview

The following diagram shows how AI clients communicate with Unity through the MCP bridge and relay.

```
AI Client (Cursor, Claude Code, etc.)
    |
    | MCP protocol (stdio)
    |
Relay binary (~/.unity/relay/) with --mcp flag
    |
    | IPC (named pipe / Unix socket)
    |
Unity Editor (MCP Bridge)
    |
    | McpToolRegistry
    |
Registered tools (built-in + custom)
```

### Connection security

MCP server uses a combination of automatic trust and user approval to manage client connections.

- **AI gateway connections** are automatically approved and don't require user interaction.
- **Direct connections** from external MCP clients require user approval in Project Settings. Approved clients are remembered for future sessions.

## Key features

The key features of MCP server are as follows:

- **Tool registration system**: Create and register MCP tools using attributes, interfaces, or runtime APIs.
- **Built-in tools**: Automate Unity tasks, such as scene management, asset operations, script editing, and console access.
- **Dynamic discovery**: Detect tools and register them automatically on Unity Editor startup.
- **Connection security**: Direct connections require explicit user approval; gateway connections are trusted.
- **Multi-client support**: Connect multiple MCP clients to the same Unity instance simultaneously.

## Tools in MCP server

MCP server exposes the capabilities of Unity Editor to external AI clients through MCP tools.

These tools represent actions that clients can invoke, such as manage scenes, work with assets, edit scripts, or read console output. Unity provides built-in tools and allows you to define custom tools.

When the Unity Editor starts, it discovers and registers the available tools automatically. Each tool defines a name, description, input parameters, and optional output schema, which AI clients use to determine how to call the tool.

To learn how to create and register custom tools, refer to [Register custom MCP tools](xref:unity-mcp-tool-registration).

## Project Settings

Use the Unity MCP Server page to monitor connections and configure how the MCP bridge behaves. For details about available settings and options, refer to the [Unity MCP Server configuration page reference](xref:unity-mcp-reference).

Configure the MCP server bridge in **Edit** > **Project Settings** > **AI** > **Unity MCP Server**.

The settings page provides:

- Bridge status and start or stop controls.
- Connected and pending client connections.
- A list of available tools with enable or disable options.
- Client integration configuration.

> [!NOTE]
> The **Assistant MCP Extensions** page (under **AI** > **Assistant MCP Extensions**) configures MCP servers that the in-editor AI assistant *connects to*. The **Unity MCP Server** page configures the MCP server that Unity *exposes* to external AI clients.

## Additional resources

- [Get started with MCP server](xref:unity-mcp-get-started)
- [Register custom MCP tools](xref:unity-mcp-tool-registration)
- [Troubleshoot MCP server bridge issues](xref:unity-mcp-troubleshooting)
