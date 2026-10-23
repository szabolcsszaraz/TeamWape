# Unity AI Assistant — Sample Integration (Full Documentation)

This sample demonstrates how to use the **AI Assistant API** to include AI capabilities in your package or module.

It showcases how to build:

* A **custom agent** with a system prompt and custom tools
* **Custom tools** with parameters, return values, async execution, and permission checks
* **Custom UI rendering** for tool calls
* **Custom user interactions** during tool execution
* **Custom link handlers** for markdown links returned by the assistant
* Use of **custom context data** to give the agent additional information
* Different ways to **run an agent**: headless, with UI, or through a prompt popup
* Integration inside an **EditorWindow**
* Persistent storage for tools
* A **custom skill** setup

The full sample code [can be found here](../Sample/ApiExample.cs) and is intended to help developers understand how to build rich editor integrations.

---

# Table of Contents

1. [Overview](#overview)
2. [Running the Assistant](#running-the-agent)
    * [Headless mode](#headless-mode)
    * [Run with Assistant UI](#run-with-assistant-ui)
    * [PromptThenRun](#prompt-before-run)
3. [Attaching Custom Context Data](#attaching-custom-context-data)
4. [Defining a Custom Agent](#defining-a-custom-agent)
5. [Implementing Custom Tools](#implementing-custom-tools)
    * [`GetLocation`](#getlocation)
    * [`GetWeather`](#getweather)
    * [`SaveNote`](#savenote-and-permissions-system)
6. [Building a Custom Interaction UI](#building-a-custom-interaction-ui)
7. [Adding a Custom Tool Renderer](#adding-a-custom-tool-renderer)
8. [Implementing a Custom Link Handler](#implementing-a-custom-link-handler)
9. [Editor Window Integration](#editor-window-integration)

---

# Overview

The purpose of this integration is to show how the AI Assistant can:

* Access project data
* Request user input
* Trigger custom tools
* Save output to files
* Display custom UI and markdown
* Orchestrate tools and agent behavior

The example defines a simple **Personal Assistant** agent that recommends daily activities based on the weather and saves notes automatically.

---

### What this does

* Runs automatically when the editor reloads so that agent is still available after a domain reload
* Registers the sample agent so it’s available globally in the AI Assistant
* Defines in which modes this agent is available (`AssistantMode.Agent | AssistantMode.Ask`)

---

# Running the Assistant

The sample demonstrates **three different ways** to run the Assistant.

## Headless Mode

A one-shot execution of a single custom agent with no Assistant UI.

```csharp
public static async Task<string> RunHeadless()
{
    var agent = CreateSampleAgent();

    var attachedContext = new AssistantApi.AttachedContext();
    attachedContext.VirtualAttachments.Add(new VirtualAttachment(
        payload: "{username: John, hobbies: [hiking, gaming, cooking], city: Montreal}",
        type: "WeatherData",
        displayName: "Weather Data"
    ));

    return await agent.RunHeadless("What should I do today?", attachedContext);
}
```

### Explanation

* Creates an instance of the agent
* Attaches custom context to give extra information to the agent
* Executes the request programmatically ans wait for it to finish
* Extracts the final LLM-generated text result, which is the last received block

---

## Run with Assistant UI

Executes through the official Assistant Window, along with all existing agents.

```csharp
await AssistantApi.Run("What should I do today?", attachedContext);
```

### Explanation

* The AI Assistant Window opens
* The agent participates in orchestration and will most likely be picked for this task
* The user can interact with the Assistant Window to ask follow-up question, possibly using other agents

---

## Prompt-Then-Run

Shows a popup allowing the user to edit the request before sending it.

```csharp
await AssistantApi.PromptThenRun(parent, "What should I do today?", attachedContext);
```

### Explanation

* A popup will be shown next to the provided `VisualElement` or `Rect`
* The user can edit the placeholder prompt
* Sending the request would then open the Assistant Window and behave as [Run with Assistant UI](#run-with-assistant-ui) 

---

# Attaching Custom Context Data

Custom context data allows you to give structured additional information to the agent.

```csharp
var customAttachment = new VirtualAttachment(
    payload: "{username: John, hobbies: [hiking, gaming, cooking]}",
    type: "WeatherData",
    displayName: "Weather Data"
);
attachedContext.VirtualAttachments.Add(customAttachment);
```

### Why this matters

* Gives the agent extra information without modifying the user prompt (particularly useful for Prompt-Then-Run)
* Avoids unnecessary tool calls which would introduce extra delay
* Can contain arbitrary JSON data or Unity object references (through `attachedContext.Objects`)
* Useful for domain-specific context (user profiles, assets, scene data, etc.)

---

# Defining a Custom Agent

The sample agent is defined in **`CreateSampleAgent()`**:

```csharp
var agent = new LlmAgent()
    .WithId(k_SampleAgentId)
    .WithName("Personal Assistant")
    .WithDescription("Specialized agent to help plan your daily activities according to the weather.")
    .WithSystemPrompt(@"
        You are a friendly personal assistant. Use your tools to:
        1. Suggest activities based on the weather
        2. ALWAYS save the list of suggestions into personal notes

        In your final answer, when you mention a specific activity name, always mention it as a url with the following format:
        [Activity Name](sample://activity_name)
    ")
    .WithToolsFrom<SampleTools>();
```

### Features shown here

* Named agent with description: the description is used to select the best agent for a task
* System prompt instructing:
    * How to behave
    * How to respond
    * How to use tools
* A unique ID to identify this agent among other agents
* Tools made available to this specific agent (here all tools from the `SampleTools` class)
* Custom URL scheme included in the answer: used by the [custom link handler](#implementing-a-custom-link-handler)

---

# Implementing Custom Tools

Tools are plain C# methods that can:

* Have arbitrary parameters
* Return arbitrary types
* Be synchronous or asynchronous
* Request user input via interactions
* Check permissions

## `GetLocation`

```csharp
[AgentTool(
    "Get the current location of the user.",
    "Unity.ApiSample.GetLocation",
    assistantMode: AssistantMode.Agent | AssistantMode.Ask,  // Available in both modes as it is a read-only tool
    )]
public static async Task<string> GetLocation(ToolExecutionContext context)
{
    // Check persistent storage to avoid asking user again
    const string locationStateKey = "location";
    if (context.Conversation.PersistentStorage.TryGetState<string>(locationStateKey, out var storedLocation))
        return storedLocation;
    
    // Create an interaction UX to pick a city
    var locationInteraction = new SampleInteraction(new List<string> { "Paris", "London", "Montreal", "Berlin" });

    // Wait for user interaction result
    var location = await context.Interactions.WaitForUser(locationInteraction);
    
    // Store location result for this conversation so that we don't ask user again
    context.Conversation.PersistentStorage.SetState(locationStateKey, location);
    
    return location;
}
```

### Highlights

* Read-only tool available in all modes (`Agent` and `Ask`)
* Shows a [custom UI](#building-a-custom-interaction-ui) for the user (`SampleInteraction`)
* Suspends tool execution until the user picks a city
* Uses a persistent storage to avoid asking for the user location again (note that this is just to showcase the feature, it is not the ideal use case for persistent storage.)

---

## `GetWeather`

```csharp
[AgentTool(
    "Return the weather at a given location.",
    "Unity.ApiSample.GetWeather",
    assistantMode: AssistantMode.Agent | AssistantMode.Ask)]
public static WeatherOutput GetWeather(string location)
```

### Highlights

* Returns a **structured type** (`WeatherOutput`)
* Demonstrates how agents can consume strongly typed results

---

## `SaveNote` and Permissions System

```csharp
[AgentTool(
    "Saves a note to a file.",
    "Unity.ApiSample.SaveNote",
    assistantMode: AssistantMode.Agent)]
public static async Task SaveNote(ToolExecutionContext context, string text)
{
    const string folderPath = "Assets/TempNotes";
    var fileName = $"Note_{DateTime.Now:yyyyMMdd_HHmmssfff}.txt";
    var filePath = Path.Combine(folderPath, fileName);

    await context.Permissions.CheckFileSystemAccess(
        IToolPermissions.ItemOperation.Create, filePath);

    if (!Directory.Exists(folderPath))
        Directory.CreateDirectory(folderPath);

    await File.WriteAllTextAsync(filePath, text);

    AssetDatabase.ImportAsset(filePath);
    AssetDatabase.Refresh();
}
```

### Highlights

* Tool modifies user files: permission check required *before* the modification is done
* Demonstrates best practice for all tools that modify:
    * Files
    * Assets
    * GameObjects or other Objects
    * Project settings

---

# Building a Custom Interaction UI

The agent requests a location using a custom interaction element:

```csharp
class SampleInteraction : BaseInteraction<string>
{
    public SampleInteraction(List<string> choices)
    {
        foreach (var choice in choices)
        {
            var button = new Button(() => CompleteInteraction(choice))
            {
                text = choice
            };
            Add(button);
        }
    }
}
```

### Explanation

* Inherits from `BaseInteraction<T>`
* Displays one button per option
* Calling `CompleteInteraction(value)` ends the waiting state and sets the interaction result
* The tool execution resumes with the selected answer

This is ideal for tool calls requiring user choices.

---

# Adding a Custom Tool Renderer

The sample provides a custom renderer for the `GetWeather` tool:

```csharp
[FunctionCallRenderer(typeof(SampleTools), nameof(SampleTools.GetWeather))]
class GetWeatherRenderer : DefaultFunctionCallRenderer
{
    public override void OnCallSuccess(string functionId, Guid callId, IFunctionCaller.CallResult result)
    {
        var weather = result.GetTypedResult<SampleTools.WeatherOutput>();
        var typeText = weather.Type switch {
            WeatherType.Sun => "☀️",
            WeatherType.Cloud => "☁️",
            WeatherType.Rain => "🌧️",
            WeatherType.Snow => "❄️",
        };
        Add(FunctionCallUtils.CreateContentLabel($"{typeText} {weather.Temperature}°C"));
    }
}
```

### What this does

* When the tool runs successfully, the result is rendered with emoji
* Provides a richer UI experience than plain JSON
* Lets you customize UI on a per-tool basis

---

# Implementing a Custom Link Handler

The agent outputs links like:

```
[Hiking](sample://hiking)
```

The handler reacts to `sample://` URLs:

```csharp
[LinkHandler("sample")]
public class SampleLinkHandler : ILinkHandler
{
    public void Handle(ILinkHandler.Context context, string prefix, string url)
    {
        EditorUtility.DisplayDialog(
            "Link Clicked",
            $"Clicked link: '{url}' with prefix '{prefix}'",
            "OK"
        );
    }
}
```

### Use cases

* Navigating to custom tools
* Opening assets
* Triggering editor actions
* Implementing custom protocols

---

# Editor Window Integration

The sample includes a simple `EditorWindow` to trigger the different run modes:

```csharp
public class SampleWindow : EditorWindow
{
    public void CreateGUI()
    {
        m_HeadlessButton = new Button(() => _ = RunHeadless()) { text = "Run Headless" };
        var withUiButton = new Button(RunWithUI) { text = "Run with UI" };
        m_PromptThenRunButton = new Button(PromptThenRun) { text = "Prompt then Run" };
        ...
    }
}
```

### What this demonstrates

* Integrating the assistant into custom editor tools
* Calling API methods from UI
* Displaying the assistant’s result in dialogs

---