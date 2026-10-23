---
uid: mcp-landing
---

# MCP tools in Assistant

[!include[](../snippets/mcp-tool-deprecation-notice.md)]

Connect Assistant to external tools by configuring Model Context Protocol (MCP) servers.

MCP enables Assistant to discover and use tools provided by MCP external servers running on your local machine. These tools allow Assistant to interact with systems and workflows outside the Unity Editor, such as version control tools or custom utilities.

With this integration, Assistant acts as an MCP client that connects to external MCP servers, which act as tool providers. It connects to configured MCP servers, reads their tool definitions, and invokes those tools during conversations.

To use MCP, you need to configure the MCP servers and understand how Assistant interprets and uses the tools that those servers expose. If you're new to Assistant, begin with [Work with Assistant](xref:get-started), then return here to integrate MCP. If you're looking for other tool integrations, refer to [Assistant tools](xref:assistant-tools).

| Topic	| Description |
| ----- | ----------- |
| [MCP integration in Assistant](xref:mcp-overview)	| Connect Assistant to Model Context Protocol (MCP) servers so the AI can call external tools and data during chats. |
| [Configure MCP servers](xref:mcp-configure) | Enable the MCP client, configure servers, and use their tools in Assistant conversations. |

## Additional resources

- [Open and use Assistant](xref:get-started)
- [Assistant tools](xref:assistant-tools)
