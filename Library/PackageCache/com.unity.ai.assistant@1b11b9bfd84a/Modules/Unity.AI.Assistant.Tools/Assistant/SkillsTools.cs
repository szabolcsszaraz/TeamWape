using System;
using System.Threading.Tasks;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Skills;

namespace Unity.AI.Assistant.Tools.Editor
{
    static class SkillsTools
    {
        // Tool IDs are directly used by backend, do not change without backend update.
        const string k_GetSkillBodyID = "Unity.Skill.ReadSkillBody";
        const string k_GetResourceContentID = "Unity.Skill.ReadSkillResource";

        const int k_MaxSkillContentChars = 1024 * 1024;

        static string TruncateIfNeeded(string content, string description)
        {
            if (content.Length <= k_MaxSkillContentChars)
                return content;
            return $"Truncated result: {description} exceeded the {k_MaxSkillContentChars}-character limit. Showing first {k_MaxSkillContentChars} characters only. Surface this as an IMPORTANT warning to the user in the response or summary.\nTruncated content:\n{content.Substring(0, k_MaxSkillContentChars)}";
        }
        
        [Serializable]
        public class GetResourceOutput
        {
            public string ResourcePath = string.Empty;
            public string ResourceContent = string.Empty;
        }
        
        [AgentTool("Returns a skill's body, frontmatter and markdown",
            k_GetSkillBodyID)]
        [AgentToolSettings(
            assistantMode: AssistantMode.Agent | AssistantMode.Ask | AssistantMode.Plan,
            toolCallEnvironment: ToolCallEnvironment.PlayMode | ToolCallEnvironment.EditMode,
            tags: FunctionCallingUtilities.k_StaticContextTag)]
        public static async Task<string> ReadSkillBody(
            ToolExecutionContext context,
            [ToolParameter("The skill's name as defined in metadata")]
            string skill_name
        )
        {
            if (string.IsNullOrEmpty(skill_name))
                throw new ArgumentException("Skill name cannot be empty.");

            var skills = await SkillsRegistry.GetSkillsAsync();
            if (!skills.TryGetValue(skill_name, out var skill))
                throw BuildSkillNotFoundException(skill_name);

            return TruncateIfNeeded(skill.Content, $"Skill '{skill_name}' content");
        }
        
        [AgentTool("Returns one skill's content for a given resource.",
            k_GetResourceContentID)]
        [AgentToolSettings(
            assistantMode: AssistantMode.Agent | AssistantMode.Ask | AssistantMode.Plan,
            toolCallEnvironment: ToolCallEnvironment.PlayMode | ToolCallEnvironment.EditMode,
            tags: FunctionCallingUtilities.k_StaticContextTag)]
        public static async Task<GetResourceOutput> GetSkillResourceContent(
            ToolExecutionContext context,
            [ToolParameter("The skill's name as defined in metadata")]
            string skill_name,
            [ToolParameter("The resource path to retrieve content from")]
            string resource_path
        )
        {
            if (string.IsNullOrEmpty(skill_name))
                throw new ArgumentException("Skill name cannot be empty.");
            if (string.IsNullOrEmpty(resource_path))
                throw new ArgumentException("Resource path cannot be empty.");

            var skills = await SkillsRegistry.GetSkillsAsync();
            if (!skills.TryGetValue(skill_name, out var skill))
                throw BuildSkillNotFoundException(skill_name);

            if (!skill.Resources.TryGetValue(resource_path, out var resource))
            {
                throw new ArgumentException($"Resource path {resource_path} doesn't exist in skill {skill_name}.");
            }
            
            var output = new GetResourceOutput();

            output.ResourcePath = resource_path;
            output.ResourceContent = LoadResourceContent(resource, resource_path, skill_name);

            return output;
        }

        // Internal so tests can assert the exact message the agent receives through
        // AIAssistantFunctionCaller.GetExceptionErrorMessage.
        internal static string LoadResourceContent(ISkillResource resource, string resourcePath, string skillName)
        {
            try
            {
                // Passing the truncation budget lets the resource stop reading one char past it
                // instead of materializing arbitrarily large content that gets truncated anyway.
                return TruncateIfNeeded(resource.GetContent(k_MaxSkillContentChars), $"Resource '{resourcePath}' in skill '{skillName}'");
            }
            // Encoding failures arrive as InvalidOperationException, not IOException, so catching only
            // the latter let the low-level message reach the agent unattributed to a skill.
            catch (Exception ex) when (ex is System.IO.IOException or InvalidOperationException)
            {
                // Deliberately no inner exception: AIAssistantFunctionCaller.GetExceptionErrorMessage
                // prefers InnerException.Message, so passing ex along would hand the agent the raw
                // unattributed message this wrapper exists to replace. ex.Message is embedded instead.
                throw new InvalidOperationException($"Failed to load resource '{resourcePath}' from skill '{skillName}': {ex.Message}");
            }
        }

        static ArgumentException BuildSkillNotFoundException(string skillName)
        {
            var timeoutDesc = SkillsScanner.GetPendingScansDescription();
            if (timeoutDesc != null)
                return new ArgumentException(
                    $"Skill '{skillName}' was not found - the skill list may be incomplete. {timeoutDesc} " +
                    $"Ask the user to wait a moment and retry, or rescan from Project Settings > AI > Assistant.");

            return new ArgumentException($"Skill '{skillName}' doesn't exist in local skills.");
        }
    }
}
