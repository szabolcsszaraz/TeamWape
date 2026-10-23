---
uid: assistant-best
---

# Best practices for using Assistant

Use these best practices to get accurate, relevant, and efficient results when you work with Assistant.

## Structure your prompts effectively

The quality of your prompt directly affects the quality of the response. Structure your prompts using the following pattern:

**Current state > Desired outcome > Technical details**

- **Current state**: What already exists in your project, such as scripts, components, or setup.
- **Desired outcome**: What you want to achieve or change.
- **Technical details**: Unity systems, constraints, or tools to use.

**Example**

- Use: `I have a player using a CharacterController. I want to add a dash mechanic that moves the player forward. Use the Input System and include a cooldown.`.
- Instead of: `Make a movement script.`.

## Provide clear and specific queries

**Be precise**: Clearly describe the problem or task, and avoid vague requests.

**Example**

- Use: `How do I fix the 'NullReferenceException' in my player movement script?`.
- Instead of: `How do I fix this script?`.

**Use context**: Include relevant details, such as components, settings, or scene setup to help Assistant understand your query better.

For example, `How do I change the material of a GameObject at runtime using a Renderer component?`.

**Reference scene objects**: You can refer to GameObjects in the **Hierarchy** window to provide additional context.

For example, `Why does the Sphere object not collide with the Plane in my scene?`.

## Use Unity-specific terminology

Use accurate Unity terms to improve response quality and ensure results are ready to use in the Unity Editor.

| Avoid | Use instead |
|------|-----------|
| 3D object | GameObject or Prefab |
| Properties | SerializedField, Component, or public variable |
| Screen | Scene View, Game View, or Canvas |
| Code file | Script or MonoBehaviour |
| Input/Controls | Input Action Asset, Input System, or KeyCode |
| Image/Graphic | Sprite, Texture2D, Material, or RenderTexture |
| Physics body | Rigidbody, Collider (Box/Sphere/Capsule/Mesh) |
| Camera plugin | Cinemachine, Virtual Camera, or FreeLook |
| Animation | Animator, Animation Clip, or Animator Controller |

## Leverage code generation and debugging

**Generate code**: Provide clear requirements when requesting scripts.

For example, `Create a C# script that rotates a GameObject continuously using Transform.Rotate.`.

**Debugging help**: When troubleshooting, include the following information in your prompt:

- The exact error message from the **Console**
- Relevant code
- Component setup
- Expected vs actual behavior

**Example**

- Use: `I’m getting a NullReferenceException on line 42 of my InventoryManager script. The UI panel is assigned in the Inspector but is null at runtime. Why?`.
- Instead of: `My script isn't working.`.

You can also copy and paste error messages directly from the **Console** window into Assistant. Assistant can identify the issue more accurately and provide more targeted solutions.

**Use Console context**: Select errors in the Console and ask Assistant for help.

## Work iteratively

Break complex tasks into smaller steps instead of solving everything in a single prompt.

1. Create the base functionality.
2. Add behavior or interaction.
3. Refine and optimize.

This approach produces more reliable results and simplifies debugging.

## Match prompt complexity to the task

Avoid over-complicating prompts.

- Start simple to get a working result.
- Add detail in later prompts for refinement.
- Provide more detail only when debugging or optimizing.

**Example**

- Use: `Create a player movement script with WASD movement, jumping, and sprinting`.
- Instead of: `Create a scalable, enterprise-grade movement system with extensibility`.

## Generate assets effectively

When you generate assets, provide clear artistic and technical direction:

- **Style**: Specify pixel art, realistic, or stylized.
- **Technical details**: Specify seamless, PBR, or sprite sheet.
- **Resolution**: For example, 64×64 or 1024×1024.
- **Context**: Intended use in your project.

For example, use `Generate a seamless PBR material of weathered stone for a dungeon environment.`.

## Customize Assistant settings

Adjust Assistant settings to match your workflow:

- [Control prompt submission behavior](xref:preferences#configure-prompt-submission)
- [Configure permission levels](xref:preferences#configure-permission-levels)
- [Enable or disable Autorun](xref:preferences#enable-autorun)
- [Manage reasoning display](xref:prompt-settings#toggle-collapse-reasoning-from-the-prompt)

When you provide feedback on responses, it improves the results over time.

## Integrate with Unity’s documentation

**Quick access**: Use Assistant to access Unity’s documentation and tutorials. Ask for links or summaries of relevant documentation.

For example, `Can you link me to the Unity documentation on Rigidbody components?`.

**Learning resources**: Request tutorials or best practices for specific features to enhance your skills.

For example, `Can you recommend a tutorial on creating custom shaders in Unity?`.

## Stay updated

Keep Unity and Assistant up-to-date to access the latest features and improvements. Review release notes regularly to stay informed about new capabilities.

## Additional resources

* [Work with Assistant](xref:get-started)
* [Assistant interface](xref:assistant-interface)