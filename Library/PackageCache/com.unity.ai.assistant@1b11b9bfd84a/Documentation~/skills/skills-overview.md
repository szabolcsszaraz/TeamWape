---
uid: skills-overview
---

# About skills

Use skills to extend Assistant with reusable workflows.

Skills are modular capabilities that extend Assistant with instructions and metadata for specific workflows. They reduce repeated prompting and support domain-specific tasks without hard-coded logic. They also keep Assistant focused with use of progressive disclosure, initially loading only skill metadata.

Skills can also work together in a conversation. Assistant combines multiple skills for more complex tasks in the Unity Editor.

A skill can rely on built-in Assistant actions, such as reading project files and assets, querying scene objects and component data, capturing screenshots, and running or editing C# code. These actions are always available to any skill without additional declaration.

## Skill activation and opt-in

Assistant discovers skills automatically from project, user, and package locations. However, Assistant uses only the skills that you explicitly enable.

To allow Assistant to use a skill, set it to **Allow** in **Preferences**. By default, newly discovered skills are set to **Deny** until you review and enable them. For more information, refer to [Enable a discovered skill](xref:skills-filesystem#enable-a-discovered-skill).

## Skill sources

Assistant discovers skills from the following locations:

- **Project skills**: Skills stored in your project (for example, under `Assets/`).
- **User skills**: Skills stored in the user-level folder on your machine.
- **Package skills**: Skills included in installed Unity packages. Package skills are installed through the Unity Package Manager. A package can include an `AIAssistantSkills` folder that contains one or more skills. For example, a package might include a skill at `Packages/com.mycompany.mypackage/AIAssistantSkills/my-skill/SKILL.md`.

## Characteristics of a skill

Skills have the following characteristics:

- **Modular instructions and metadata**: Skills package instructions and metadata that teach Assistant a workflow or domain-specific task.
- **Progressive disclosure**: Only skill metadata is always loaded in the context window. Supporting instructions and referenced files are loaded when needed.
- **Reusable natural-language workflows**: Skills describe workflows in concise natural language and don't require a strict phrasing pattern.
- **Always-available built-in actions**: Skills can guide Assistant to use built-in Editor actions, such as reading project files, querying scene data, capturing screenshots, and running or editing C# code.
- **Support files loaded on demand**: Skills can reference files by relative path, and Assistant loads those files only when the skill instructions direct it to.
- **Support for project-specific APIs**: Skills can direct Assistant to call static utility functions by a fully qualified name through C# code execution.

## Use supporting files to keep skills focused

A skill can include supporting files in subfolders and reference them by relative path from `SKILL.md`. These files are loaded on demand when the skill is active rather than being included in the initial context.

Supporting files can contain code templates, API references, or detailed step-by-step instructions. This keeps the main `SKILL.md` file concise while still allowing the skill to access detailed information when required.

## Extend skills with static utility functions

Besides built-in Assistant actions, a skill can direct Assistant to call static utility functions by invoking C# code. Static utility functions are public static C# methods that wrap project-specific Editor workflows or APIs. Use them when an operation is too specific or too fragile for open-ended code generation.

A skill calls a static utility function by its fully qualified name and can reference an API file under `resources/` for parameter and return-value details. Static utility functions don't need to be declared in `SKILL.md` frontmatter. The examples in this documentation are illustrative only, and skill instructions don't need to follow a fixed syntax.

## Extend skills with custom tools

Besides built-in actions and static utility functions, a skill can use [custom tools](xref:custom-tools) to perform structured operations.

A custom tool is a public static C# method annotated with the `[AgentTool]` attribute. Assistant discovers these tools automatically and uses the provided descriptions to determine when to call them and what values to pass.

Unlike static utility functions, which the skill calls through generated C# code, tools are invoked directly by Assistant based on their definitions and parameter descriptions.

Use custom tools when you want to expose higher-level or reusable operations, such as creating assets, querying project data, or performing file operations.

For information on how to define and configure tools, refer to [Create custom tools](xref:custom-tools).

## Additional resources

- [Create skills from the filesystem](xref:skills-filesystem)
- [Use static utility functions in skills](xref:static-utility-functions)
- [Create custom tools](xref:custom-tools)