---
uid: ai-gateway-intro
---

# AI gateway overview

Run third-party coding agents in Assistant so you can use your preferred provider inside the Unity Editor.

AI gateway adds an agent selector to the **Assistant** window. Agents run locally on your machine using your provider credentials. Assistant routes your prompts to the selected agent and returns responses in the same conversation.

AI gateway supports CLI-based coding agents, such as Claude Code, Cursor CLI, Codex CLI, and Gemini CLI. AI gateway uses bundled versions of Codex CLI and Gemini CLI, which might differ from the versions installed locally on your machine. Depending on your license and configuration, these agents can chat, generate code, or perform actions directly in Unity using Model Context Protocol (MCP) tools.

To connect Assistant to external MCP servers that you host or configure, refer to the [Use MCP tools in Assistant](xref:mcp-landing) documentation.

AI gateway provides a secure, Unity-managed environment to run supported agents, and control permissions and entitlements. Use AI gateway to perform the following tasks:

- Ask questions about your project.
- Request code changes.
- Generate scripts and assets.
- Debug and fix issues.
- Modify GameObjects, scenes, and prefabs.

Use AI gateway when you want:

- A preferred third-party agent inside Unity.
- Provider-specific models and commands in the Assistant workflow.
- A tool-enabled workflow that lets the agent act directly in the Unity Editor.

## Additional resources

- [Configure AI gateway](xref:ai-gateway-get-started)
- [Use third-party agents in Assistant](xref:use-third-party-agents)