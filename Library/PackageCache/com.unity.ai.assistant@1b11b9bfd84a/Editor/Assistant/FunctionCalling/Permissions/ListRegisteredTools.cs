using System.Linq;
using System.Text;
using Unity.AI.Assistant.FunctionCalling;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor
{
    static class ListRegisteredTools
    {
#if ASSISTANT_INTERNAL
        [MenuItem("AI Assistant/Internals/Tools/List Registered Tools")]
        static void ListAll()
        {
            var tools = ToolRegistry.FunctionToolbox.Tools
                .Where(t => t.FunctionDefinition != null)
                .ToList();

            if (tools.Count == 0)
            {
                Debug.Log("[AI Assistant] No tools registered.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[AI Assistant] {tools.Count} registered tool(s):");

            foreach (var group in tools.GroupBy(t => t.FunctionDefinition.Tags?.FirstOrDefault() ?? "none").OrderBy(g => g.Key))
            {
                sb.AppendLine($"\n  [{group.Key}]");
                foreach (var tool in group.OrderBy(t => t.FunctionDefinition.FunctionId))
                {
                    var def = tool.FunctionDefinition;
                    var env = (tool as LocalAssistantFunction)?.ToolCallEnvironment;
                    var tags = def.Tags != null ? string.Join(", ", def.Tags) : "";
                    sb.AppendLine($"    {def.FunctionId} <color=#888888>[{def.AssistantMode}, {env}]</color> <color=#5BA3CF>{tags}</color>");
                }
            }

            Debug.Log(sb.ToString());
        }
#endif
    }
}
