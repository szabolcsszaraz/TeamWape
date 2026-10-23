---
uid: skills-landing
---

# Skills

Learn how to create, organize, enable, and test local skills that guide Assistant through reusable project workflows.

Skills are modular capabilities that extend Assistant with domain-specific instructions and metadata. You can use them to capture workflows that already work well in chat, keep conversations focused through progressive disclosure, and combine multiple skills for complex tasks in the Unity Editor.

Assistant discovers skills automatically, but can only use the skills that you explicitly set to **Allow** in **Preferences**. For more information, refer to [Enable a discovered skill](xref:skills-filesystem#enable-a-discovered-skill).

If you want to expose callable methods directly to Assistant with attributes instead of skill-authored C# implementation, refer to [Create custom tools](xref:custom-tools).

This section also explains how to add skills from the filesystem, validate them in the Unity Editor, and extend them with static utility functions.

| Topic | Description |
| ----- | ----------- |
| [About skills](xref:skills-overview) | Understand what skills are, how they work, and how they help Assistant follow reusable domain-specific workflows. |
| [Decide whether to create a skill](xref:skills-evaluate) | Evaluate whether a skill improves Assistant outcomes for a specific use case. |
| [Create skills from the filesystem](xref:skills-filesystem) | Add local skills by creating `SKILL.md` files and optional supporting resources in scanned folders. |
| [Test and validate skills](xref:skills-test) | Confirm that Assistant discovers skills, parses them correctly, and activates them in conversations. |
| [Use static utility functions in skills](xref:static-utility-functions) | Extend skills with public static C# methods that expose project-specific Editor operations. |
| [Example: Create a skill that generates and places a red top hat](xref:create-red-tophat-skill-example) | Follow a complete example that combines skill instructions, generated assets, static utility functions, and validation. |
| [Manage Skills page reference](xref:skills-reference) | Review discovered skills, enable or disable them, and inspect validation issues. |

## Additional resources

- [Create custom tools](xref:custom-tools)
- [Integrate models, skills, and tools](xref:integration-landing)