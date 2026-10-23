---
uid: custom-tools
---

# Create custom tools

Create custom tools that Assistant can call during conversations to perform actions in the Unity Editor, such as querying project data or creating files.

To create a tool, you define a C# method. To make the method available as a tool in Assistant, the method must be public and static, and it must have the `[AgentTool]` attribute. Each parameter must have the `[ToolParameter]` attribute. Assistant discovers these tools automatically and uses the descriptions you provide to decide when to call them and what values to pass.

Assistant can use custom tools directly during conversations or reference them in skills to support reusable workflows. When a skill declares tool IDs in the `tools` field of its `SKILL.md` frontmatter, Assistant uses those tools as part of the skill workflow. For more information on how skills use tools, refer to [About skills](xref:skills-overview) and [Create skills from the filesystem](xref:skills-filesystem).

## Prerequisites

Before you create custom tools, ensure you have the following:

- Install and set up [Assistant](xref:install-assistant).
- Have a Unity project where you can add C# scripts.
- Be familiar with how to create static C# methods and work with Unity Editor scripts.

## Define and implement a custom tool

Use the `Unity.AI.Assistant.FunctionCalling` namespace to define methods that Assistant can discover and call.

### Create a static tool method

To define a custom tool:

1. Create or open a C# script in your project.
2. Add the following namespace:

   ```csharp
   using Unity.AI.Assistant.FunctionCalling;
   ```
3. Define a `public static` method for the action you want Assistant to perform.
4. Add the `[AgentTool]` attribute to the method.
5. Add the `[ToolParameter]` attribute to every parameter.
6. Implement the method logic.

### Configure the `AgentTool` attribute

To describe how Assistant uses the tool:

1. Add the `AgentTool` attribute to the method:

   ```csharp
   [AgentTool(description, id)]
   ```
2. Set the following values:

   - `description`: Describe what the tool does and when Assistant might use it. This value can't be empty. Assistant relies on this description to decide whether to call the tool, so it must be clear, concise, and specific.
   - `id`: Provide a unique tool identifier using dot-separated PascalCase, for example, `MyTools.CreateFile`.

### Configure tool parameters

To define how Assistant supplies parameter values:

1. Add the `ToolParameter` attribute to each parameter in your method:

   ```
   [ToolParameter(description)]
   ```

2. Provide a description for each parameter that explains the expected value.
3. (Optional) Assign a default value to make a parameter optional.

### Follow implementation rules

To ensure that Assistant can use your tool correctly:

- Ensure the method is `static`.
- Ensure the tool ID is unique across all tools.
- Ensure every parameter includes a `[ToolParameter]` attribute.
- The `description` argument for `[AgentTool]` can't be empty, and must clearly and concisely describe what the tool does. Assistant uses this description to decide whether to call the tool, so make it specific and unambiguous.
- The description argument for `[ToolParameter]` can’t be empty.
- Use supported parameter types:
   - `string`, `int`, `long`, `float`, `double`, `bool`
   - Enums
   - Arrays and `List<T>`
   - `Dictionary<string, T>`
   - Custom classes or structs with public properties
- Use a supported return type:
   - Any serializable type
   - `void`
   - `Task<T>` for asynchronous tools

Use clear, action-oriented descriptions. For example, use `Creates a text file with given content` instead of `Handles files`.

## Example: tool with no parameters

Use the following code to create a tool that returns the current date and time:

```csharp
using Unity.AI.Assistant.FunctionCalling;

public static class MyTools
{
    [AgentTool("Returns the current date and time.", "MyTools.GetDateTime")]
    public static string GetDateTime()
    {
        return System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
```

## Example: tool with an optional parameter

Use the following code to create a tool with an optional parameter:

```csharp
[AgentTool("Counts C# scripts in a folder.", "MyTools.CountScripts")]
public static int CountScripts(
    [ToolParameter("Folder path relative to the project root.")]
    string folderPath = "Assets")
{
    var fullPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), folderPath);
    if (!System.IO.Directory.Exists(fullPath))
        return 0;

    return System.IO.Directory.GetFiles(fullPath, "*.cs", System.IO.SearchOption.AllDirectories).Length;
}
```
If the user doesn't specify a folder, Assistant uses `Assets`.

## Example: tool with required parameters

Use the following code to create a tool with required parameters:

```csharp
[AgentTool("Creates a text file with the given content.", "MyTools.CreateTextFile")]
public static string CreateTextFile(
    [ToolParameter("File path relative to the project root.")] string path,
    [ToolParameter("Text content to write.")] string content)
{
    var fullPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), path);
    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));
    System.IO.File.WriteAllText(fullPath, content);
    UnityEditor.AssetDatabase.Refresh();

    return $"File created at {path}";
}
```

## Example: Tool with an enum parameter

Use the following code to create a tool that uses an enum:

```csharp
public enum LogLevel { Info, Warning, Error }

[AgentTool("Logs a message to the Unity console.", "MyTools.Log")]
public static void Log(
    [ToolParameter("The message to log.")] string message,
    [ToolParameter("Severity level.")] LogLevel level = LogLevel.Info)
{
    switch (level)
    {
        case LogLevel.Warning:
            UnityEngine.Debug.LogWarning(message);
            break;
        case LogLevel.Error:
            UnityEngine.Debug.LogError(message);
            break;
        default:
            UnityEngine.Debug.Log(message);
            break;
    }
}
```

Assistant selects from `Info`, `Warning`, or `Error` based on the context.

After completing these steps, the Assistant can discover your custom tools and call them during conversations when required. If Assistant doesn't use your tool as expected, review the tool description and parameter descriptions to ensure they clearly explain when and how the tool might be used.

## Additional resources

- [AI gateway](xref:ai-gateway-landing)
- [MCP tools in Assistant](xref:mcp-landing)
- [MCP server](xref:unity-mcp-landing)
- [Skills](xref:skills-landing)