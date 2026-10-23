---
uid: assistant-api
---

# Run Assistant from code

Use the `AssistantApi` class to start Assistant from your own editor scripts. You can open the Assistant window with a pre-filled prompt, show a popup to edit the prompt before running, or run an agent headless to receive its final answer as a return value.

This workflow is useful when you want to trigger Assistant from custom editor UI, such as menu items or Inspector buttons. You can also attach scene objects, project assets, or image data as context for the prompt..

## Prerequisites

Before you call the API, ensure you have the following:

- Install and set up [Assistant](xref:install-assistant).
- Have a Unity project where you can add C# editor scripts.
- Be familiar with `async`/`await` and `CancellationToken` in C#.

## Set up the Assistant API

The Assistant API entry points are defined in the `Unity.AI.Assistant.Editor.Api` assembly. To reference the API in your script, add the following namespaces:

```csharp
using Unity.AI.Assistant.Editor.Api;
using Unity.AI.Assistant.Agents;
using Unity.AI.Assistant;
```

`AssistantApi` is a `static` class with three public ways to start Assistant:

- `Run`: opens the Assistant window and starts the prompt immediately.
- `PromptThenRun`: shows a popup to review or edit the prompt before submission.
- `RunHeadless`: an extension method on `IAgent` that runs the agent without opening the Assistant window and returns the final answer as a string.

Each method accepts an optional `AttachedContext` and a `CancellationToken`.

## Run Assistant in the Assistant window

Use `AssistantApi.Run` to open the Assistant window and submit a prompt.

```csharp
using System.Threading.Tasks;
using UnityEditor;
using Unity.AI.Assistant.Editor.Api;

public static class AssistantMenu
{
    [MenuItem("Tools/Ask Assistant to Optimize Scene")]
    public static async Task OptimizeScene()
    {
        await AssistantApi.Run("Suggest performance optimizations for the currently open scene.");
    }
}
```

The returned task completes when Assistant produces its final answer.

## Show a prompt popup before running

Use `AssistantApi.PromptThenRun` to show a popup where you can review and edit a prompt before it is sent. If you close the popup without submitting, the task completes without opening the Assistant window.

```csharp
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Unity.AI.Assistant.Editor.Api;

public class MyEditorWindow : EditorWindow
{
    async Task ShowPopupBelowButton(Rect buttonRect)
    {
        await AssistantApi.PromptThenRun(
            buttonRect,
            placeholderPrompt: "Describe what you want Assistant to do...");
    }
}
```

To anchor the popup to a UI Toolkit `VisualElement`, use the overload that accepts a `VisualElement`:

```csharp
await AssistantApi.PromptThenRun(myButton, placeholderPrompt: "Refactor this script...");
```

## Run an agent without opening the Assistant window

Run `RunHeadless` to run an `IAgent` without opening the Assistant window.

```csharp
using System.Threading.Tasks;
using Unity.AI.Assistant.Agents;
using Unity.AI.Assistant.Editor.Api;

public static class HeadlessExample
{
    public static async Task<string> Summarize(IAgent agent)
    {
        return await agent.RunHeadless("Summarize the build settings of this project.");
    }
}
```

The method returns the final answer as a string.

`RunHeadless` only works with the in-editor AI assistant provider. The call throws an exception if the workflow fails to initialize, if the provided cancellation token is canceled, or if the run exceeds the internal timeout.

## Attach context to a run

All Assistant API entry points accept an optional `AssistantApi.AttachedContext` object. Use this to attach Unity objects, virtual attachments, or image data to a prompt.

### Attach Unity objects

Attach scene objects or project assets so Assistant can inspect them as part of the request.
To attach Unity objects:

```csharp
using Unity.AI.Assistant.Editor.Api;
using UnityEditor;
using UnityEngine;

var context = new AssistantApi.AttachedContext();
context.Add(Selection.activeGameObject);
context.AddRange(Selection.objects);

await AssistantApi.Run("Explain what these objects do in the scene.", context);
```

### Attach a virtual attachment

Use `VirtualAttachment` to attach custom string data, such as JSON or generated text, without creating a Unity asset.

To attach virtual content:

```csharp
using Unity.AI.Assistant;
using Unity.AI.Assistant.Editor.Api;

var context = new AssistantApi.AttachedContext();
context.Add(new VirtualAttachment(
    payload: "{ \"fps\": 24, \"resolution\": \"1920x1080\" }",
    type: "application/json",
    displayName: "render-settings.json",
    metadata: null));

await AssistantApi.Run("Review these render settings.", context);
```

### Attach image content

Use `AddImageContent` to attach the pixels of a `Texture` so Assistant can analyze the image with vision capabilities. 

```csharp
var context = new AssistantApi.AttachedContext();
context.AddImageContent(myTexture, displayName: "concept-art.png");

await AssistantApi.Run("Describe the mood and color palette of this image.", context);
```

If you only need Assistant to know which texture asset is referenced, attach the `Texture` as an `Object` instead.

## Cancel a run request

Pass `CancellationToken` to cancel a running Assistant request.

To cancel a request:

1. Create a `CancellationTokenSource`.
2. Pass the token to the API call.
3. Catch cancellation exceptions if needed.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.Api;

var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

try
{
    await AssistantApi.Run("Audit this project for unused assets.", cancellationToken: cts.Token);
}
catch (Exception e) when (cts.IsCancellationRequested)
{
    UnityEngine.Debug.Log("User canceled the Assistant run.");
}
```

Cancellation throws an exception from the awaited `Task`, so wrap the call in `try`/`catch` if your editor flow needs to handle it gracefully.

## Run a custom agent

`RunHeadless` is an extension method on `IAgent`. Use `LlmAgent` to create a custom agent.

```csharp
using System.Threading.Tasks;
using Unity.AI.Assistant.Agents;
using Unity.AI.Assistant.Editor.Api;

public static class HeadlessExample
{
    public static async Task<string> ReviewCode()
    {
        IAgent agent = new LlmAgent(
            uniqueId: "MyTools.ReviewAgent",
            name: "Code Review Agent",
            description: "Reviews C# scripts for style and performance issues.");

        return await agent.RunHeadless("Review the selected script for issues.");
    }
}
```

The multi-agent orchestration system uses `description` to select the best agent for a given task, so make it specific.

## Additional resources

- [Create custom tools](xref:custom-tools)
- [MCP tools in Assistant](xref:mcp-landing)
- [MCP server](xref:unity-mcp-landing)
