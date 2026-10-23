---
uid: unity-mcp-troubleshooting
---

# Troubleshoot MCP server bridge issues

[!include[](../snippets/mcp-deprecation-notice.md)]

Resolve issues that might occur when you connect AI clients to the Unity Editor through the Unity Model Context Protocol (MCP) bridge.

## Bridge doesn't start

The MCP server bridge fails to start. This prevents any AI client from connecting to the Unity Editor.

### Symptoms

You might note one or more of the following symptoms:

- The MCP server settings page (**AI** > **Unity MCP Server**) shows **Stopped**.
- The bridge doesn't start automatically.
- No clients can connect to Unity.

### Cause

The bridge might fail to start due to script compilation errors, incomplete package installation, or because it was explicitly stopped.

### Resolution

To resolve this issue:

1. Open the Unity **Console** and fix any compilation errors.
2. Go to **Edit** > **Project Settings** > **AI** > **Unity MCP Server** and select **Start**.
3. Verify the `com.unity.ai.assistant` package is installed in **Window** > **Package Manager**.
4. Restart the Unity Editor if the issue persists.

## MCP client can't connect

The AI client can't connect to Unity, which prevents it from discovering or invoking Unity tools.

### Symptoms

You might note one or more of the following symptoms:

- The AI client (Cursor, Claude Code, etc.) reports connection errors.
- The client can't find Unity tools.

### Cause

This issue can occur when:

- Unity isn't running or the bridge didn't start.
- The relay binary is missing from `~/.unity/relay/`.
- The MCP client configuration points to the wrong executable path.
- The `--mcp` flag is missing from the client configuration.

### Resolution

To resolve this issue:

1. Confirm the bridge is running in **Project Settings** > **AI** > **Unity MCP Server**.
2. Verify the relay binary exists:
   - **macOS**: `~/.unity/relay/relay_mac_arm64.app/` (or `relay_mac_x64.app/`)
   - **Windows**: `%USERPROFILE%\.unity\relay\relay_win.exe`
   - **Linux**: `~/.unity/relay/relay_linux`
3. Verify your client configuration includes `"args": ["--mcp"]`.
4. Use the **Integrations** section in MCP server settings to reconfigure the client.
5. Restart both Unity and your MCP client.

## Unity Editor freezes or crashes when you use `get_components` on UI objects

The Unity Editor can freeze or crash when an MCP client calls `get_components` on UI objects, such as Canvas or elements in a UI hierarchy.

### Symptoms

You might note one or more of the following symptoms:

- The Unity Editor freezes or becomes unresponsive.
- Unity crashes after an MCP tool call.
- The Console window shows repeated errors such as `Assertion failed on expression: 'ValidTRS()'`.
- The Console window shows deep or repeated `Newtonsoft.Json` serialization logs.
- The MCP client disconnects when the issue occurs.

### Cause

Some UI component properties can trigger invalid transform (TRS) states or cause unbounded serialization when accessed through MCP. This can make the Unity Editor unstable.

This issue occurs with the following UI components:

- Canvas
- CanvasScaler
- GraphicRaycaster
- RectTransform

To reduce this risk, MCP filters some unsafe properties on common components, such as Transform and Camera, but similar filtering isn't currently applied to these UI components.

### Resolution

To avoid this issue, don't call `get_components` on Canvas objects or on GameObjects in a UI hierarchy.

## Connection pending/not approved

The MCP client connects to Unity but can't invoke tools because the connection hasn't been approved.

### Symptoms

You might note one or more of the following symptoms:

- Your MCP client connects but can't invoke any tools.
- The MCP server settings page shows the connection under **Pending Connections**.

### Cause

Direct connections from external MCP clients require user approval. The connection is waiting for you to accept it.

### Resolution

To resolve this issue:

1. Go to **Edit** > **Project Settings** > **AI** > **Unity MCP Server**.
2. In the **Pending Connections** section, review the client details.
3. Select **Allow** to approve the connection.

Once approved, Unity remembers the client and reconnects automatically in future sessions.

## Tools not discovered

The MCP client connects successfully, but no Unity tools (or only some tools) are available.

### Symptoms

You might note one or more of the following symptoms:

- The MCP client connects but lists no Unity tools.
- Some custom tools are missing.

### Cause

This issue can occur when:

- Tool registration scripts contain compilation errors.
- The `[McpTool]` attribute is missing or incorrectly applied.
- Custom tools are defined in assemblies not scanned by `TypeCache`.
- Tools are disabled in the MCP server settings.

### Resolution

To resolve this issue:

1. Check the Unity **Console** for compilation errors in tool scripts and fix them.
2. Verify that tool methods are `public static` and decorated with `[McpTool]`.
3. For class-based tools, ensure they implement `IUnityMcpTool` or `IUnityMcpTool<T>` and have a parameterless constructor.
4. In MCP server settings, check the **Tools** section to ensure the tools are enabled.
5. In MCP server settings, enable **Show Debug Logs** for detailed discovery information.

## Relay binary not installed

The relay binary is not installed, which prevents the MCP client from starting the MCP server.

### Symptoms

You might note one or more of the following symptoms:

- The relay binary is missing from `~/.unity/relay/`.
- The MCP client fails to start the server.

### Cause

The `ServerInstaller` runs at editor startup and copies relay binaries from the package. It might have been interrupted or the package directory isn't accessible.

### Resolution

To resolve this issue:

1. Restart the Unity Editor to trigger the installer.
2. Verify the package directory exists: `Packages/com.unity.ai.assistant/RelayApp~/`.
3. Check the Unity **Console** for installation warnings.
4. If required, manually locate and copy the relay binary using the **Locate Server** button in MCP server settings.

## Performance issues

Unity tools respond slowly or time out when invoked by the MCP client.

### Symptoms

You might note one or more of the following symptoms:

- Slow responses from Unity tools.
- Timeout errors in the MCP client.

### Cause

Unity might be busy with heavy operations, such as asset imports, builds, or script compilation, or system resources are limited.

### Resolution

To resolve this issue:

1. Wait for Unity to finish any ongoing operations (check the progress bar).
2. Restart Unity to clear cached state.
3. Reduce the number of concurrent MCP client connections.
4. Enable **Show Debug Logs** to identify bottlenecks.

## Enable debug logging

For detailed diagnostic information, enable debug logging in the MCP server settings page:

1. Go to **Edit** > **Project Settings** > **AI** > **Unity MCP Server**.
2. Check **Show Debug Logs**.

Debug logs include connection attempts, tool discovery details, command execution traces, and error information.

## Unity Editor freezes when you use MCP tools

If the Unity Editor freezes or becomes unresponsive after you invoke an MCP tool, the issue might be caused by a tool-specific error.

### Resolution

To investigate this issue:

1. Enable **Show Debug Logs** in the MCP server settings.
2. Identify the last MCP tool call and its arguments.
3. Restart Unity if required to recover the Editor.
4. Report the issue with details of the tool call and logs.

## Additional resources

- [AI client integration with Unity (MCP)](xref:unity-mcp-overview)
- [Get started with MCP server](xref:unity-mcp-get-started)
- [Register custom MCP tools](xref:unity-mcp-tool-registration)
- [Unity MCP Server configuration page reference](xref:unity-mcp-reference)
