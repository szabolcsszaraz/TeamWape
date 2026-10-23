using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Assistant.Bridge.Editor;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Api;
using Unity.AI.Assistant.Skills;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Integrations.Profiler.Editor
{
    class ProjectAuditorAssistant : IProxyProjectAuditorAskAssistantService
    {
        internal const string k_SkillName = "unity-project-auditor-skill";
        internal const string k_SkillTag = "Skills.Profiler";

        [InitializeOnLoadMethod]
        static void InitializeAgent()
        {
            try
            {
                var skill = CreateProjectAuditorSkill();
                SkillsRegistry.AddSkills(new List<SkillDefinition> { skill });
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize Project Auditor Skill: {ex.Message}");
            }
        }

        public bool Initialize()
        {
            return SkillsRegistry.GetSkills().ContainsKey(k_SkillName);
        }

        public void Dispose()
        {
            // Nothing to do - agent is always registered to ensure we can continue conversation on domain reload.
        }

        public void ShowAskAssistantPopup(Rect parentRect, IProxyAskAssistantService.Context context, string prompt)
        {
            if (string.IsNullOrEmpty(context.Payload))
                throw new ArgumentException("Payload cannot be null or empty", nameof(context.Payload));
            if (string.IsNullOrEmpty(prompt))
                throw new ArgumentException("Prompt cannot be null or empty", nameof(prompt));

            var attachment = new VirtualAttachment(context.Payload, context.Type, context.DisplayName, context.Metadata);
            try
            {
                var attachedContext = new AssistantApi.AttachedContext();
                attachedContext.Add(attachment);
                _ = AssistantApi.PromptThenRunInternal(parentRect, prompt, attachedContext, integrationName: IntegrationName.ProjectAuditor);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private class PromptGetter : LazyFileConfiguration<string>
        {
            public PromptGetter(string defaultPrompt, string path) : base(defaultPrompt, path) { }
            protected override string Parse(FileStream stream)
            {
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            public override string ToString()
            {
                return Data;
            }
        }

        // Use path to a local text file for faster iteration during development.
        private static readonly PromptGetter k_SystemPrompt = new PromptGetter(k_DefaultSystemPrompt, null);
        private const string k_DefaultSystemPrompt =
@"## Role
You are a professional Unity game engine performance expert.
Your role is to help resolving diagnostics issues surfaced by the Project Auditor.
Focus on Unity-specific performance considerations.

## Workflow
1. Always fetch relevant code snippets from provided file information in 'File Name:' and 'Line Number:' parameters in the attachment
2. Suggest improvements based on the code and recommendations
3. Read text files in parts and split automatically to fit the context window
4. Plan changes first before editing files
5. Ask user for clarifications when needed

## Code Change Rules
When suggesting code changes:
- Write code changes in large batches to minimize the number of code reloads
- Make minimal changes needed to address the issue
- Keep the code style and formatting consistent with the existing code";

        static SkillDefinition CreateProjectAuditorSkill()
        {
            return new SkillDefinition()
                .WithName(k_SkillName)
                .WithDescription("Skill that handles requests with 'Project Auditor Issue' attachment and 'Project Auditor' references. ALWAYS use this skill for such issues.")
                .WithTag(k_SkillTag)
                .WithTag(SkillRegistryTags.BuiltIn) // bypasses the user opt-in filter; never cleared by file scanners
                .WithContent(k_SystemPrompt.Data)
                .WithToolsFrom<FileProfilingTools>();
        }
    }
}
